using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [생명 관리] 유닛의 체력, 데미지 피격, 죽음 등을 관리하는 핵심 컴포넌트입니다.
/// </summary>
public class Targetable : MonoBehaviour
{
    // === 팩션(진영) 정의 ===
    public enum Faction
    {
        Player, // 플레이어 및 아군
        Enemy,  // 적
        Neutral // 중립
    }
    public Faction faction;

    [Header("능력치")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isDead => currentHealth <= 0;

    [Header("넉백 설정")]
    public float knockBackPower = 5f;    // 넉백 힘 (Enemy.cs에서 사용)
    public float knockBackDuration = 0.2f; // 넉백 지속 시간 (Enemy.cs에서 사용)
    public bool isKnockbackable = true;
    private bool _isKnockedBack = false;

    /// <summary>
    /// 현재 넉백 상태인지 여부. UnitMover2D, Enemy.cs, AllyAI.cs와 공유됩니다.
    /// </summary>
    public bool IsKnockedBack
    {
        get { return _isKnockedBack; }
        set
        {
            _isKnockedBack = value;
            // 넉백 시작/종료 시 필요한 추가적인 상태 변경 로직이 여기에 들어갈 수 있습니다.
        }
    }

    // === 힐/피격 관련 필드 ===
    private SpriteRenderer sr;
    private Color originalColor;
    private Coroutine healFlashCoroutine;
    private Coroutine hitFlashCoroutine; // 피격 코루틴 참조

    void Awake()
    {
        // Awake에서 SpriteRenderer를 먼저 찾아 originalColor를 저장
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            originalColor = sr.color;
        }
        currentHealth = maxHealth;
    }

    void OnEnable()
    {
        currentHealth = maxHealth;
        isKnockbackable = true; // 기본값
        IsKnockedBack = false; // 상태 초기화

        // 오브젝트 풀링(Pooling) 시 색상 초기화 보장
        if (sr != null)
        {
            sr.color = originalColor;
        }
    }


    // === 피격 및 데미지 처리 ===
    /// <summary>
    /// 대상이 데미지를 입었을 때 호출되며, 넉백 로직과 피격 이펙트를 호출합니다.
    /// </summary>
    public void TakeDamage(float damage, Transform hitSource)
    {
        if (isDead) return;

        currentHealth -= damage;

        // 복구된 기능: 피격 시 깜빡이는 이펙트 호출
        if (sr != null)
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        // 넉백 처리 (넉백이 가능한 대상일 경우)
        if (isKnockbackable)
        {
            // Vector2를 UnityEngine.Vector2로 명시적 지정하여 모호성 해결
            UnityEngine.Vector2 knockbackDir = (transform.position - hitSource.position).normalized;

            // Enemy/AllyAI 스크립트가 붙어있는 경우에만 넉백 코루틴 실행
            Enemy enemy = GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.ApplyKnockback(knockbackDir, knockBackPower, knockBackDuration);
            }

            AllyAI ally = GetComponent<AllyAI>();
            if (ally != null)
            {
                ally.ApplyKnockback(knockbackDir, knockBackPower, knockBackDuration);
            }
        }

        if (isDead)
        {
            Die(hitSource);
        }
    }

    /// <summary>
    /// 체력 회복 시 짧게 깜빡이는 코루틴입니다.
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead) return;

        // 실제로 회복될 체력이 있을 때만 실행
        if (currentHealth < maxHealth)
        {
            // 현재 체력을 최대 체력을 넘지 않도록 회복합니다.
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

            // 힐 이펙트 추가: 짧게 초록색으로 깜빡입니다.
            if (sr != null)
            {
                // 기존 힐 플래시 코루틴이 실행 중이면 중지 (새로운 플래시가 시작되므로)
                if (healFlashCoroutine != null)
                {
                    StopCoroutine(healFlashCoroutine);
                }
                healFlashCoroutine = StartCoroutine(HealFlashRoutine());
            }
        }

        // GameManager의 Health HUD를 직접 업데이트하는 경우, 여기서 호출할 수 있습니다.
        if (faction == Faction.Player && GameManager.instance != null)
        {
            // GameManager의 int health를 업데이트해야 한다면
            GameManager.instance.health = Mathf.FloorToInt(currentHealth);
        }
    }

    /// <summary>
    /// [★ 신규 추가] 힐 장판을 벗어날 때 힐 플래시를 강제로 중지하고 색상을 복구합니다.
    /// </summary>
    public void StopHealFlashAndResetColor()
    {
        if (healFlashCoroutine != null)
        {
            StopCoroutine(healFlashCoroutine);
            healFlashCoroutine = null;
        }

        // 피격 중이 아니라면 색상 복구 (피격 중이면 빨간색을 유지해야 함)
        if (hitFlashCoroutine == null && sr != null)
        {
            sr.color = originalColor;
        }
    }

    /// <summary>
    /// 체력 회복 시 짧게 초록색으로 깜빡이는 코루틴입니다.
    /// </summary>
    private IEnumerator HealFlashRoutine()
    {
        float duration = 0.15f; // 0.15초 동안 깜빡임
        Color healColor = Color.green; // 힐 색상: 녹색

        // 회복 시작 시 즉시 색상 변경
        if (sr != null) sr.color = healColor;

        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(duration);

        // 코루틴이 자연 종료될 때, 다른 플래시가 없는 경우에만 원래 색상으로 복구
        if (hitFlashCoroutine == null && sr != null)
        {
            sr.color = originalColor;
        }

        healFlashCoroutine = null;
    }

    /// <summary>
    /// 추가된 기능: 피격 시 짧게 빨간색으로 깜빡이는 코루틴입니다.
    /// </summary>
    private IEnumerator HitFlashRoutine()
    {
        float duration = 0.1f; // 0.1초 동안 깜빡임
        Color hitColor = Color.red; // 피격 색상: 빨간색

        // 피격 시 즉시 색상 변경
        if (sr != null) sr.color = hitColor;

        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(duration);

        // 원래 색상으로 복구
        // 힐 중이 아니라면 색상 복구 (힐 중이면 초록색을 유지해야 함)
        if (healFlashCoroutine == null && sr != null)
        {
            sr.color = originalColor;
        }

        hitFlashCoroutine = null;
    }


    // === 유닛 사망 처리 ===
    void Die(Transform killer)
    {
        // (여기에 사망 애니메이션, 경험치 드랍 등 로직 추가)
        gameObject.SetActive(false);

        // 복구된 기능: 적이 죽었을 때 플레이어에게 경험치(EXP) 부여
        if (faction == Faction.Enemy && GameManager.instance != null)
        {
            GameManager.instance.getExp();
        }

    }
}