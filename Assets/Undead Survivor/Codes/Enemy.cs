using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// ★ [추가] 공격 스타일을 정의하는 열거형
public enum AttackStyle
{
    Single, // 단일 공격
    Circle, // 원형 범위 공격 (기존 Area)
    Fan     // 부채꼴 범위 공격 (신규)
}

public class Enemy : MonoBehaviour
{
    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float attackDamage = 1f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    [Header("외부 속도 제어")]
    public float speedMultiplier = 1f;

    [Header("공격 연출 설정")]
    [Tooltip("애니메이션 시작 후 실제 타격/이펙트까지 걸리는 시간")]
    public float attackImpactDelay = 0.2f;
    [Tooltip("보스가 단일 공격할 때 나올 이펙트 프리팹")]
    public GameObject hitEffectPrefab;

    [Header("공격 범위 설정")]
    // ★ [변경] bool isAreaAttack 대신 스타일 선택으로 변경
    public AttackStyle attackStyle = AttackStyle.Single;

    [Tooltip("원형/부채꼴 공격의 사거리")]
    public float areaAttackRadius = 3.0f;

    [Tooltip("부채꼴 공격의 각도 (Fan 모드일 때만 사용)")]
    [Range(0, 360)] public float fanAngle = 120f;

    [Tooltip("범위 공격 시 나올 이펙트")]
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

    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Animator anim;
    private Targetable currentTarget;
    private Targetable myTargetable;
    private Coroutine aiCoroutine;

    private bool isLive = false;
    private bool isBoss = false;

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
        isBoss = false;  //프리팹으로 테스트시 isLive, isBoss를 true로 변경

        spawnGraceUntil = Time.time + avoidanceGrace;
        avoidDir = Vector2.zero;
        avoidUntil = 0f;
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

    // ★ [수정] 부채꼴 범위도 눈으로 볼 수 있게 Gizmo 기능 강화
    void OnDrawGizmosSelected()
    {
        // 1. 감지 범위
        Gizmos.color = new Color(1, 0, 0, 0.1f);
        Gizmos.DrawSphere(transform.position, detectionRadius);

        // 2. 성 우선 인식 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, castlePriorityRadius);

        // 3. 공격 범위 시각화 (타입에 따라 다르게 그림)
        Gizmos.color = Color.magenta;
        if (attackStyle == AttackStyle.Circle)
        {
            Gizmos.DrawWireSphere(transform.position, areaAttackRadius);
        }
        else if (attackStyle == AttackStyle.Fan)
        {
            // 부채꼴 그리기
            Vector3 pos = transform.position;
            // 에디터에서는 실행 중이 아닐 때 flipX를 알 수 없으므로 오른쪽 기준으로 그림
            // (실제 게임에서는 보고 있는 방향으로 나갑니다)
            Vector3 forward = Vector3.right;

            // 스프라이트 렌더러가 있으면 쳐다보는 방향 반영
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.flipX) forward = Vector3.left;

            Quaternion leftRayRot = Quaternion.AngleAxis(fanAngle * 0.5f, Vector3.forward);
            Quaternion rightRayRot = Quaternion.AngleAxis(-fanAngle * 0.5f, Vector3.forward);

            Vector3 leftRay = leftRayRot * forward;
            Vector3 rightRay = rightRayRot * forward;

            Gizmos.DrawRay(pos, leftRay * areaAttackRadius);
            Gizmos.DrawRay(pos, rightRay * areaAttackRadius);
            Gizmos.DrawWireSphere(pos, areaAttackRadius); // 거리 표시용 원
        }

        if (currentTarget != null && isLive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
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

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
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
            if (anim != null) anim.SetFloat("Speed", 0f);
            return;
        }

        Vector2 targetPos = currentTarget.transform.position;
        Vector2 currentPos = transform.position;
        Vector2 dir = (targetPos - currentPos).normalized;

        if (Time.time < avoidUntil)
        {
            dir = Vector2.Lerp(dir, avoidDir, 0.7f).normalized * avoidSpeedMul;
        }

        float finalSpeed = speed * speedMultiplier;
        Vector2 nextPos = currentPos + dir * finalSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPos);

        if (anim != null) anim.SetFloat("Speed", finalSpeed);
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
            if (anim != null) anim.SetTrigger("Attack");
            lastAttackTime = Time.time;
            StartCoroutine(AttackRoutine(t));
        }
    }

    // ★ [수정] 공격 스타일에 따라 로직 분기
    IEnumerator AttackRoutine(Targetable target)
    {
        yield return new WaitForSeconds(attackImpactDelay);

        if (!isLive || !gameObject.activeSelf) yield break;

        // 범위 이펙트는 단일 공격이 아닐 때만 재생
        if (attackStyle != AttackStyle.Single && areaAttackEffectPrefab != null)
        {
            // 보스거나, 범위공격일 때 이펙트 생성
            Instantiate(areaAttackEffectPrefab, transform.position, Quaternion.identity);
        }

        switch (attackStyle)
        {
            case AttackStyle.Single:
                // 단일 공격 (기존)
                if (target != null && !target.isDead)
                {
                    if (isBoss && hitEffectPrefab != null)
                        Instantiate(hitEffectPrefab, target.transform.position, Quaternion.identity);

                    target.TakeDamage(attackDamage, transform);
                }
                break;

            case AttackStyle.Circle:
                // 원형 범위 공격 (기존)
                PerformCircleAttack();
                break;

            case AttackStyle.Fan:
                // ★ 부채꼴 범위 공격 (신규)
                PerformFanAttack();
                break;
        }
    }

    // 원형 범위 공격 로직
    void PerformCircleAttack()
    {
        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);
        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead)
                t.TakeDamage(attackDamage, transform);
        }
    }

    // ★ [추가] 부채꼴 공격 로직
    void PerformFanAttack()
    {
        // 1. 일단 사거리 안에 있는 애들을 다 찾음
        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);

        // 2. 내 몸이 바라보는 방향 (FlipX 기준)
        // 스프라이트가 기본적으로 오른쪽을 보고 있다고 가정. FlipX면 왼쪽.
        Vector2 facingDir = spriter.flipX ? Vector2.left : Vector2.right;

        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead)
            {
                // 적이 있는 방향
                Vector2 targetDir = (t.transform.position - transform.position).normalized;

                // 내 시선과 적 방향 사이의 각도 계산
                float angle = Vector2.Angle(facingDir, targetDir);

                // 3. 그 각도가 부채꼴 각도의 절반(좌우 합쳐서 fanAngle) 이내라면 타격
                if (angle <= fanAngle * 0.5f)
                {
                    t.TakeDamage(attackDamage, transform);
                }
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
        isBoss = true;
        attackDamage = spec.attackDamage;
        attackCooldown = spec.attackCooldown;
        detectionRadius = spec.detectionRadius;
        speed = spec.moveSpeed;

        if (myTargetable != null)
        {
            myTargetable.maxHealth = spec.maxHP;
            myTargetable.currentHealth = spec.maxHP;
        }

        // ★ [수정] BossSpec의 설정에 맞춰 AttackStyle 자동 설정
        // (BossSpec 스크립트도 수정이 필요할 수 있으나, 일단 기존 bool 값 호환)
        if (spec.isAreaAttack)
        {
            // 기본은 Circle로 하되, 원한다면 나중에 BossSpec에도 enum을 넣어서 Fan으로 확장 가능
            attackStyle = AttackStyle.Circle;
        }
        else
        {
            attackStyle = AttackStyle.Single;
        }

        areaAttackRadius = spec.areaRadius;
        if (spec.areaAttackEffect != null)
            areaAttackEffectPrefab = spec.areaAttackEffect;

        transform.localScale = Vector3.one * spec.scaleMultiplier;
        isLive = true;
    }

    public void OnEnemyDead()
    {
        isLive = false;
        rb.linearVelocity = Vector2.zero;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (anim != null) anim.SetTrigger("Dead");
        StartCoroutine(DisableDelayed());
    }

    IEnumerator DisableDelayed()
    {
        yield return new WaitForSeconds(2.0f);
        gameObject.SetActive(false);
    }
}