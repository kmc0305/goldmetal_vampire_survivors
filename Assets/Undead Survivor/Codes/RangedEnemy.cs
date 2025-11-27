using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random; // 모호함 방지
using Vector2 = UnityEngine.Vector2; // ✅ 모호함 방지 명시
using Vector3 = UnityEngine.Vector3; // ✅ 모호함 방지 명시

/// <summary>
/// 원거리 공격 적 유닛입니다.
/// [최적화 적용됨]: 코루틴 초기 지연(Load Balancing) 및 갱신 주기 랜덤화(Jittering)
/// [수정]: 클래스 이름 중복 오류 해결 (Player -> RangedEnemy)
/// </summary>
public class RangedEnemy : MonoBehaviour
{
    [Header("무기 데이터")]
    public EnemyWeaponData weaponData;
    public int bulletPrefabId;

    [Header("기본 설정")]
    public float speed = 2.0f;
    public LayerMask targetLayer;
    public float detectionRadius = 15f;

    // ✅ [최적화] AI 업데이트 주기 및 랜덤 지연 시간
    public float aiUpdateFrequency = 0.5f;
    private float aiUpdateRandomDelay = 0.1f;

    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private Targetable currentTarget;
    private float lastAttackTime;
    private Targetable myTargetable; // 내 자신의 Targetable

    private Coroutine aiCoroutine;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        myTargetable = GetComponent<Targetable>();
    }

    void OnEnable()
    {
        currentTarget = null;
        // ✅ [최적화] 유닛 생성 시 타겟 갱신 코루틴을 랜덤 지연 후 시작
        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
    }

    void OnDisable()
    {
        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }
        currentTarget = null;
        rigid.linearVelocity = Vector2.zero;
    }

    // ✅ [최적화] 초기 지연 코루틴 (프레임 스파이크 방지)
    IEnumerator UpdateTargetCoroutineDelayed()
    {
        float initialDelay = Random.Range(0f, aiUpdateFrequency);
        yield return new WaitForSeconds(initialDelay);
        StartCoroutine(UpdateTargetCoroutine());
    }

    // ✅ [최적화] 주기적 타겟 탐색 (Jitter 적용)
    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            // 타겟 유효성 검사
            if (currentTarget != null && currentTarget.isDead)
            {
                currentTarget = null;
            }

            // 타겟이 없으면 탐색
            if (currentTarget == null)
            {
                currentTarget = FindClosestTarget();
            }

            // 다음 갱신까지 대기 (주기 + 랜덤 오차)
            float waitTime = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (waitTime < 0.1f) waitTime = 0.1f;

            yield return new WaitForSeconds(waitTime);
        }
    }

    // ... FindClosestTarget 함수는 기존과 동일 ...
    Targetable FindClosestTarget()
    {
        float closestDist = float.MaxValue;
        Targetable bestTarget = null;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);

        foreach (Collider2D col in hits)
        {
            Targetable t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead) // 죽지 않은 유닛만 타겟으로 간주
            {
                float dist = Vector2.Distance(transform.position, col.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = t;
                }
            }
        }
        return bestTarget;
    }

    // ✅ [최적화] 이동 로직을 FixedUpdate에서 MovePosition으로 처리
    void FixedUpdate()
    {
        // 넉백 중이면 이동 안함
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

        // 타겟이 없으면 정지
        if (currentTarget == null)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, currentTarget.transform.position);

        // 공격 사거리 안에 들어오면 정지
        if (weaponData != null && dist <= weaponData.attackRange)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        // 추적 이동 (공격 범위 밖일 경우)
        Vector2 dir = (currentTarget.transform.position - transform.position).normalized;
        Vector2 step = dir * speed * Time.fixedDeltaTime;
        rigid.MovePosition(rigid.position + step);
    }

    void Update()
    {
        // 넉백 중이면 회전/발사 안 함
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null) return;
        if (weaponData == null) return;

        // 스프라이트 방향 전환
        spriter.flipX = currentTarget.transform.position.x < transform.position.x;

        // 공격 로직
        float dist = Vector2.Distance(transform.position, currentTarget.transform.position);

        if (dist <= weaponData.attackRange)
        {
            if (Time.time >= lastAttackTime + weaponData.cooldown)
            {
                Fire();
                lastAttackTime = Time.time;
            }
        }
    }

    void Fire()
    {
        GameObject bulletObj = GameManager.instance.Pool.Get(bulletPrefabId);
        if (bulletObj == null) return;

        bulletObj.transform.position = transform.position;

        Vector3 dir = (currentTarget.transform.position - transform.position).normalized;

        EnemyBullet bulletScript = bulletObj.GetComponent<EnemyBullet>();
        if (bulletScript != null)
        {
            bulletScript.Init(weaponData, dir);
        }
    }

    // [디버그용]
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        if (weaponData != null)
        {
            Gizmos.color = Color.red;
            // weaponData.attackRange가 있다고 가정하고 그립니다.
            Gizmos.DrawWireSphere(transform.position, weaponData.attackRange);
        }
    }
}