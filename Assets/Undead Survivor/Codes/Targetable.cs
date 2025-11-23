using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 유닛의 생명력, 피격, 사망 처리를 담당하는 핵심 컴포넌트입니다.
/// </summary>
public class Targetable : MonoBehaviour
{
    public enum Faction
    {
        Player,
        Enemy
    }

    [Header("진영 설정")]
    public Faction faction;

    [Header("체력(HP) 설정")]
    public float maxHealth = 10f;
    public float currentHealth;
    public bool isDead = false;

    [Header("레벨/드롭 아이템 설정")]
    public int dropItemIndex = -1;

    [Header("넉백 설정")]
    public float knockbackPower = 20f; // 넉백 힘 (기본값)
    public float knockbackDuration = 0.2f;

    [Header("피격 피드백 (무적/색상)")]
    [Tooltip("피격 후 무적 시간(초).")]
    public float invincibilityDuration = 0.2f;

    // [수정] 희미해지는 효과를 위해 알파값(A)을 낮춘 색상 사용
    // A 값을 0.5 정도로 설정하면 반투명해짐
    public Color invincibilityColor = new Color(1f, 0.5f, 0.5f, 0.5f);

    public UnityEvent onDie;

    // 내부 변수
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private PoolManager poolManager;
    private bool isInvincible = false;
    private Color originalColor;
    private bool isKnockedBack = false; // 넉백 상태 플래그

    // 외부에서 넉백 상태 확인용 프로퍼티
    public bool IsKnockedBack => isKnockedBack;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();

        if (GameManager.instance != null)
        {
            poolManager = GameManager.instance.Pool;
        }

        if (spriter != null)
        {
            originalColor = spriter.color;
        }
    }

    private void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;
        isInvincible = false;
        isKnockedBack = false;

        if (spriter != null)
        {
            spriter.color = originalColor;
        }
        if (rigid != null)
        {
            rigid.linearVelocity = UnityEngine.Vector2.zero;
        }
    }

    /// <summary>
    /// 외부에서 호출하여 데미지를 주는 함수
    /// </summary>
    public void TakeDamage(float damage, Transform attacker)
    {
        if (isDead || isInvincible) return;

        // 체력 감소
        currentHealth -= damage;

        // 넉백 & 무적 효과 (공격자가 있을 때만)
        if (attacker != null)
        {
            ApplyKnockback(attacker);
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(InvincibilityBlinkRoutine());
        }

        // 사망 체크
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        DropItem();

        // 경험치 획득 코드 삭제됨 (중복 방지)

        // 킬 수 증가
        if (GameManager.instance != null && faction == Faction.Enemy)
        {
            GameManager.instance.AddKill();
        }

        onDie.Invoke();
        gameObject.SetActive(false);
    }

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

    // --- 피격 효과 관련 코루틴 ---

    private IEnumerator InvincibilityBlinkRoutine()
    {
        isInvincible = true;

        if (spriter != null)
        {
            // [수정] 피격 색상(반투명) 적용
            spriter.color = invincibilityColor;
        }

        yield return new WaitForSeconds(invincibilityDuration);

        isInvincible = false;
        if (spriter != null)
        {
            // 원래 색상(불투명) 복구
            spriter.color = originalColor;
        }
    }

    private void ApplyKnockback(Transform attacker)
    {
        if (rigid == null) return;

        // 공격자 반대 방향 계산 (Vector2 모호성 해결)
        UnityEngine.Vector2 knockbackDir = (transform.position - attacker.position).normalized;

        // Targetable 자체적으로 물리 넉백 처리
        if (isKnockedBack) StopCoroutine("PhysicsKnockback");
        StartCoroutine(PhysicsKnockback(knockbackDir));
    }

    private IEnumerator PhysicsKnockback(UnityEngine.Vector2 dir)
    {
        isKnockedBack = true;

        // 1. 기존 속도 초기화 (관성 제거)
        rigid.linearVelocity = UnityEngine.Vector2.zero;

        // 2. 순간적인 힘(Impulse)을 가해 밀어냄
        rigid.AddForce(dir * knockbackPower, ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);

        // 3. 넉백 후 정지
        rigid.linearVelocity = UnityEngine.Vector2.zero;
        isKnockedBack = false;
    }
}