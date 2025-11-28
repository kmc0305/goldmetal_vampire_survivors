using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Vector2 = UnityEngine.Vector2; // ✅ 혹시 모를 모호함 방지 (네 원본 코드 반영)

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

    // 내부 변수
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private PoolManager poolManager;
    private bool isInvincible = false;
    private Color originalColor;
    private bool isKnockedBack = false;

    // ★ 타워 여부 확인용
    private SpawnPoint mySpawnPoint;

    // ★ 힐 효과 코루틴
    private Coroutine healFlashCoroutine;

    // 외부에서 넉백 상태 확인용 프로퍼티
    public bool IsKnockedBack => isKnockedBack;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();

        // 나한테 SpawnPoint가 붙어있는지 확인 (있으면 타워)
        mySpawnPoint = GetComponent<SpawnPoint>();

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

        // 코루틴 변수 초기화 (중요)
        healFlashCoroutine = null;

        if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = true;
        this.enabled = true;
        if (rigid != null) rigid.simulated = true;

        if (spriter != null)
        {
            spriter.color = originalColor;
        }
        if (rigid != null)
        {
            rigid.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 외부에서 호출하여 데미지를 주는 함수
    /// </summary>
    public void TakeDamage(float damage, Transform attacker)
    {
        if (isDead || isInvincible) return;

        currentHealth -= damage;

        if (attacker != null)
        {
            ApplyKnockback(attacker);
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(InvincibilityBlinkRoutine());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // ★ [힐 함수]
    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        // 힐 이펙트 (초록색 깜빡임)
        if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
        healFlashCoroutine = StartCoroutine(HealFlashRoutine());
    }

    // ★ [추가] HealingArea.cs에서 호출하는 함수
    public void StopHealFlashAndResetColor()
    {
        if (healFlashCoroutine != null)
        {
            StopCoroutine(healFlashCoroutine);
            healFlashCoroutine = null; // ★ [중요] 변수를 비워줘야 다른 색상 로직이 꼬이지 않음
        }

        if (spriter != null) spriter.color = originalColor;
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        

        // 킬 수 증가 및 경험치 획득
        if (GameManager.instance != null && faction == Faction.Enemy)
        {
            GameManager.instance.AddKill(); // 팀원 기능 (킬 카운트)

            for (int i = 0; i < expReward; i++) // 네 기능 (경험치)
            {
                GameManager.instance.getExp();
            }
        }

        onDie.Invoke();

        // ★ 타워와 일반 유닛 구분
        if (mySpawnPoint != null)
        {
            Debug.Log("📢 타워 사망! SpawnPoint에게 파괴 명령 보냄!");
            mySpawnPoint.DeactivatePermanently(); // 보스 소환

            if (rigid)
            {
                rigid.linearVelocity = Vector2.zero;
                rigid.bodyType = RigidbodyType2D.Static;
            }
            this.enabled = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
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

    // --- 코루틴들 ---

    private IEnumerator InvincibilityBlinkRoutine()
    {
        isInvincible = true;
        if (spriter != null) spriter.color = invincibilityColor;

        yield return new WaitForSeconds(invincibilityDuration);

        isInvincible = false;
        // 힐 중이 아닐 때만 원래 색으로 복구 (충돌 방지 로직)
        if (spriter != null && healFlashCoroutine == null)
        {
            spriter.color = originalColor;
        }
    }

    private IEnumerator HealFlashRoutine()
    {
        if (spriter != null) spriter.color = Color.green;
        yield return new WaitForSeconds(0.2f);

        if (spriter != null) spriter.color = originalColor;
        healFlashCoroutine = null; // 종료 시 변수 비우기
    }

    private void ApplyKnockback(Transform attacker)
    {
        if (rigid == null) return;

        Vector2 knockbackDir = (transform.position - attacker.position).normalized;

        if (isKnockedBack) StopCoroutine("PhysicsKnockback");
        StartCoroutine(PhysicsKnockback(knockbackDir));
    }

    private IEnumerator PhysicsKnockback(Vector2 dir)
    {
        isKnockedBack = true;
        rigid.linearVelocity = Vector2.zero;
        rigid.AddForce(dir * knockbackPower, ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);

        rigid.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }
}