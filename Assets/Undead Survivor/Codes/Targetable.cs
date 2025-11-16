using System.Collections;
using System.Collections.Generic;
// using System.Diagnostics;  // <-- CS0104 'Debug' 모호성 오류를 일으켜 삭제
// using System.Numerics;    // <-- CS0104 'Vector2' 모호성 오류를 일으켜 삭제
using UnityEngine;
using UnityEngine.Events; // UnityEvent를 사용하기 위해 추가

/// <summary>
/// [핵심 유닛 컴포넌트] (수정됨)
/// 모든 유닛의 진영, 체력(HP), 사망, 드롭 아이템,
/// ★신규: 피격(넉백, 무적, 깜박임)을 모두 관리합니다.
/// </summary>
public class Targetable : MonoBehaviour
{
    // ... (기존 변수들은 그대로 유지) ...
    [Header("진영 설정")]
    public Faction faction;

    [Header("체력(HP) 설정")]
    public float maxHealth = 10f;
    public float currentHealth;
    public bool isDead = false;

    [Header("레벨/드롭 아이템 설정")]
    public int dropItemIndex = -1;

    [Header("넉백 설정")]
    public float knockbackPower = 4f;
    public float knockbackDuration = 0.2f;

    // ★★★ [신규] 피격 무적 및 시각적 피드백 ★★★
    [Header("피격 피드백 (무적/색상)")]
    [Tooltip("피격 후 무적 시간(초). 이 시간 동안은 데미지/넉백을 더 받지 않습니다.")]
    public float invincibilityDuration = 0.3f; // 0.3초간 무적
    [Tooltip("피격 시 깜박일 색상")]
    public Color invincibilityColor = Color.red; // 맞았을 때 변할 색상

    // --- 내부 참조 변수 ---
    private PoolManager poolManager;
    private SpriteRenderer spriter;     // [신규] 색상 변경을 위한 스프라이트 렌더러
    private Color originalColor;      // [신규] 유닛의 원래 색상
    private bool isInvincible = false;  // [신규] 현재 무적 상태인지 여부
    // ★★★ ---------------------------------- ★★★

    [Header("사망 이벤트")]
    public UnityEvent onDie;

    /// <summary> 진영을 구분하기 위한 열거형(Enum) </summary>
    public enum Faction { Ally, Enemy }

    /// <summary>
    /// [Unity 이벤트] Start() - 모든 Awake()가 끝난 후 1회 호출
    /// </summary>
    void Start()
    {
        if (GameManager.instance != null)
        {
            poolManager = GameManager.instance.Pool;
        }
        else
        {
            // 오류가 해결되어 이제 UnityEngine.Debug를 올바르게 참조합니다.
            Debug.LogWarning("Targetable.cs: GameManager.instance가 null입니다. PoolManager 참조를 가져올 수 없습니다.");
        }

        // [신규] 자식 오브젝트를 포함하여 SpriteRenderer를 찾습니다.
        // (유닛의 스프라이트가 자식 오브젝트에 있을 수 있으므로)
        spriter = GetComponentInChildren<SpriteRenderer>();
        if (spriter != null)
        {
            // 유닛의 원래 색상을 저장합니다.
            originalColor = spriter.color;
        }
    } // <-- CS1513 오류는 이 중괄호가 삭제되었을 때 발생할 수 있습니다.

    /// <summary>
    /// [Unity 이벤트] OnEnable() - 오브젝트가 활성화될 때마다 호출됩니다.
    /// </summary>
    void OnEnable()
    {
        isDead = false;
        currentHealth = maxHealth;
        isInvincible = false; // [신규] 스폰 시 무적 상태 초기화

        // [신규] 스폰 시 유닛 색상을 원래대로 복원
        // (무적 상태(빨간색)로 죽었을 경우를 대비)
        if (spriter != null)
        {
            spriter.color = originalColor;
        }
    }

    /// <summary>
    /// [핵심 기능] 이 유닛에게 데미지를 입힙니다.
    /// </summary>
    public void TakeDamage(float damage, Transform attackerTransform)
    {
        // 1. [수정] 이미 죽었거나 '무적(isInvincible)' 상태라면,
        //    데미지, 넉백, 무적 로직을 모두 무시하고 즉시 함수를 종료합니다.
        if (isDead || isInvincible) return;

        // 2. 현재 체력에서 데미지 수치만큼 깎습니다.
        currentHealth -= damage;
        // Debug.Log(gameObject.name + " 체력: " + currentHealth);

        // 3. [신규] 피격 즉시 '무적 및 깜박임' 코루틴(Coroutine)을 시작합니다.
        //    (넉백 로직보다 먼저 실행되어야, 넉백 중 추가 넉백을 막을 수 있습니다.)
        StartCoroutine(InvincibilityBlinkRoutine());

        // 4. [기존 넉백 로직] (변경 없음)
        if (attackerTransform != null && knockbackPower > 0)
        {
            // 오류가 해결되어 이제 UnityEngine.Vector2를 올바르게 참조합니다.
            Vector2 knockbackDir = (transform.position - attackerTransform.position).normalized;

            Enemy enemyAI = GetComponent<Enemy>();
            if (enemyAI != null)
            {
                enemyAI.ApplyKnockback(knockbackDir, knockbackPower, knockbackDuration);
            }

            AllyAI allyAI = GetComponent<AllyAI>();
            if (allyAI != null)
            {
                allyAI.ApplyKnockback(knockbackDir, knockbackPower, knockbackDuration);
            }

            // (추후 확장) 플레이어 넉백 (Player.cs)
        }

        // 5. 체력이 0 이하로 떨어졌는지 확인합니다.
        if (currentHealth <= 0)
        {
            Die(); // 사망 처리 함수를 호출합니다.
        }
    }

    // ... Die() 함수와 DropItem() 함수는 기존과 동일하게 유지 ...
    // (기존 Die() 함수)
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        DropItem();
        GameManager.instance.getExp(); // (참고: 이 함수는 Game Manager.cs에 없습니다. 추후 추가 필요)

        onDie.Invoke();

        gameObject.SetActive(false);
    }

    // (기존 DropItem() 함수)
    void DropItem()
    {
        if (poolManager == null || dropItemIndex < 0)
            return;

        GameObject item = poolManager.Get(dropItemIndex);

        if (item != null)
        {
            item.transform.position = transform.position;
        }
    }
    // ... ----------------------------------------- ...


    /// <summary>
    /// [신규] 지정된 시간(invincibilityDuration) 동안 무적 상태를 부여하고,
    /// 유닛의 색상을 변경(깜박임)했다가 되돌리는 코루틴(Coroutine)입니다.
    /// </summary>
    private IEnumerator InvincibilityBlinkRoutine()
    {
        // 1. 무적 상태 활성화
        isInvincible = true;

        // 2. 스프라이트 색상 변경 (피격 색상)
        if (spriter != null)
        {
            spriter.color = invincibilityColor;
        }

        // 3. 지정된 무적 시간(invincibilityDuration)만큼 대기
        yield return new WaitForSeconds(invincibilityDuration);

        // 4. 스프라이트 색상 복원 (원래 색상)
        if (spriter != null)
        {
            spriter.color = originalColor;
        }

        // 5. 무적 상태 비활성화
        isInvincible = false;
    }
}