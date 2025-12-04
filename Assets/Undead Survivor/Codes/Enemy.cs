using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum AttackStyle
{
    Single,
    Circle,
    Fan
}

public enum EnemyType
{
    Normal, // 일반 몬스터 (Speed 연산 안 함)
    Boss    // 보스 몬스터 (Speed 연산 함)
}

public class Enemy : MonoBehaviour
{
    [Header("적군 타입 설정")]
    public EnemyType enemyType = EnemyType.Normal;

    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float attackDamage = 1f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    [Header("외부 속도 제어")]
    public float speedMultiplier = 1f;

    [Header("공격 연출 설정")]
    public float attackImpactDelay = 0.2f;
    [Tooltip("보스가 단일 공격할 때만 나오는 이펙트")]
    public GameObject hitEffectPrefab;

    // [수정 반영] 이펙트 추적 On/Off 설정
    [Tooltip("범위 공격 이펙트가 적 유닛(보스)을 따라다닐지 여부")]
    public bool areaEffectFollows = true;

    [Header("공격 범위 설정")]
    public AttackStyle attackStyle = AttackStyle.Single;
    public float areaAttackRadius = 3.0f;
    [Range(0, 360)] public float fanAngle = 120f;
    public GameObject areaAttackEffectPrefab;

    [Header("AI 설정")]
    public LayerMask targetLayer;
    public float detectionRadius = 100f;
    public float aiUpdateFrequency = 0.5f;
    public float castlePriorityRadius = 15.0f;

    private float aiUpdateRandomDelay = 0.1f;
    private Collider2D[] targetBuffer = new Collider2D[30];
    private ContactFilter2D contactFilter;
    private Targetable cachedCastle;

    [Header("회피 및 이동 설정")]
    public float avoidDuration = 0.6f;
    public float avoidSpeedMul = 1.2f;
    public float avoidanceGrace = 0.35f;
    public float minSpeedToAvoid = 0.1f;
    public float minDotBlock = 0.25f;

    private float avoidUntil = 0f;
    private Vector2 avoidDir = Vector2.zero;
    private float spawnGraceUntil = 0f;

    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Animator anim;
    private Targetable currentTarget;
    private Targetable myTargetable;
    private Coroutine aiCoroutine;
    private bool isLive = false;

    // 해시값 미리 저장 (최적화)
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashDead = Animator.StringToHash("Dead");

    private float lastSetSpeed = -1f;

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
        isLive = true;

        spawnGraceUntil = Time.time + avoidanceGrace;
        avoidDir = Vector2.zero;
        avoidUntil = 0f;
        speedMultiplier = 1f;
        lastSetSpeed = -1f;

        // OnEnable 시 EnemyType이 Normal로 초기화되는 것은 버그일 가능성이 높습니다.
        // Boss 타입 설정은 ApplyBossSpec에서 이루어져야 하므로 이 줄은 제거하거나 주석 처리하는 것이 좋습니다.
        // enemyType = EnemyType.Normal; 

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
    public void SetSpeedMultiplier(float multiplier) { speedMultiplier = multiplier; }
    private IEnumerator SlowDownRoutine(float rate, float duration)
    {
        float originalSpeed = speed; speed *= rate; yield return new WaitForSeconds(duration); speed = originalSpeed;
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
            if (currentTarget != null && currentTarget.isDead) currentTarget = null;
            currentTarget = FindClosestTarget();
            float wait = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (wait < 0.1f) wait = 0.1f;
            yield return new WaitForSeconds(wait);
        }
    }
    Targetable FindClosestTarget()
    {
        if (!isLive) return null;
        Targetable bestUnit = null; float closestUnitDist = float.MaxValue;
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
                    if (dist < closestUnitDist) { closestUnitDist = dist; bestUnit = t; }
                }
            }
        }
        float distToCastle = float.MaxValue;
        bool castleAlive = (cachedCastle != null && !cachedCastle.isDead);
        if (castleAlive) distToCastle = Vector2.Distance(transform.position, cachedCastle.GetComponent<Collider2D>().ClosestPoint(transform.position));
        if (castleAlive && distToCastle <= castlePriorityRadius) return cachedCastle;
        if (bestUnit != null && closestUnitDist < distToCastle) return bestUnit;
        if (castleAlive) return cachedCastle;
        return bestUnit;
    }

    void FixedUpdate()
    {
        if (!isLive || (myTargetable != null && (myTargetable.IsKnockedBack || myTargetable.isDead)))
        {
            if (myTargetable != null && !myTargetable.IsKnockedBack) rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateMoveAnimation(0f);
            return;
        }

        Vector2 targetPos = currentTarget.transform.position;
        Vector2 currentPos = transform.position;
        Vector2 dir = (targetPos - currentPos).normalized;

        if (Time.time < avoidUntil) dir = Vector2.Lerp(dir, avoidDir, 0.7f).normalized * avoidSpeedMul;

        float finalSpeed = speed * speedMultiplier;
        Vector2 nextPos = currentPos + dir * finalSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPos);

        UpdateMoveAnimation(finalSpeed);
    }

    void UpdateMoveAnimation(float currentSpeed)
    {
        if (anim == null) return;
        if (enemyType == EnemyType.Normal) return;
        if (Mathf.Abs(lastSetSpeed - currentSpeed) < 0.01f) return;

        lastSetSpeed = currentSpeed;
        anim.SetFloat(HashSpeed, currentSpeed);
    }

    void LateUpdate()
    {
        if (!isLive || (myTargetable != null && myTargetable.IsKnockedBack)) return;
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
            if (anim != null) anim.SetTrigger(HashAttack);

            lastAttackTime = Time.time;
            StartCoroutine(AttackRoutine(t));
        }
    }

    IEnumerator AttackRoutine(Targetable target)
    {
        yield return new WaitForSeconds(attackImpactDelay);
        if (!isLive || !gameObject.activeSelf) yield break;

        // 범위 공격 이펙트 처리
        if (attackStyle != AttackStyle.Single && areaAttackEffectPrefab != null)
        {
            // [수정] 이펙트 추적 설정에 따라 부모를 설정하거나 null로 설정
            Transform parentTransform = areaEffectFollows ? transform : null;

            // 4번째 인자에 'parentTransform'을 넣어 부모를 설정
            GameObject effectInstance = Instantiate(areaAttackEffectPrefab, transform.position, Quaternion.identity, parentTransform);

            if (spriter.flipX)
            {
                SpriteRenderer sr = effectInstance.GetComponent<SpriteRenderer>();
                if (sr) sr.flipX = true;
                else { Vector3 s = effectInstance.transform.localScale; s.x *= -1; effectInstance.transform.localScale = s; }
            }
        }

        switch (attackStyle)
        {
            case AttackStyle.Single:
                if (target != null && !target.isDead)
                {
                    if (enemyType == EnemyType.Boss && hitEffectPrefab != null)
                        Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);

                    target.TakeDamage(attackDamage, transform);
                }
                break;

            case AttackStyle.Circle:
                PerformCircleAttack();
                break;

            case AttackStyle.Fan:
                PerformFanAttack();
                break;
        }
    }

    void PerformCircleAttack()
    {
        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);
        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead) t.TakeDamage(attackDamage, transform);
        }
    }

    void PerformFanAttack()
    {
        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);
        Vector2 facingDir = spriter.flipX ? Vector2.left : Vector2.right;
        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead)
            {
                Vector2 targetDir = (t.transform.position - transform.position).normalized;
                if (Vector2.Angle(facingDir, targetDir) <= fanAngle * 0.5f) t.TakeDamage(attackDamage, transform);
            }
        }
    }

    public void TryAvoid(Vector2 obstaclePos)
    {
        if (!isLive) return;
        if (Time.time < spawnGraceUntil || Time.time < avoidUntil) return;
        if (rb.linearVelocity.magnitude > minSpeedToAvoid) return;

        Vector2 toObstacle = (obstaclePos - (Vector2)transform.position).normalized;
        if (Vector2.Dot(rb.linearVelocity.normalized, toObstacle) > minDotBlock)
        {
            avoidDir = -(toObstacle + Random.insideUnitCircle * 0.5f).normalized;
            avoidUntil = Time.time + avoidDuration;
        }
    }

    public void ApplyBossSpec(BossSpec spec)
    {
        if (spec == null) return;

        enemyType = EnemyType.Boss;

        attackDamage = spec.attackDamage;
        attackCooldown = spec.attackCooldown;
        detectionRadius = spec.detectionRadius;
        speed = spec.moveSpeed;

        if (myTargetable != null)
        {
            myTargetable.maxHealth = spec.maxHP;
            myTargetable.currentHealth = spec.maxHP;
        }

        attackStyle = spec.isAreaAttack ? AttackStyle.Circle : AttackStyle.Single;
        areaAttackRadius = spec.areaRadius;
        if (spec.areaAttackEffect != null) areaAttackEffectPrefab = spec.areaAttackEffect;

        transform.localScale = Vector3.one * spec.scaleMultiplier;
        isLive = true;
    }

    public void OnEnemyDead()
    {
        isLive = false;
        rb.linearVelocity = Vector2.zero;
        if (GetComponent<Collider2D>()) GetComponent<Collider2D>().enabled = false;

        if (anim != null) anim.SetTrigger(HashDead);

        StartCoroutine(DisableDelayed());
    }

    IEnumerator DisableDelayed()
    {
        yield return new WaitForSeconds(2.0f);
        gameObject.SetActive(false);
    }
}