using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// [핵심 유닛 컴포넌트] (수정됨)
/// 모든 유닛의 진영, 체력(HP), 사망, 드롭 아이템,
/// ★신규: 피격(넉백, 무적, 깜박임)을 모두 관리합니다.
/// </summary>
public class Targetable : MonoBehaviour
{
    [Header("진영 설정")]
    public Faction faction;

    [Header("체력(HP) 설정")]
    public float maxHealth = 10f;
    public float currentHealth;
    public bool isDead = false;

    // [수정] RangedEnemy 등 외부 스크립트에서 참조할 수 있도록 상태 변수 추가
    public bool IsKnockedBack = false;

    [Header("넉백 설정")]
    public float knockbackPower = 4f;
    public float knockbackDuration = 0.2f;

    [Header("피격 피드백 (무적/색상)")]
    [Tooltip("피격 후 무적 시간(초). 이 시간 동안은 데미지/넉백을 더 받지 않습니다.")]
    public float invincibilityDuration = 0.3f;
    [Tooltip("피격 시 깜박일 색상")]
    public Color invincibilityColor = Color.red;

    // --- 내부 참조 변수 ---
    private PoolManager poolManager;
    private SpriteRenderer spriter;
    private Color originalColor;
    private bool isInvincible = false;

    [Header("사망 이벤트")]
    public UnityEvent onDie;

    public enum Faction { Ally, Enemy }

    void Start()
    {
        if (GameManager.instance != null)
        {
            poolManager = GameManager.instance.Pool;
        }
        else
        {
            // [수정] UnityEngine.Debug 명시하여 모호함 해결
            UnityEngine.Debug.LogWarning("Targetable.cs: GameManager.instance가 null입니다.");
        }

        spriter = GetComponentInChildren<SpriteRenderer>();
        if (spriter != null)
        {
            originalColor = spriter.color;
        }
    }

    void OnEnable()
    {
        isDead = false;
        currentHealth = maxHealth;
        isInvincible = false;
        IsKnockedBack = false; // 상태 초기화

        if (spriter != null)
        {
            spriter.color = originalColor;
        }
    }

    public void TakeDamage(float damage, Transform attackerTransform)
    {
        if (isDead || isInvincible) return;

        currentHealth -= damage;

        StartCoroutine(InvincibilityBlinkRoutine());

        if (attackerTransform != null && knockbackPower > 0)
        {
            UnityEngine.Vector2 knockbackDir = (transform.position - attackerTransform.position).normalized;

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
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // 적군(Enemy)이 죽었을 때만 경험치를 얻도록 조건 추가
        if (faction == Faction.Enemy)
        {
            if (GameManager.instance != null)
                GameManager.instance.getExp();
        }

        onDie.Invoke();
        gameObject.SetActive(false);
    }

    private IEnumerator InvincibilityBlinkRoutine()
    {
        isInvincible = true;

        if (spriter != null)
        {
            spriter.color = invincibilityColor;
        }

        yield return new WaitForSeconds(invincibilityDuration);

        if (spriter != null)
        {
            spriter.color = originalColor;
        }

        isInvincible = false;
    }
}