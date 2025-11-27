using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Vector2 = UnityEngine.Vector2; // ✅ Vector2 모호함 방지

/// <summary>
/// 유닛의 생명력, 피격, 넉백, 사망 처리를 담당합니다.
/// [최적화]: 코루틴 관리 강화, 넉백 로직 안정화
/// [수정]: isDead 변수화, Heal 함수 추가, EXP 획득 로직 변경 (아이템 드랍 -> 즉시 획득)
/// </summary>
public class Targetable : MonoBehaviour
{
    public enum Faction
    {
        Player,
        Enemy,
        Neutral
    }

    [Header("진영 설정")]
    public Faction faction;

    [Header("체력(HP) 설정")]
    public float maxHealth = 10f;
    public float currentHealth;

    // ✅ [수정] 읽기 전용 프로퍼티( => )에서 변수 필드로 변경하여 수정 가능하게 함
    public bool isDead = false;

    [Header("레벨/드롭 아이템 설정")]
    public int dropItemIndex = -1;

    // ✅ [추가] 적 처치 시 획득할 경험치 양 (기본값 1)
    public int expReward = 1;

    [Header("넉백 설정")]
    public float knockbackPower = 20f;
    public float knockbackDuration = 0.2f;
    public bool isKnockbackable = true;
    private bool _isKnockedBack = false;

    /// <summary>
    /// 현재 넉백 상태인지 여부
    /// </summary>
    public bool IsKnockedBack
    {
        get { return _isKnockedBack; }
        private set { _isKnockedBack = value; }
    }

    [Header("피격 피드백 (무적/색상)")]
    public float invincibilityDuration = 0.2f;
    public Color invincibilityColor = new Color(1f, 0.5f, 0.5f, 0.5f); // 반투명 빨강

    public UnityEvent onDie;

    // 내부 변수
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private PoolManager poolManager;

    private bool isInvincible = false;
    private Color originalColor;
    private Coroutine knockbackCoroutine;
    private Coroutine flashCoroutine;
    private Coroutine healFlashCoroutine; // 힐 플래시 코루틴

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        // 스프라이트가 없으면 흰색을 기본으로
        originalColor = (spriter != null) ? spriter.color : Color.white;
    }

    void Start()
    {
        if (GameManager.instance != null)
        {
            poolManager = GameManager.instance.Pool;
        }
    }

    void OnEnable()
    {
        // 생성 시 초기화
        currentHealth = maxHealth;
        isDead = false;
        isInvincible = false;
        IsKnockedBack = false;

        if (spriter != null) spriter.color = originalColor;
        if (rigid != null) rigid.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 데미지를 입는 함수입니다.
    /// </summary>
    public void TakeDamage(float damage, Transform attacker = null)
    {
        if (isDead || isInvincible) return;

        currentHealth -= damage;

        // 피격 효과 (깜빡임)
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(InvincibilityBlinkRoutine());

        // 넉백 적용
        if (isKnockbackable && attacker != null)
        {
            ApplyKnockback(attacker);
        }

        // 사망 체크
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // ✅ HealingArea.cs 연동을 위한 힐 함수
    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        // 힐 이펙트 (초록색 깜빡임 등)
        if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
        healFlashCoroutine = StartCoroutine(HealFlashRoutine());
    }

    // ✅ HealingArea.cs 연동을 위한 힐 플래시 종료 함수
    public void StopHealFlashAndResetColor()
    {
        if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
        if (spriter != null) spriter.color = originalColor;
    }

    void Die()
    {
        isDead = true;
        onDie?.Invoke();

        // ✅ [수정] 적 유닛이 사망하면 즉시 경험치 획득
        // faction이 Enemy일 때만 경험치를 줍니다 (플레이어나 아군 사망 시 경험치 x)
        if (faction == Faction.Enemy && GameManager.instance != null)
        {
            // GameManager의 getExp()는 1씩 오르므로, expReward만큼 반복 호출
            for (int i = 0; i < expReward; i++)
            {
                GameManager.instance.getExp();
            }
        }

        // ✅ [수정] 아이템 드랍 로직 주석 처리 (경험치 젬 드랍 방지)
        // DropItem(); 

        gameObject.SetActive(false);
    }

    void DropItem()
    {
        if (poolManager == null || dropItemIndex < 0) return;

        GameObject item = poolManager.Get(dropItemIndex);
        if (item != null)
        {
            item.transform.position = transform.position;
            item.SetActive(true);
        }
    }

    // --- 피격 효과 관련 코루틴 ---
    private IEnumerator InvincibilityBlinkRoutine()
    {
        isInvincible = true;
        if (spriter != null) spriter.color = invincibilityColor;
        yield return new WaitForSeconds(invincibilityDuration);
        if (spriter != null) spriter.color = originalColor;
        isInvincible = false;
    }

    // ✅ 힐 효과 코루틴
    private IEnumerator HealFlashRoutine()
    {
        if (spriter != null) spriter.color = Color.green; // 힐은 초록색
        yield return new WaitForSeconds(0.2f);
        if (spriter != null) spriter.color = originalColor;
    }

    private void ApplyKnockback(Transform attacker)
    {
        if (rigid == null) return;
        Vector2 knockbackDir = (transform.position - attacker.position).normalized;

        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(PhysicsKnockback(knockbackDir));
    }

    private IEnumerator PhysicsKnockback(Vector2 dir)
    {
        IsKnockedBack = true;
        rigid.linearVelocity = Vector2.zero;
        rigid.AddForce(dir * knockbackPower, ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);

        rigid.linearVelocity = Vector2.zero;
        IsKnockedBack = false;
    }
}