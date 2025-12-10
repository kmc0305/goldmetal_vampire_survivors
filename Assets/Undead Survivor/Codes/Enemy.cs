using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Tooltip("범위 공격 이펙트가 캐릭터 중심에서 얼마나 앞에서 생성될지 거리 (기본값 0)")]
    public float areaEffectOffset = 0f;

    [Tooltip("보스가 단일 공격할 때만 나오는 이펙트")]
    public GameObject hitEffectPrefab;

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
    private bool isAttacking = false;

    // ★ [추가] 마지막 공격 방향 (Gizmo 그리기용)
    private Vector3 lastAttackDir = Vector3.right;

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
        isAttacking = false;

        spawnGraceUntil = Time.time + avoidanceGrace;
        avoidDir = Vector2.zero;
        avoidUntil = 0f;
        speedMultiplier = 1f;
        lastSetSpeed = -1f;

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
        // 1. 죽었거나 넉백 중이면 패스
        if (!isLive || (myTargetable != null && (myTargetable.IsKnockedBack || myTargetable.isDead)))
        {
            if (myTargetable != null && !myTargetable.IsKnockedBack) rb.linearVelocity = Vector2.zero;
            return;
        }

        // 2. 보스 공격 중 정지 로직 (기존 유지)
        if (isAttacking && enemyType == EnemyType.Boss)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateMoveAnimation(0f);
            return;
        }

        // 3. 타겟 없으면 정지
        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateMoveAnimation(0f);
            return;
        }

        // =================================================================================
        // ★ [추가된 부분] 범위 공격(Circle, Fan)일 때 사거리 체크 후 공격
        // =================================================================================
        if (attackStyle != AttackStyle.Single)
        {
            // 타겟과의 거리 계산
            float distToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);

            // 타겟이 내 공격 범위(areaAttackRadius) 안에 들어왔다면?
            if (distToTarget <= areaAttackRadius)
            {
                // 1. 이동 멈춤
                rb.linearVelocity = Vector2.zero;
                UpdateMoveAnimation(0f);

                // 2. 쿨타임 됐고, 공격 중이 아니라면 -> 공격 시작!
                if (Time.time >= lastAttackTime + attackCooldown && !isAttacking)
                {
                    if (anim != null) anim.SetTrigger(HashAttack);
                    lastAttackTime = Time.time;
                    StartCoroutine(AttackRoutine(currentTarget));
                }

                // 3. 공격 범위 안이니까 더 이상 이동하지 않고 여기서 끝냄
                return;
            }
        }
        // =================================================================================


        // 4. 이동 로직 (범위 밖이거나 Single 공격일 때는 계속 추적)
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
        isAttacking = true;

        yield return new WaitForSeconds(attackImpactDelay);
        if (!isLive || !gameObject.activeSelf)
        {
            isAttacking = false;
            yield break;
        }

        // 범위 공격 이펙트 처리
        if (attackStyle != AttackStyle.Single && areaAttackEffectPrefab != null)
        {
            Transform parentTransform = areaEffectFollows ? transform : null;
            Vector3 spawnPos = transform.position;

            if (areaEffectOffset != 0f)
            {
                // 여기서는 flipX 기준으로 계산하지만, 필요하다면 target 방향으로 계산할 수도 있음
                // 지금은 기존 유지
                Vector3 forwardDir = spriter.flipX ? Vector3.left : Vector3.right;
                spawnPos += forwardDir * areaEffectOffset;
            }

            GameObject effectInstance = Instantiate(areaAttackEffectPrefab, spawnPos, Quaternion.identity, parentTransform);

            // 이펙트 회전: 타겟을 향하도록 (선택 사항 - 부채꼴 방향과 맞추려면 아래 주석 해제)
            /*
            if (target != null) {
                 Vector3 dir = (target.transform.position - transform.position).normalized;
                 float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                 effectInstance.transform.rotation = Quaternion.Euler(0, 0, angle);
                 // 회전을 직접 시키므로 flipX 처리는 상황에 따라 다를 수 있음
            }
            */

            // 기존 flipX 반전 로직
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
                // ★ [수정됨] 타겟 정보를 넘겨줌
                PerformFanAttack(target);
                break;
        }

        isAttacking = false;
    }

    void PerformCircleAttack()
    {
        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);
        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead) t.TakeDamage(attackDamage, transform);
        }
    }

    // ★ [수정됨] 타겟(centerTarget)을 인자로 받아서 그쪽으로 부채꼴을 쏨
    void PerformFanAttack(Targetable centerTarget)
    {
        // 1. 공격 방향(aimDir) 결정
        Vector2 aimDir;

        if (centerTarget != null)
        {
            // 타겟이 있으면 타겟 방향이 중심!
            aimDir = (centerTarget.transform.position - transform.position).normalized;
        }
        else
        {
            // 타겟이 없으면(예외 상황) 그냥 보는 방향
            aimDir = spriter.flipX ? Vector2.left : Vector2.right;
        }

        // Gizmos 그리기를 위해 마지막 공격 방향 저장
        lastAttackDir = aimDir;

        // 2. 주변 적 탐지
        int count = Physics2D.OverlapCircle(transform.position, areaAttackRadius, contactFilter, targetBuffer);

        // 3. 부채꼴 판정
        for (int i = 0; i < count; i++)
        {
            if (targetBuffer[i].TryGetComponent(out Targetable t) && !t.isDead)
            {
                Vector2 targetDir = (t.transform.position - transform.position).normalized;

                // ★ aimDir(타겟 방향)과 적(targetDir) 사이의 각도를 잼
                if (Vector2.Angle(aimDir, targetDir) <= fanAngle * 0.5f)
                {
                    t.TakeDamage(attackDamage, transform);
                }
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

    void OnDrawGizmosSelected()
    {
        // 1. 탐지 범위 (노란색 원)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 2. 공격 범위 및 타겟 표시 (빨간색)
        Gizmos.color = Color.red;

        if (attackStyle == AttackStyle.Circle)
        {
            // 원형 범위 공격
            Gizmos.DrawWireSphere(transform.position, areaAttackRadius);
        }
        else if (attackStyle == AttackStyle.Fan)
        {
            // 부채꼴 공격
            Vector3 facing = Vector3.right;

            if (currentTarget != null)
            {
                facing = (currentTarget.transform.position - transform.position).normalized;
            }
            else
            {
                facing = (spriter != null && spriter.flipX) ? Vector3.left : Vector3.right;
            }

            Vector3 leftRay = Quaternion.AngleAxis(-fanAngle * 0.5f, Vector3.forward) * facing;
            Vector3 rightRay = Quaternion.AngleAxis(fanAngle * 0.5f, Vector3.forward) * facing;

            Gizmos.DrawRay(transform.position, leftRay * areaAttackRadius);
            Gizmos.DrawRay(transform.position, rightRay * areaAttackRadius);

            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawRay(transform.position, facing * areaAttackRadius);
        }
        else if (attackStyle == AttackStyle.Single)
        {
            // ★ [수정됨] 단일 공격 범위 및 타겟 표시

            // A. 근접 공격 사거리 표시 (기본 0.8f)
            Gizmos.DrawWireSphere(transform.position, 0.8f);

            // B. 현재 노리고 있는 타겟 연결선 표시
            if (currentTarget != null)
            {
                // 타겟을 향해 굵은 주황색 선 그리기
                Gizmos.color = new Color(1f, 0.5f, 0f); // 주황색
                Gizmos.DrawLine(transform.position, currentTarget.transform.position);

                // 타겟 위치에 네모 박스 표시 (확실하게 보이도록)
                Gizmos.DrawWireCube(currentTarget.transform.position, Vector3.one * 0.8f);
            }
        }

        // 3. (공통) 타겟 연결선 (녹색 - 디버깅용 보조)
        if (currentTarget != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // 반투명 녹색
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}