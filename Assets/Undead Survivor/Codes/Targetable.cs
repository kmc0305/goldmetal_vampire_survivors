using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // UnityEvent를 사용하기 위해 추가

/// <summary>
/// [핵심 유닛 컴포넌트]
/// 모든 유닛(플레이어, 적, 아군, 성)의 진영, 체력(HP), 사망, 레벨업(경험치)을 관리합니다.
/// 이 스크립트가 붙어있는 오브젝트는 '공격 대상이 될 수 있는 생명체'임을 의미합니다.
/// </summary>
public class Targetable : MonoBehaviour
{
    [Header("진영 설정")]
    /// <summary>이 유닛의 소속 진영 (아군 또는 적군)</summary>
    public Faction faction;

    [Header("체력(HP) 설정")]
    /// <summary>유닛의 최대 체력</summary>
    public float maxHealth = 10f;
    /// <summary>유닛의 현재 체력 (OnEnable에서 maxHealth로 초기화됨)</summary>
    public float currentHealth;
    /// <summary>유닛의 사망 상태 (true이면 더 이상 데미지를 받지 않음)</summary>
    public bool isDead = false;

    [Header("레벨/드롭 아이템 설정")]
    /// <summary>사망 시 PoolManager에서 가져올 아이템의 인덱스. -1이면 드롭 안 함. (예: 3 = ExpGem)</summary>
    public int dropItemIndex = -1;

    // ★★★ [신규] 넉백 설정 ★★★
    [Header("넉백 설정")]
    /// <summary>피격 시 밀려나는 힘의 크기 (0이면 넉백 없음)</summary>
    public float knockbackPower = 4f;
    /// <summary>넉백이 지속되는 시간 (초)</summary>
    public float knockbackDuration = 0.2f;
    // ★★★ [신규] 넉백 설정 끝 ★★★

    [Header("사망 이벤트")]
    /// <summary>
    /// 유닛이 사망(Die)할 때 호출할 함수들을 인스펙터(Inspector)에서 연결할 수 있습니다.
    /// (예: 점수 올리기, 사망 사운드 재생, 사망 이펙트 스폰 등)
    /// </summary>
    public UnityEvent onDie;

    // 내부 참조
    /// <summary>아이템 드롭에 사용할 PoolManager 참조</summary>
    private PoolManager poolManager;

    /// <summary>
    /// 진영을 구분하기 위한 열거형(Enum)
    /// </summary>
    public enum Faction
    {
        Ally,  // 아군 진영
        Enemy  // 적군 진영
    }

    /// <summary>
    /// [Unity 이벤트] Start() - 모든 Awake()가 끝난 후 1회 호출
    /// </summary>
    void Start()
    {
        // GameManager는 싱글톤(Singleton)이므로 'GameManager.instance'로 쉽게 접근 가능합니다.
        // 아이템 드롭에 필요한 PoolManager의 참조를 GameManager로부터 받아옵니다.
        // (참고: PoolManager가 null이어도 게임은 동작하지만, 아이템 드롭만 안 됩니다.)

        // [오류 방지] GameManager.instance가 null일 수 있으므로 Start()에서 호출
        if (GameManager.instance != null)
        {
            poolManager = GameManager.instance.Pool;
        }
        else
        {
            Debug.LogWarning("Targetable.cs: GameManager.instance가 null입니다. PoolManager 참조를 가져올 수 없습니다.");
        }
    }


    /// <summary>
    /// [Unity 이벤트] OnEnable() - 오브젝트가 활성화될 때마다 호출됩니다.
    /// (PoolManager.Get()으로 유닛이 재활용(스폰)될 때마다 이 함수가 실행됩니다.)
    /// </summary>
    void OnEnable()
    {
        // 유닛이 스폰(재활용)될 때마다 항상 '살아있는' 상태로 시작하도록 초기화합니다.
        isDead = false;
        // 체력을 최대치로 다시 채워줍니다.
        currentHealth = maxHealth;
    }

    /// <summary>
    /// [핵심 기능] 이 유닛에게 데미지를 입힙니다.
    /// (호출하는 곳: Enemy.cs, AllyAI.cs의 OnCollisionStay2D, MeleeWeapon.cs의 OnTriggerEnter2D 등)
    /// </summary>
    /// <param name="damage">입힐 데미지 양</param>
    /// <param name="attackerTransform">
    /// 공격자의 Transform 정보입니다. 넉백 방향을 계산하기 위해 사용됩니다.
    /// (공격자가 없거나 넉백이 필요 없다면 null을 전달해도 됩니다.)
    /// </param>
    public void TakeDamage(float damage, Transform attackerTransform)
    {
        // 1. 이미 죽은 유닛이라면, 이후의 모든 로직을 무시하고 즉시 함수를 종료합니다.
        if (isDead) return;

        // 2. 현재 체력에서 데미지 수치만큼 깎습니다.
        currentHealth -= damage;
        // Debug.Log(gameObject.name + " 체력: " + currentHealth); // (디버깅용)

        // (선택적) 여기에 피격 애니메이션이나 이펙트 호출 코드를 추가할 수 있습니다.
        // animator.SetTrigger("Hit");
        // StartCoroutine(BlinkEffect()); // 예: 피격 시 깜박임 효과

        // 3. 넉백 로직 호출
        //    - 공격자가 존재하고 (attackerTransform != null)
        //    - 이 유닛의 넉백 힘(knockbackPower)이 0보다 클 때만 넉백을 실행합니다.
        if (attackerTransform != null && knockbackPower > 0)
        {
            // 3-1. 넉백 방향 계산: (내 위치 - 공격자 위치) = 공격자로부터 멀어지는 방향
            Vector2 knockbackDir = (transform.position - attackerTransform.position).normalized;

            // 3-2. 내 AI 스크립트에 넉백 명령 전달
            //    이 Targetable 스크립트는 자신이 Enemy인지 Ally인지 모릅니다.
            //    따라서, 자신(gameObject)에게 Enemy.cs가 붙어있는지 확인하고,
            //    없다면 AllyAI.cs가 붙어있는지 확인하여, 해당 스크립트의 넉백 함수를 호출합니다.

            Enemy enemyAI = GetComponent<Enemy>();
            if (enemyAI != null)
            {
                // Enemy 스크립트의 ApplyKnockback 함수를 호출하여 넉백을 '요청'합니다.
                enemyAI.ApplyKnockback(knockbackDir, knockbackPower, knockbackDuration);
            }

            AllyAI allyAI = GetComponent<AllyAI>();
            if (allyAI != null)
            {
                // AllyAI 스크립트의 ApplyKnockback 함수를 호출하여 넉백을 '요청'합니다.
                allyAI.ApplyKnockback(knockbackDir, knockbackPower, knockbackDuration);
            }

            // (추후 확장) 플레이어 넉백 (Player.cs에도 ApplyKnockback 함수 추가 필요)
            // Player player = GetComponent<Player>();
            // if (player != null) { player.ApplyKnockback(knockbackDir, knockbackPower, knockbackDuration); }
        }
        // ★★★ 넉백 로직 끝 ★★★

        // 4. 체력이 0 이하로 떨어졌는지 확인합니다.
        if (currentHealth <= 0)
        {
            Die(); // 사망 처리 함수를 호출합니다.
        }
    }

    /// <summary>
    /// [핵심 기능] 이 유닛을 사망 처리합니다.
    /// (TakeDamage 함수 내부에서만 호출됩니다.)
    /// </summary>
    public void Die()
    {
        // 1. 중복 사망 방지: 이미 isDead가 true라면 즉시 함수를 종료합니다.
        if (isDead) return;
        isDead = true; // 즉시 사망 상태로 변경

        // 2. 아이템 드롭, 경험치 획득
        DropItem();
        GameManager.instance.getExp();

        // 3. Inspector에서 연결된 사망 이벤트 호출 (onDie)
        //    (예: GameManager의 점수(Score) 증가 함수가 연결되어 있을 수 있습니다.)
        onDie.Invoke();

        // 4. 오브젝트 비활성화 (풀링을 위해 Destroy 대신 SetActive(false) 사용)
        //    이렇게 하면 유닛이 PoolManager로 반납되고, 나중에 재사용(재활용)될 수 있습니다.
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 설정된 'dropItemIndex'에 해당하는 아이템을 현재 위치에 스폰(Spawn)합니다.
    /// (Die 함수 내부에서만 호출됩니다.)
    /// </summary>
    void DropItem()
    {
        // PoolManager 참조가 없거나 (Start에서 못 가져왔거나),
        // 드롭할 아이템이 설정(-1)되어있지 않으면 함수를 종료합니다.
        if (poolManager == null || dropItemIndex < 0)
            return;

        // PoolManager에게 dropItemIndex에 해당하는 아이템을 'Get' (요청)합니다.
        GameObject item = poolManager.Get(dropItemIndex);

        if (item != null)
        {
            // 아이템을 내(죽은 유닛) 위치에 스폰(배치)합니다.
            item.transform.position = transform.position;
        }
    }
}
