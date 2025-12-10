using System.Collections;
using System.Collections.Generic; // List 사용을 위해 추가
using UnityEngine;
using Random = UnityEngine.Random;

public class RangedEnemy : MonoBehaviour
{
    [Header("무기 데이터")]
    public EnemyWeaponData weaponData;
    public int bulletPrefabId;

    [Header("기본 설정")]
    public float speed = 2.0f;
    public float speedMultiplier = 1f;

    public LayerMask targetLayer;
    public float detectionRadius = 15f;

    // [추가] 성 우선 공격 거리 (이 거리 안에 성이 있으면 성을 침)
    public float castlePriorityRadius = 15.0f;

    [Header("AI 업데이트")]
    public float aiUpdateFrequency = 0.5f;
    private float aiUpdateRandomDelay = 0.1f;

    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private Targetable currentTarget;
    private Targetable myTargetable;
    private float lastAttackTime;

    private Coroutine aiCoroutine;
    private Animator anim;

    // [추가] 성(Castle) 참조 캐싱
    private Targetable cachedCastle;

    // [최적화] Physics2D 버퍼 (메모리 낭비 방지)
    private Collider2D[] targetBuffer = new Collider2D[20];

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        myTargetable = GetComponent<Targetable>();
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        // [추가] 시작할 때 성(Castle)을 미리 찾아둠
        GameObject castleObj = GameObject.FindGameObjectWithTag("Castle");
        if (castleObj != null)
            cachedCastle = castleObj.GetComponent<Targetable>();
    }

    void OnEnable()
    {
        currentTarget = null;
        speedMultiplier = 1f;

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

    IEnumerator UpdateTargetCoroutineDelayed()
    {
        float initialDelay = Random.Range(0f, aiUpdateFrequency);
        yield return new WaitForSeconds(initialDelay);
        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            // ★ [핵심 수정] 타겟이 있든 없든, 주기적으로 가장 좋은 타겟을 다시 찾음 (갈아타기 가능)
            currentTarget = FindClosestTarget();

            float wait = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (wait < 0.1f) wait = 0.1f;

            yield return new WaitForSeconds(wait);
        }
    }

    Targetable FindClosestTarget()
    {
        Targetable bestTarget = null;
        float closestDist = float.MaxValue;

        // 1. 주변 유닛 탐색 (OverlapCircleNonAlloc 사용으로 최적화)
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectionRadius, targetBuffer, targetLayer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = targetBuffer[i];

            // 성은 따로 계산하므로 패스
            if (col.CompareTag("Castle")) continue;

            Targetable t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                float d = Vector2.Distance(transform.position, t.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    bestTarget = t;
                }
            }
        }

        // 2. 성(Castle) 우선순위 로직 적용
        bool castleAlive = (cachedCastle != null && !cachedCastle.isDead);
        float distToCastle = float.MaxValue;

        if (castleAlive)
        {
            distToCastle = Vector2.Distance(transform.position, cachedCastle.transform.position);
        }

        // [우선순위 판단]
        // 조건 A: 내 바로 근처(5m 이내)에 적 유닛이 있으면 걔부터 쏨 (자기 방어)
        if (bestTarget != null && closestDist < 5.0f)
        {
            return bestTarget;
        }

        // 조건 B: 성이 우선순위 반경(15m) 안에 있으면 성을 쏨
        if (castleAlive && distToCastle <= castlePriorityRadius)
        {
            return cachedCastle;
        }

        // 조건 C: 성보다 유닛이 더 가까우면 유닛을 쏨
        if (bestTarget != null && closestDist < distToCastle)
        {
            return bestTarget;
        }

        // 조건 D: 다 아니면 기본적으로 성을 공격
        if (castleAlive) return cachedCastle;

        return bestTarget;
    }

    void FixedUpdate()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

        if (currentTarget == null)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

        // 공격 사거리 안 -> 멈춰서 공격
        if (weaponData != null && dist <= weaponData.attackRange)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        // 공격 사거리 밖 -> 이동
        Vector2 dir = (currentTarget.transform.position - transform.position).normalized;
        float currentMoveSpeed = speed * speedMultiplier;
        Vector2 step = dir * currentMoveSpeed * Time.fixedDeltaTime;

        rigid.MovePosition(rigid.position + step);
    }

    void Update()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null || weaponData == null) return;

        spriter.flipX = currentTarget.transform.position.x < transform.position.x;

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

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
        if (anim != null) anim.SetTrigger("Attack");

        GameObject bulletObj = GameManager.instance.Pool.Get(bulletPrefabId);
        if (bulletObj == null) return;

        bulletObj.transform.position = transform.position;

        // 정확도 향상을 위해 발사 순간 타겟 방향 다시 계산
        Vector3 dir = (currentTarget.transform.position - transform.position).normalized;

        EnemyBullet b = bulletObj.GetComponent<EnemyBullet>();
        if (b != null)
        {
            b.Init(weaponData, dir);
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    void OnDrawGizmosSelected()
    {
        // 탐지 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 공격 사거리
        if (weaponData != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, weaponData.attackRange);
        }

        // 현재 타겟 연결선
        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}