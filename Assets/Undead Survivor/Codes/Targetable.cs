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
        if (rigid != null) rigid.linearVelocity = Vector2.zero; // Unity 6에서는 linearVelocity로 자동 변환됨
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
        if (isDead) return;
        isDead = true;

        // 적 처치 시 보상
        if (GameManager.instance != null && faction == Faction.Enemy)
        {
            GameManager.instance.AddKill();
            for (int i = 0; i < expReward; i++) GameManager.instance.getExp();
        }

        onDie.Invoke();

        // 1. 플레이어 사망 (게임 오버)
        if (faction == Faction.Player)
        {
            Debug.Log("📢 플레이어 사망! 게임 오버!");

            // Player 스크립트에게 UI 띄우라고 명령
            Player playerScript = GetComponent<Player>();
            if (playerScript != null)
            {
                playerScript.TriggerGameOver();
            }
            // 플레이어 오브젝트는 끄지 않음 (UI 보여야 함)
            return;
        }

        // 2. 타워 사망
        if (mySpawnPoint != null)
        {
            Debug.Log("📢 타워 파괴됨!");
            mySpawnPoint.DeactivatePermanently();
            if (rigid)
            {
                rigid.linearVelocity = Vector2.zero;
                rigid.bodyType = RigidbodyType2D.Static;
            }
            this.enabled = false;
        }
        // 3. 일반 적 사망
        else
        {
            Enemy enemyScript = GetComponent<Enemy>();
            if (enemyScript != null)
            {
                enemyScript.OnEnemyDead();
            }
            else
            {
                gameObject.SetActive(false);
            }
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