using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 유닛의 생명력, 피격, 사망 처리를 담당하는 핵심 컴포넌트
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
    public int expReward = 1;

    [Header("넉백 설정")]
    public float knockbackPower = 20f;
    public float knockbackDuration = 0.2f;

    [Header("피격 피드백")]
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

    // 타워 여부 확인용
    private SpawnPoint mySpawnPoint;

    // 힐 효과 코루틴
    private Coroutine healFlashCoroutine;

    // 지형 효과 관련 변수
    private Coroutine terrainTintCoroutine;
    private Color currentTerrainTint = Color.white;
    private bool isInTerrainEffect = false;

    public bool IsKnockedBack => isKnockedBack;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
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
        healFlashCoroutine = null;

        if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = true;
        this.enabled = true;
        if (rigid != null) rigid.simulated = true;

        if (spriter != null) spriter.color = originalColor;
        if (rigid != null) rigid.linearVelocity = UnityEngine.Vector2.zero; // Unity 6에서는 linearVelocity로 자동 변환됨

        // ★ [추가] 재활용 시 자식 오브젝트도 활성화 (AllyAI.cs에서 이 로직을 처리하는 경우 주석 처리 가능)
        // SetChildrenActive(true);
    }

    /// <summary>
    /// 외부에서 호출하여 데미지를 주는 함수
    /// </summary>
    public void TakeDamage(float damage, Transform attacker)
    {
        // 이미 죽었거나 무적 상태면 무시
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
            currentHealth = 0; // 체력이 0 이하로 내려가지 않도록 고정
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        if (healFlashCoroutine != null) StopCoroutine(healFlashCoroutine);
        healFlashCoroutine = StartCoroutine(HealFlashRoutine());
    }

    public void StopHealFlashAndResetColor()
    {
        if (healFlashCoroutine != null)
        {
            StopCoroutine(healFlashCoroutine);
            healFlashCoroutine = null;
        }

        if (spriter != null) spriter.color = originalColor;
    }

    /// <summary>
    /// 사망 처리 로직 (플레이어/적/타워 구분)
    /// </summary>
    public void Die()
    {
        if (isDead) return; // 중복 사망 처리 방지
        isDead = true;

        // 물리 시뮬레이션 및 충돌 즉시 중지
        if (rigid != null)
        {
            rigid.linearVelocity = UnityEngine.Vector2.zero;
            rigid.simulated = false; // 물리 엔진에서 제외 (충돌/움직임 즉시 정지)
        }
        if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = false; // 충돌체 비활성화

        // ★ [핵심 수정] 풀로 반환하기 전에 자식 오브젝트들을 모두 비활성화하여 정리
        SetChildrenActive(false);

        // 사망 이벤트 호출
        onDie.Invoke();

        // 1. 플레이어 사망 (게임 오버)
        if (faction == Faction.Player)
        {
            UnityEngine.Debug.Log("📢 플레이어 사망! 게임 오버!"); // Debug 명시적 사용

            Player playerScript = GetComponent<Player>();
            if (playerScript != null)
            {
                playerScript.TriggerGameOver();
            }

            // 시각적 요소 정리 (플레이어 스프라이트만)
            if (spriter != null) spriter.enabled = false;

            return;
        }

        // 2. 타워 사망
        if (mySpawnPoint != null)
        {
            UnityEngine.Debug.Log("📢 타워 파괴됨!"); // Debug 명시적 사용
            mySpawnPoint.DeactivatePermanently();

            // PoolManager를 통해 풀로 반환
            if (poolManager != null)
            {
                poolManager.Return(gameObject);
            }
            else
            {
                // PoolManager가 없으면 일반 비활성화
                gameObject.SetActive(false);
            }
        }
        // 3. 일반 적 및 기타 유닛 사망
        else
        {
            // 적은 Enemy.cs의 OnEnemyDead()에서 지연 비활성화를 처리합니다.
            Enemy enemyScript = GetComponent<Enemy>();
            if (enemyScript != null)
            {
                enemyScript.OnEnemyDead();
            }
            else
            {
                // Enemy 스크립트가 없는 일반 Targetable 유닛 (아군 소환수 등)은 풀로 즉시 반환
                // PoolManager를 통해 풀로 반환
                if (poolManager != null)
                {
                    poolManager.Return(gameObject);
                }
                else
                {
                    // PoolManager가 없으면 일반 비활성화
                    gameObject.SetActive(false);
                }
            }
        }

        // 적 처치 시 보상 (Enemy faction에서만 실행됨)
        if (GameManager.instance != null && faction == Faction.Enemy)
        {
            GameManager.instance.AddKill();
            for (int i = 0; i < expReward; i++) GameManager.instance.getExp();
        }
    }

    // ★ [추가] 모든 자식 오브젝트의 활성화 상태를 제어하는 헬퍼 함수
    // 유닛 오브젝트가 비활성화될 때 자식 오브젝트(MiniMap Icon, Shadow 등)를 정리하는 데 사용됩니다.
    private void SetChildrenActive(bool state)
    {
        // 유닛의 자식 오브젝트들을 순회하며 활성화/비활성화합니다.
        foreach (Transform child in transform)
        {
            // SetActive()를 사용하여 자식 오브젝트를 명시적으로 정리합니다.
            child.gameObject.SetActive(state);
        }
    }


    void DropItem()
    {
        if (poolManager == null || dropItemIndex < 0) return;

        GameObject item = poolManager.Get(dropItemIndex);
        if (item != null)
        {
            item.transform.position = transform.position;
        }
    }

    // --- 코루틴 및 이펙트 처리 ---

    private IEnumerator InvincibilityBlinkRoutine()
    {
        isInvincible = true;
        if (spriter != null) spriter.color = invincibilityColor;

        yield return new WaitForSeconds(invincibilityDuration);

        isInvincible = false;
        // 힐 중이나 지형 효과 중이면 색상 유지
        if (spriter != null && healFlashCoroutine == null)
        {
            spriter.color = isInTerrainEffect ? currentTerrainTint : originalColor;
        }
    }

    private IEnumerator HealFlashRoutine()
    {
        if (spriter != null) spriter.color = Color.green;
        yield return new WaitForSeconds(0.2f);

        if (spriter != null) spriter.color = isInTerrainEffect ? currentTerrainTint : originalColor;
        healFlashCoroutine = null;
    }

    private void ApplyKnockback(Transform attacker)
    {
        if (rigid == null) return;

        UnityEngine.Vector2 knockbackDir = (transform.position - attacker.position).normalized;

        if (isKnockedBack) StopCoroutine("PhysicsKnockback");
        StartCoroutine(PhysicsKnockback(knockbackDir));
    }

    private IEnumerator PhysicsKnockback(UnityEngine.Vector2 dir)
    {
        isKnockedBack = true;
        rigid.linearVelocity = UnityEngine.Vector2.zero;
        rigid.AddForce(dir * knockbackPower, ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);

        rigid.linearVelocity = UnityEngine.Vector2.zero;
        isKnockedBack = false;
    }

    // --- 지형 효과 틴트 ---

    public void ApplyTerrainTint(Color tintColor)
    {
        if (isDead || spriter == null) return;

        isInTerrainEffect = true;
        currentTerrainTint = tintColor;

        if (terrainTintCoroutine != null) StopCoroutine(terrainTintCoroutine);

        if (!isInvincible && healFlashCoroutine == null)
        {
            spriter.color = tintColor;
        }
    }

    public void RemoveTerrainTint()
    {
        isInTerrainEffect = false;
        currentTerrainTint = Color.white;

        if (terrainTintCoroutine != null)
        {
            StopCoroutine(terrainTintCoroutine);
            terrainTintCoroutine = null;
        }

        if (spriter != null && !isInvincible && healFlashCoroutine == null)
        {
            spriter.color = originalColor;
        }
    }
}