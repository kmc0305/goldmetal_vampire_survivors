using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

public class Enemy : MonoBehaviour
{
    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float attackDamage = 1f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    [Header("AI 설정")]
    public LayerMask targetLayer;
    public float detectionRadius = 100f;
    public float aiUpdateFrequency = 0.5f;

    // 성 우선 공격 범위 (노란 원)
    public float castlePriorityRadius = 15.0f;

    private float aiUpdateRandomDelay = 0.1f;

    // 물리 연산용 버퍼
    private Collider2D[] targetBuffer = new Collider2D[30]; // 버퍼 크기도 조금 늘림
    private ContactFilter2D contactFilter;

    // ★ [추가] 성을 미리 기억해둘 변수
    private Targetable cachedCastle;

    [Header("Friendly Tower Avoidance")]
    public float avoidDuration = 0.6f;
    public float avoidSpeedMul = 1.2f;

    [Header("Avoidance Filters")]
    public float avoidanceGrace = 0.35f;
    public float minSpeedToAvoid = 0.1f;
    public float minDotBlock = 0.25f;

    private float avoidUntil = 0f;
    private Vector2 avoidDir = Vector2.zero;
    private float spawnGraceUntil = 0f;

    [Header("범위공격 옵션")]
    public bool isAreaAttack = false;
    public float areaAttackRadius = 3.0f;
    public GameObject areaAttackEffectPrefab;

    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Targetable currentTarget;
    private Targetable myTargetable;
    private Coroutine aiCoroutine;

    private bool isLive = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        myTargetable = GetComponent<Targetable>();
    }

    void OnEnable()
    {
        currentTarget = null;
        isLive = false;
        spawnGraceUntil = Time.time + avoidanceGrace;

        if (aiCoroutine != null) StopCoroutine(aiCoroutine);
        aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
    }

    void Start()
    {
        if (targetLayer.value == 0)
        {
            Debug.LogError($"⛔ [Enemy] '{name}' Target Layer가 설정되지 않았습니다!");
        }

        contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(targetLayer);
        contactFilter.useTriggers = true;

        // ★ [핵심 수정] 태어날 때, 맵에 있는 'Castle'을 찾아서 기억해둠!
        GameObject castleObj = GameObject.FindGameObjectWithTag("Castle");
        if (castleObj != null)
        {
            cachedCastle = castleObj.GetComponent<Targetable>();
        }
        else
        {
            // 성을 못 찾았으면 경고 (태그 확인 필수!)
            // Debug.LogWarning("Enemy: Castle 태그를 가진 오브젝트를 찾을 수 없습니다.");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.1f);
        Gizmos.DrawSphere(transform.position, detectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, castlePriorityRadius);

        if (currentTarget != null && isLive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);

            if (currentTarget.CompareTag("Castle"))
                Gizmos.DrawSphere(currentTarget.transform.position, 0.5f);
        }
    }

    // SpawnPoint에서 호출
    public void init(SpawnData data)
    {
        speed = data.speed;
        if (myTargetable != null)
        {
            myTargetable.maxHealth = data.health;
            myTargetable.currentHealth = data.health;
        }
        isLive = true;
    }

    public void slowDown(float rate, float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(SlowDownRoutine(rate, duration));
    }

    private IEnumerator SlowDownRoutine(float rate, float duration)
    {
        float originalSpeed = speed;
        speed *= rate;
        yield return new WaitForSeconds(duration);
        speed = originalSpeed;
    }

    IEnumerator UpdateTargetCoroutineDelayed()
    {
        float delay = Random.Range(0f, aiUpdateFrequency);
        yield return new WaitForSeconds(delay);
        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            if (currentTarget != null && currentTarget.isDead)
            {
                currentTarget = null;
            }

            currentTarget = FindClosestTarget();

            float wait = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (wait < 0.1f) wait = 0.1f;
            yield return new WaitForSeconds(wait);
        }
    }

    Targetable FindClosestTarget()
    {
        if (!isLive) return null;

        // 1. 유닛 탐색 (기존 로직)
        Targetable bestUnit = null;
        float closestUnitDist = float.MaxValue;

        // 주변 유닛들을 물리 탐색
        int count = Physics2D.OverlapCircle(transform.position, detectionRadius, contactFilter, targetBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = targetBuffer[i];

            // 성은 따로 계산할 거니까 여기서 걸려도 무시 (중복 계산 방지)
            if (col.CompareTag("Castle")) continue;

            if (col.TryGetComponent(out Targetable t))
            {
                if (!t.isDead)
                {
                    float dist = Vector2.Distance(transform.position, col.ClosestPoint(transform.position));
                    if (dist < closestUnitDist)
                    {
                        closestUnitDist = dist;
                        bestUnit = t;
                    }
                }
            }
        }

        // 2. 성 거리 계산 (★ 물리 탐색에 의존하지 않고 직접 계산)
        float distToCastle = float.MaxValue;
        bool isCastleAlive = (cachedCastle != null && !cachedCastle.isDead);

        if (isCastleAlive)
        {
            // 내 위치에서 미리 기억해둔 성까지의 거리 계산
            distToCastle = Vector2.Distance(transform.position, cachedCastle.GetComponent<Collider2D>().ClosestPoint(transform.position));
        }

        // 3. ★ 최종 우선순위 결정 로직 ★

        // [우선순위 1] 성이 살아있고, '노란 원(15m)' 안에 들어왔는가?
        if (isCastleAlive && distToCastle <= castlePriorityRadius)
        {
            // 유닛이 옆에 있든 말든 무조건 성 공격!
            return cachedCastle;
        }

        // [우선순위 2] 노란 원 밖이라면? -> 더 가까운 놈 공격
        if (bestUnit != null)
        {
            // 유닛이 성보다 가까우면 유닛 공격
            if (closestUnitDist < distToCastle)
            {
                return bestUnit;
            }
        }

        // 유닛이 없거나, 성이 유닛보다 가까우면 성 공격
        if (isCastleAlive)
        {
            return cachedCastle;
        }

        return bestUnit;
    }

    void FixedUpdate()
    {
        if (!isLive || (myTargetable != null && (myTargetable.IsKnockedBack || myTargetable.isDead)))
        {
            if (myTargetable != null && !myTargetable.IsKnockedBack)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPos = currentTarget.transform.position;
        Vector2 currentPos = transform.position;
        Vector2 dir = (targetPos - currentPos).normalized;

        if (Time.time < avoidUntil)
        {
            dir = Vector2.Lerp(dir, avoidDir, 0.7f).normalized;
            dir *= avoidSpeedMul;
        }

        Vector2 nextPos = currentPos + dir * speed * Time.fixedDeltaTime;
        rb.MovePosition(nextPos);
    }

    void LateUpdate()
    {
        if (!isLive) return;
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null) return;

        spriter.flipX = currentTarget.transform.position.x < transform.position.x;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!isLive) return;
        if (myTargetable != null && (myTargetable.IsKnockedBack || myTargetable.isDead)) return;
        if (Time.time < lastAttackTime + attackCooldown) return;

        Targetable target = collision.gameObject.GetComponent<Targetable>();

        if (target != null && ((1 << collision.gameObject.layer) & targetLayer) != 0)
        {
            if (isAreaAttack) PerformAreaAttack();
            else target.TakeDamage(attackDamage, transform);

            lastAttackTime = Time.time;
        }
    }

    void PerformAreaAttack()
    {
        if (areaAttackEffectPrefab != null) Instantiate(areaAttackEffectPrefab, transform.position, Quaternion.identity);

        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);
        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead)
            {
                t.TakeDamage(attackDamage, transform);
            }
        }
    }

    public void TryAvoid(Vector2 obstaclePos)
    {
        if (!isLive) return;
        if (Time.time < spawnGraceUntil) return;
        if (Time.time < avoidUntil) return;

        if (rb.linearVelocity.magnitude > minSpeedToAvoid) return;

        Vector2 toObstacle = (obstaclePos - (Vector2)transform.position).normalized;
        float dot = Vector2.Dot(rb.linearVelocity.normalized, toObstacle);

        if (dot > minDotBlock)
        {
            Vector2 away = -(toObstacle + Random.insideUnitCircle * 0.5f).normalized;
            avoidDir = away;
            avoidUntil = Time.time + avoidDuration;
        }
    }

    public void ApplyBossSpec(BossSpec spec)
    {
        if (spec == null) return;
        attackDamage = spec.attackDamage;
        attackCooldown = spec.attackCooldown;
        detectionRadius = spec.detectionRadius;
        speed = spec.moveSpeed;

        if (myTargetable != null)
        {
            myTargetable.maxHealth = spec.maxHP;
            myTargetable.currentHealth = spec.maxHP;
        }
        isAreaAttack = spec.isAreaAttack;
        areaAttackRadius = spec.areaRadius;
        if (spec.areaAttackEffect != null)
            areaAttackEffectPrefab = spec.areaAttackEffect;

        transform.localScale = Vector3.one * spec.scaleMultiplier;

        isLive = true;
    }
}