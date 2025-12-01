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

    // [추가] 늪지대/외부 효과를 위한 속도 배율
    [Header("외부 속도 제어")]
    public float speedMultiplier = 1f;

    [Header("AI 설정")]
    public LayerMask targetLayer;
    public float detectionRadius = 100f;
    public float aiUpdateFrequency = 0.5f;
    public float castlePriorityRadius = 15.0f;

    private float aiUpdateRandomDelay = 0.1f;

    private Collider2D[] targetBuffer = new Collider2D[30];
    private ContactFilter2D contactFilter;

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
    private Animator anim;   // ★ 공격 애니메이션용
    private Targetable currentTarget;
    private Targetable myTargetable;
    private Coroutine aiCoroutine;

    private bool isLive = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        myTargetable = GetComponent<Targetable>();
    }

    void OnEnable()
    {
        currentTarget = null;
        isLive = false;
        spawnGraceUntil = Time.time + avoidanceGrace;
        avoidDir = Vector2.zero;
        avoidUntil = 0f;
        // [수정] 활성화 시 속도 배율 초기화
        speedMultiplier = 1f;

        if (aiCoroutine != null) StopCoroutine(aiCoroutine);
        aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
    }

    void Start()
    {
        contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(targetLayer);
        contactFilter.useTriggers = true;

        GameObject castleObj = GameObject.FindGameObjectWithTag("Castle");
        if (castleObj != null)
            cachedCastle = castleObj.GetComponent<Targetable>();
    }

    // ★ [복구] 에디터에서 공격 범위와 성 우선 범위를 보여주는 기능
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

    // [추가 시작] 늪지대에서 호출할 함수 (CS1061 오류 해결)
    /// <summary>
    /// 외부 요인에 의한 이동 속도 배율을 설정합니다.
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }
    // [추가 끝]

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
                currentTarget = null;

            currentTarget = FindClosestTarget();

            float wait = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (wait < 0.1f) wait = 0.1f;

            yield return new WaitForSeconds(wait);
        }
    }

    Targetable FindClosestTarget()
    {
        if (!isLive) return null;

        Targetable bestUnit = null;
        float closestUnitDist = float.MaxValue;

        int count = Physics2D.OverlapCircle(transform.position, detectionRadius, contactFilter, targetBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = targetBuffer[i];

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

        float distToCastle = float.MaxValue;
        bool castleAlive = (cachedCastle != null && !cachedCastle.isDead);

        if (castleAlive)
        {
            distToCastle = Vector2.Distance(transform.position,
                cachedCastle.GetComponent<Collider2D>().ClosestPoint(transform.position));
        }

        if (castleAlive && distToCastle <= castlePriorityRadius)
            return cachedCastle;

        if (bestUnit != null && closestUnitDist < distToCastle)
            return bestUnit;

        if (castleAlive)
            return cachedCastle;

        return bestUnit;
    }

    void FixedUpdate()
    {
        if (!isLive ||
            (myTargetable != null && (myTargetable.IsKnockedBack || myTargetable.isDead)))
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
            dir = Vector2.Lerp(dir, avoidDir, 0.7f).normalized * avoidSpeedMul;
        }

        // [수정] 최종 이동 속도에 speedMultiplier 적용
        float finalSpeed = speed * speedMultiplier;

        Vector2 nextPos = currentPos + dir * finalSpeed * Time.fixedDeltaTime;
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

        Targetable t = collision.gameObject.GetComponent<Targetable>();

        if (t != null && ((1 << collision.gameObject.layer) & targetLayer) != 0)
        {
            // ★ 공격 애니메이션 추가
            if (anim != null)
                anim.SetTrigger("Attack");

            if (isAreaAttack) PerformAreaAttack();
            else t.TakeDamage(attackDamage, transform);

            lastAttackTime = Time.time;
        }
    }

    void PerformAreaAttack()
    {
        if (areaAttackEffectPrefab != null)
            Instantiate(areaAttackEffectPrefab, transform.position, Quaternion.identity);

        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);

        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead)
                t.TakeDamage(attackDamage, transform);
        }
    }

    // ★ [복구] 장애물이나 아군 타워를 만났을 때 비켜가는 로직
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