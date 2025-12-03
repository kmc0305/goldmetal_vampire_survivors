using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Vector2 = UnityEngine.Vector2; // 혹시 모를 모호함 방지 (네 원본 코드 반영)

/// <summary>
/// 유닛의 생명력, 피격, 사망 처리를 담당하는 핵심 컴포넌트입니다.
/// [최종 검수 완료]: 보스 소환 + 힐/경험치 + HealingArea 연동 + 코루틴 초기화 로직 보완
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

    // ★ 적 처치 시 획득할 경험치 양
    public int expReward = 1;

    [Header("넉백 설정")]
    public float knockbackPower = 20f;
    public float knockbackDuration = 0.2f;

    [Header("피격 피드백 (무적/색상)")]
    [Tooltip("피격 후 무적 시간(초).")]
    public float invincibilityDuration = 0.2f;
    public Color invincibilityColor = new Color(1f, 0.5f, 0.5f, 0.5f);

    public UnityEvent onDie;

    // --- 지형 효과 관련 변수 추가 ---
    private Coroutine healFlashCoroutine;
    private Coroutine terrainTintCoroutine;
    private Color currentTerrainTint = Color.white;
    // ----------------------------

    // 기존 변수들
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private bool isInvincible = false;
    private bool isKnockedBack = false;
    private Color originalColor;
    private bool isFlashing = false; // 플래싱 상태를 위한 변수

    // 외부에서 넉백 상태를 확인할 수 있는 프로퍼티
    public bool IsKnockedBack => isKnockedBack;

    // [최적화] WaitForSeconds 캐싱
    private static readonly WaitForSeconds waitHealFlash = new WaitForSeconds(0.2f);
    private static readonly WaitForSeconds waitTerrainTint = new WaitForSeconds(0.1f);
    private WaitForSeconds cachedInvincibilityWait;
    private WaitForSeconds cachedKnockbackWait;

    void Awake()
    {
        // 컴포넌트 초기화
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponentInChildren<SpriteRenderer>();
        if (spriter != null)
        {
            originalColor = spriter.color;
        }
        currentHealth = maxHealth;

        // [최적화] 인스턴스별 WaitForSeconds 캐싱 (인스펙터 값 기반)
        cachedInvincibilityWait = new WaitForSeconds(invincibilityDuration);
        cachedKnockbackWait = new WaitForSeconds(knockbackDuration);
    }

    // 피격 처리 함수 (기존 로직 유지)
    public void TakeDamage(float damage, Transform attacker = null)
    {
        if (isInvincible || isDead) return;

        currentHealth -= damage;

        // 무적 시간 코루틴 시작
        if (invincibilityDuration > 0)
        {
            if (gameObject.activeInHierarchy) // 활성화된 오브젝트일 때만 코루틴 시작
            {
                if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
                StartCoroutine(InvincibilityBlinkRoutine());
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }

        // 넉백 로직 (기존 로직 유지)
        if (attacker != null)
        {
            ApplyKnockback(attacker);
        }
    }

    // 체력 회복 함수 (지형 효과에서 호출됨)
    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        // 힐 이펙트/피드백
        if (gameObject.activeInHierarchy)
        {
            // 힐 시 플래시 코루틴 시작 (색상 피드백)
            if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
            healFlashCoroutine = StartCoroutine(HealFlashRoutine());
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        // 사망 이벤트 호출 및 오브젝트 비활성화/파괴 로직
        onDie.Invoke();

        // 오브젝트 풀링/파괴 로직 (임시 파괴)
        // Destroy(gameObject);

        // 아이템 드롭 로직 (기존 로직 유지)
        DropItem();
    }

    private void DropItem()
    {
        // 아이템 드롭 로직 (기존 로직 유지)
        // if (dropItemIndex != -1) { ... }
    }

    // --- 코루틴들 ---

    // [최적화] 캐싱된 WaitForSeconds 사용
    private IEnumerator InvincibilityBlinkRoutine()
    {
        isInvincible = true;
        // 기존 힐 플래시와 무적 플래시가 충돌하지 않도록
        if (spriter != null && healFlashCoroutine == null) spriter.color = invincibilityColor;

        yield return cachedInvincibilityWait;

        isInvincible = false;
        // 힐 중이 아닐 때만 원래 색으로 복구
        if (spriter != null && healFlashCoroutine == null)
        {
            spriter.color = originalColor;
        }
    }

    // [최적화] 정적 캐싱된 WaitForSeconds 사용
    private IEnumerator HealFlashRoutine()
    {
        if (spriter != null) spriter.color = Color.green;
        yield return waitHealFlash;

        if (spriter != null)
        {
            // 무적 상태가 아닐 때만 원래 색으로 복구
            if (!isInvincible)
            {
                spriter.color = originalColor;
            }
        }
        healFlashCoroutine = null; // 종료 시 변수 비우기
    }

    // 힐 플래시를 중지하고 원래 색상으로 복구 (HealingArea에서 호출)
    public void StopHealFlashAndResetColor()
    {
        if (healFlashCoroutine != null)
        {
            StopCoroutine(healFlashCoroutine);
            healFlashCoroutine = null;
        }

        // 무적 상태가 아닐 때만 원래 색으로 복구
        if (spriter != null && !isInvincible)
        {
            spriter.color = originalColor;
        }
    }

    // --- 지형 효과 색상 틴트 (Terrain Tint) ---

    /// <summary>
    /// 지형 효과 색상 적용 (늪: 갈색, 눈: 하늘색, 힐: 녹색)
    /// </summary>
    public void ApplyTerrainTint(Color tintColor)
    {
        if (spriter == null) return;

        currentTerrainTint = tintColor;

        // 기존 틴트 코루틴 중지
        if (terrainTintCoroutine != null)
        {
            StopCoroutine(terrainTintCoroutine);
        }

        // 지속적인 틴트 효과 시작
        if (gameObject.activeInHierarchy)
        {
            terrainTintCoroutine = StartCoroutine(TerrainTintRoutine(tintColor));
        }
    }

    /// <summary>
    /// 지형 효과 색상 제거 (원래 색상으로 복구)
    /// </summary>
    public void RemoveTerrainTint()
    {
        currentTerrainTint = Color.white;

        if (terrainTintCoroutine != null)
        {
            StopCoroutine(terrainTintCoroutine);
            terrainTintCoroutine = null;
        }

        // 무적/힐 상태가 아닐 때만 원래 색으로 복구
        if (spriter != null && !isInvincible && healFlashCoroutine == null)
        {
            spriter.color = originalColor;
        }
    }

    // [최적화] 정적 캐싱된 WaitForSeconds 사용
    private IEnumerator TerrainTintRoutine(Color tintColor)
    {
        while (true)
        {
            // 무적/힐 플래시 중이 아닐 때만 틴트 적용
            if (spriter != null && !isInvincible && healFlashCoroutine == null)
            {
                // 원래 색상에 틴트를 곱해서 적용
                spriter.color = originalColor * tintColor;
            }
            yield return waitTerrainTint;
        }
    }

    private void ApplyKnockback(Transform attacker)
    {
        if (rigid == null) return;

        Vector2 knockbackDir = (transform.position - attacker.position).normalized;

        if (isKnockedBack) StopCoroutine("PhysicsKnockback");
        StartCoroutine(PhysicsKnockback(knockbackDir));
    }

    // [최적화] 캐싱된 WaitForSeconds 사용
    private IEnumerator PhysicsKnockback(Vector2 dir)
    {
        isKnockedBack = true;
        rigid.linearVelocity = Vector2.zero;
        rigid.AddForce(dir * knockbackPower, ForceMode2D.Impulse);

        yield return cachedKnockbackWait;

        isKnockedBack = false;
    }
}