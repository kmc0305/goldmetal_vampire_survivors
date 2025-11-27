using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
// ✅ [수정] 모호함 방지 명시
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

/// <summary>
/// 적 유닛의 AI, 이동, 공격 및 보스 스펙 적용을 담당합니다.
/// [최적화 적용됨]: 타겟 탐색 부하 분산
/// [수정]: init(SpawnData), slowDown(rate, time) 오버로드 추가
/// </summary>
public class Enemy : MonoBehaviour
{
    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float attackDamage = 1f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    [Header("AI 설정")]
    public LayerMask targetLayer;
    public float detectionRadius = 15f;
    public float aiUpdateFrequency = 0.5f; // AI 판단 주기

    // ✅ [최적화] AI 업데이트 분산용 지연 변수
    private float aiUpdateRandomDelay = 0.1f;

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

    [Header("Boss HP Bar")]
    public Transform hpBarRoot;
    public Transform hpFill;
    public float barWidth = 2.0f;
    public float barHeight = 0.25f;
    public Vector3 barOffset = new Vector3(0f, 1.5f, 0f);

    [Header("범위공격 옵션")]
    public bool isAreaAttack = false;
    public float areaAttackRadius = 3.0f;
    public GameObject areaAttackEffectPrefab; // 광역 공격 이펙트

    // 내부 변수
    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Animator anim; // 애니메이터 추가
    private Targetable currentTarget;
    private Targetable myTargetable;
    private Coroutine aiCoroutine;

    // 보스 스펙 적용용
    private float maxHealth;
    private float health;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        myTargetable = GetComponent<Targetable>();
    }

    void OnEnable()
    {
        // 초기화
        currentTarget = null;
        spawnGraceUntil = Time.time + avoidanceGrace;

        // ✅ [최적화] AI 코루틴 지연 시작
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
        rb.linearVelocity = Vector2.zero;
    }

    // ========================================================================
    // ✅ [추가] SpawnPoint에서 호출하는 초기화 함수 (SpawnData 버전)
    // ========================================================================
    public void init(SpawnData data)
    {
        // 데이터 적용
        speed = data.speed;
        maxHealth = data.health;
        health = data.health;

        // Targetable 컴포넌트에도 체력 적용
        if (myTargetable != null)
        {
            myTargetable.maxHealth = data.health;
            myTargetable.currentHealth = data.health;
        }

        // 스프라이트나 애니메이션 설정이 필요하다면 여기서 처리 (예: spriteType)
        // anim.runtimeAnimatorController = ... 
    }

    // (기존 SpawnPoint 직접 참조 버전도 유지 가능)
    public void init(SpawnPoint spawnPoint)
    {
        // 필요 시 구현
    }

    // ========================================================================
    // ✅ [추가] BombardBullet에서 호출하는 감속 함수 (인자 2개 버전)
    // ========================================================================
    public void slowDown(float rate, float duration)
    {
        // 이미 느려져 있는 상태 등을 체크하고 싶다면 여기서 로직 추가
        StartCoroutine(SlowDownRoutine(rate, duration));
    }

    private IEnumerator SlowDownRoutine(float rate, float duration)
    {
        float originalSpeed = speed;
        // rate가 0.1f라면 속도가 10%가 되는 것인지, 10% 줄어드는 것인지에 따라 로직 결정
        // 여기서는 BombardBullet 로직상 0.1f로 '만드는' 것(매우 느려짐)으로 가정하거나
        // 0.1f만큼 감속(0.9배)일 수 있음. 문맥상 rate 배율 적용이 일반적.
        speed *= rate;

        yield return new WaitForSeconds(duration);

        // 원래대로 복구 (다른 버프에 의해 속도가 바뀌었을 수도 있으니 주의 필요하지만 간단하게 복구)
        speed = originalSpeed;
    }

    // ========================================================================
    // ✅ [최적화] AI 로직 (코루틴 분산 + Jitter)
    // ========================================================================
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

            if (currentTarget == null)
            {
                currentTarget = FindClosestTarget();
            }

            // 랜덤 주기로 대기 (프레임 분산)
            float wait = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (wait < 0.1f) wait = 0.1f;
            yield return new WaitForSeconds(wait);
        }
    }

    Targetable FindClosestTarget()
    {
        float closestDist = float.MaxValue;
        Targetable bestTarget = null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);
        foreach (Collider2D col in hits)
        {
            Targetable t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
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

    void FixedUpdate()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

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
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null) return;

        spriter.flipX = currentTarget.transform.position.x < transform.position.x;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (Time.time < lastAttackTime + attackCooldown) return;

        Targetable target = collision.gameObject.GetComponent<Targetable>();

        if (target != null && ((1 << collision.gameObject.layer) & targetLayer) != 0)
        {
            if (isAreaAttack)
            {
                PerformAreaAttack();
            }
            else
            {
                target.TakeDamage(attackDamage, transform);
            }
            lastAttackTime = Time.time;
        }
    }

    void PerformAreaAttack()
    {
        if (areaAttackEffectPrefab != null)
        {
            Instantiate(areaAttackEffectPrefab, transform.position, Quaternion.identity);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaAttackRadius, targetLayer);
        foreach (Collider2D hit in hits)
        {
            Targetable t = hit.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                t.TakeDamage(attackDamage, transform);
            }
        }
    }

    public void TryAvoid(Vector2 obstaclePos)
    {
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

        maxHealth = spec.maxHP;
        health = spec.maxHP;
        if (myTargetable != null)
        {
            myTargetable.maxHealth = spec.maxHP;
            myTargetable.currentHealth = spec.maxHP;
        }

        isAreaAttack = spec.isAreaAttack;
        areaAttackRadius = spec.areaRadius;
        if (spec.areaAttackEffect != null)
        {
            areaAttackEffectPrefab = spec.areaAttackEffect;
        }

        transform.localScale = Vector3.one * spec.scaleMultiplier;

        // BossSpec에 colorOverlay가 없는 경우를 대비해 주석 처리 또는 조건문 필요
        // if (spec.colorOverlay != Color.white) spriter.color = spec.colorOverlay;
    }
}