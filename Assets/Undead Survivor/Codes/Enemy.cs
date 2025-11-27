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

    [Header("범위공격 옵션")]
    public bool isAreaAttack = false;
    public float areaAttackRadius = 3.0f;
    public GameObject areaAttackEffectPrefab;

    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Targetable currentTarget;
    private Targetable myTargetable;
    private Coroutine aiCoroutine;

    private float maxHealth;
    private float health;

    // 움직임 허가 플래그 (init 대기용)
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
        isLive = false; // 태어날 때는 무조건 false. init()을 기다림.
        spawnGraceUntil = Time.time + avoidanceGrace;

        if (aiCoroutine != null) StopCoroutine(aiCoroutine);
        aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
    }

    void Start()
    {
        // 강제 실행 코드 삭제됨. 
        // 이제 SpawnPoint가 init을 안 부르면 얘는 영원히 안 움직이는 게 정상임.
        if (targetLayer.value == 0)
        {
            Debug.LogError($"⛔ [Enemy] '{name}' Target Layer가 설정되지 않았습니다!");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, detectionRadius);
    }

    // ★ SpawnPoint에서 이 함수를 불러줘야만 움직임 시작!
    public void init(SpawnData data)
    {
        speed = data.speed;
        maxHealth = data.health;
        health = data.health;

        if (myTargetable != null)
        {
            myTargetable.maxHealth = data.health;
            myTargetable.currentHealth = data.health;
        }

        // 데이터 세팅 끝났으니 움직임 허가
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
        // init 안됐으면 타겟 탐색도 안 함
        if (!isLive) return null;

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
        // isLive가 false면 절대 움직이지 않음
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaAttackRadius, targetLayer);
        foreach (Collider2D hit in hits)
        {
            Targetable t = hit.GetComponent<Targetable>();
            if (t != null && !t.isDead) t.TakeDamage(attackDamage, transform);
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
            areaAttackEffectPrefab = spec.areaAttackEffect;

        transform.localScale = Vector3.one * spec.scaleMultiplier;

        isLive = true;
    }
}