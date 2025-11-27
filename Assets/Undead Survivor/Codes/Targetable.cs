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

    // A 값을 0.5 정도로 설정하면 반투명해짐
    public Color invincibilityColor = new Color(1f, 0.5f, 0.5f, 0.5f);

    public UnityEvent onDie;

    // 내부 변수
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private PoolManager poolManager;
    private bool isInvincible = false;
    private Color originalColor;
    private bool isKnockedBack = false;

    // ★ 추가: 내가 타워인지 확인하기 위한 변수
    private SpawnPoint mySpawnPoint;

    // 외부에서 넉백 상태 확인용 프로퍼티
    public bool IsKnockedBack => isKnockedBack;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();

        // ★ [핵심] 시작할 때 나한테 SpawnPoint가 붙어있는지 확인 (있으면 타워, 없으면 몬스터)
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

        // 다시 활성화될 때 컴포넌트들도 복구 (타워 재사용 대비)
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

        // 킬 수 증가
        if (GameManager.instance != null && faction == Faction.Enemy)
        {
            GameManager.instance.AddKill();
        }

        onDie.Invoke();

        // ★ [핵심 수정 부분] 타워와 일반 유닛 구분
        if (mySpawnPoint != null)
        {
            Debug.Log("📢 타워 사망! SpawnPoint에게 파괴 명령 보냄!");
            // Case A: 나는 타워다 (SpawnPoint 컴포넌트가 있음)
            // -> 오브젝트를 끄지 않고, 파괴 애니메이션 로직을 실행
            mySpawnPoint.DeactivatePermanently();

            if (rigid)
            {
                rigid.linearVelocity = Vector2.zero; // 움직임 멈춤
                rigid.bodyType = RigidbodyType2D.Static; // ★ 완전 고정된 벽으로 변경
            }
            // 이 스크립트 끄기 (더 이상 로직 안 돌게)
            this.enabled = false;
        }
        else
        {
            Debug.Log("👻 일반 몬스터 사망! 사라짐.");
            // Case B: 나는 일반 몬스터다 (SpawnPoint 컴포넌트가 없음)
            // -> 깔끔하게 비활성화 (오브젝트 풀링 혹은 삭제)
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

    // --- 피격 효과 관련 코루틴 ---

    private IEnumerator InvincibilityBlinkRoutine()
    {
        isInvincible = true;

        if (spriter != null)
        {
            spriter.color = invincibilityColor;
        }

        yield return new WaitForSeconds(invincibilityDuration);

        isInvincible = false;
        if (spriter != null)
        {
            spriter.color = originalColor;
        }
    }

    private void ApplyKnockback(Transform attacker)
    {
        if (rigid == null) return;

        // 공격자 반대 방향 계산
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