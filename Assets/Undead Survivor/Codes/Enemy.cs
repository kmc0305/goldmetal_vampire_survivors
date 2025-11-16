using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [적 유닛] AI 추적 및 공격 로직을 담당합니다. (AllyAI.cs와 동일한 구조)
/// Targetable.cs (생명)과 Rigidbody2D (물리)에 의존합니다.
/// </summary>
public class Enemy : MonoBehaviour
{
    // === [회피 설정] 같은 진영 '성'에 부딪히면 잠시 접선 방향으로 회피 ===
    [Header("Friendly Tower Avoidance")]
    public float avoidDuration = 0.6f;      // 회피 지속 시간(초)
    public float avoidSpeedMul = 1.2f;      // 회피 중 속도 배수

    // === [회피 필터/그레이스] 스폰 직후 우회 금지 + '막힘' 상황에서만 우회 ===
    [Header("Avoidance Filters")]
    public float avoidanceGrace = 0.35f;    // 스폰 직후 우회 비활성 시간(초)
    public float minSpeedToAvoid = 0.1f;    // 너무 느리면 우회 X
    public float minDotBlock = 0.25f;       // '정말 막혔는지' 판정 임계값(0~1)

    private float avoidUntil = 0f;          // 회피 종료 시각
    private Vector2 avoidDir = Vector2.zero;// 회피 이동 방향(접선)
    private float spawnGraceUntil = 0f;     // 스폰 그레이스 만료 시각

    [Header("Boss HP Bar")]
    public Transform hpBarRoot;        // HPBarRoot
    public Transform hpFill;           // HPFill (SpriteRenderer 달린 오브젝트)
    public float barWidth = 2.0f;      // 바 전체 가로 길이
    public float barHeight = 0.25f;    // 바 높이
    public Vector3 barOffset = new Vector3(0f, 1.5f, 0f); // 머리 위 오프셋

    [Header("범위공격 옵션 (Boss/일반 공용)")]
    public bool isAreaAttack = false;      // ★ 인스펙터 또는 BossSpec로 제어
    public float areaAttackRadius = 3.0f;  // ★ 반경 (BossSpec.areaRadius로도 세팅됨)

    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float health;
    public float maxHealth;
    public RuntimeAnimatorController[] animCon;

    [Header("AI 설정")]
    public LayerMask targetLayer;          // 인스펙터에서 'Ally' 선택
    public float detectionRadius = 15f;
    private float aiUpdateFrequency = 0.5f;

    [Header("공격 설정")]
    public float attackDamage = 5f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    // 컴포넌트
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private Coroutine aiCoroutine;
    private Animator anim;

    // 타겟 관련
    private Targetable currentTarget;

    // 넉백
    private bool isKnockedBack = false;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    void OnEnable()
    {
        isKnockedBack = false;
        spawnGraceUntil = Time.time + avoidanceGrace;  // 스폰 그레이스 시작
        avoidDir = Vector2.zero;
        avoidUntil = 0f;

        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutine());

        UpdateHPBar();
    }

    void OnDisable()
    {
        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }

        // 🔹 여기서 '죽어서 비활성화된 경우'에만 킬 수 증가
        var tar = GetComponent<Targetable>();
        if (tar != null && tar.isDead && GameManager.instance != null)
        {
            GameManager.instance.AddKill();
        }

        currentTarget = null;
        if (rigid != null)
            rigid.linearVelocity = Vector2.zero;
        avoidDir = Vector2.zero;
        avoidUntil = 0f;
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            if (!isKnockedBack)
            {
                currentTarget = FindClosestTarget();
            }
            yield return new WaitForSeconds(aiUpdateFrequency);
        }
    }

    Targetable FindClosestTarget()
    {
        float closestDistance = float.MaxValue;
        Targetable bestTarget = null;

        Collider2D[] targetsInView =
            Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);

        foreach (Collider2D col in targetsInView)
        {
            var t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < closestDistance)
                {
                    closestDistance = d;
                    bestTarget = t;
                }
            }
        }
        return bestTarget;
    }

    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;

        float cur = health;
        float max = maxHealth;
        var tar = GetComponent<Targetable>();
        if (tar != null)
        {
            cur = tar.currentHealth;
            max = tar.maxHealth;
        }

        float ratio = (max > 0f) ? Mathf.Clamp01(cur / max) : 0f;

        hpBarRoot.localPosition = barOffset;

        float w = barWidth * ratio;
        hpFill.localScale = new Vector3(w, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - w) * 0.5f, 0f, 0f);

        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);

        if (tar != null) hpBarRoot.gameObject.SetActive(!tar.isDead);
    }

    bool IsFriendlyTower(GameObject other)
    {
        if (other.layer != gameObject.layer) return false;

        // 자식/부모 모두 커버하여 '성' 판별
        return other.GetComponent<SpawnPoint>() != null
            || other.GetComponentInParent<SpawnPoint>() != null
            || other.GetComponent<AllySpawner>() != null
            || other.GetComponentInParent<AllySpawner>() != null;
    }

    void FixedUpdate()
    {
        // 회피 중이면 접선 방향 우선 이동
        if (Time.time < avoidUntil && avoidDir.sqrMagnitude > 0.0001f)
        {
            Vector2 step = avoidDir.normalized * speed * avoidSpeedMul * Time.fixedDeltaTime;
            rigid.MovePosition(rigid.position + step);
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        // 넉백 중이면 이동 로직 정지
        if (isKnockedBack) return;

        // 타겟 없으면 정지
        if (currentTarget == null)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        // 일반 추적 이동
        Vector2 dirVec = currentTarget.transform.position - transform.position;
        Vector2 nextVec = dirVec.normalized * speed * Time.fixedDeltaTime;

        rigid.MovePosition(rigid.position + nextVec);
        rigid.linearVelocity = Vector2.zero;
    }

    void LateUpdate()
    {
        if (isKnockedBack) return;
        if (currentTarget == null) return;

        spriter.flipX = currentTarget.transform.position.x < rigid.position.x;
        UpdateHPBar();
    }

    // === 충돌 기반 로직 ===
    void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartAvoidance(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // 회피 유지/재개 시도 (덜덜이 방지)
        TryStartAvoidance(collision);

        // 공격 처리 (단일/범위 공용 쿨다운)
        if (Time.time < lastAttackTime + attackCooldown || isKnockedBack) return;

        // 현재타겟이 없으면 굳이 공격 X (원하면 삭제 가능)
        if (currentTarget == null) return;

        // 충돌한 대상이 '현재 타겟'일 때만 트리거
        if (collision.gameObject != currentTarget.gameObject) return;

        if (isAreaAttack)
        {
            DoAreaAttack();  // ★ 범위 공격
        }
        else
        {
            // 단일 대상 공격
            currentTarget.TakeDamage(attackDamage, transform);
        }

        lastAttackTime = Time.time;
    }

    // === '막힌 상황'일 때만 접선 우회 시작 ===
    void TryStartAvoidance(Collision2D collision)
    {
        // (a) 스폰 직후는 우회 금지
        if (Time.time < spawnGraceUntil) return;

        // (b) 같은 진영 성이 아니면 패스
        if (!IsFriendlyTower(collision.gameObject) || collision.contactCount == 0) return;

        // (c) 충분히 움직이고 있을 때만
        if (rigid.linearVelocity.sqrMagnitude < minSpeedToAvoid * minSpeedToAvoid && currentTarget == null)
            return;

        // (d) 정말로 ‘막혔는지’ 판별
        Vector2 n = collision.GetContact(0).normal; // 성 표면에서 '밖'으로 (→ 우리쪽)
        Vector2 desired;

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)(currentTarget.transform.position - transform.position);
            desired = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : rigid.linearVelocity.normalized;
        }
        else
        {
            desired = rigid.linearVelocity.sqrMagnitude > 0.0001f ? rigid.linearVelocity.normalized : Vector2.zero;
        }
        if (desired == Vector2.zero) return;

        // desired가 타워 안쪽(-n)으로 얼마나 향하는지 (값↑ = 진짜 막힘)
        float intoWall = Vector2.Dot(desired, -n);
        if (intoWall < minDotBlock) return; // 충분히 막힌 상황 아니면 우회 X

        // (e) 두 접선 중, 목표 방향과 더 잘 맞는 쪽 선택
        Vector2 t1 = new Vector2(-n.y, n.x);
        Vector2 t2 = new Vector2(n.y, -n.x);
        Vector2 chosen = (Vector2.Dot(t1, desired) >= Vector2.Dot(t2, desired)) ? t1 : t2;

        avoidDir = chosen.normalized;
        avoidUntil = Time.time + avoidDuration;
    }

    // === 범위 공격 구현 ===
    void DoAreaAttack()
    {
        // 반경 내 '타겟 레이어'에 해당하는 모든 대상 탐색
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaAttackRadius, targetLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            var t = hits[i].GetComponent<Targetable>();
            if (t == null || t.isDead) continue;

            t.TakeDamage(attackDamage, transform);
        }
    }

    // === 넉백 ===
    public void ApplyKnockback(Vector2 knockbackDir, float power, float duration)
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(knockbackDir, power, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 knockbackDir, float power, float duration)
    {
        isKnockedBack = true;
        rigid.linearVelocity = knockbackDir * power;
        yield return new WaitForSeconds(duration);
        rigid.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }

    public void slowDown(float x,float dur)
    {
        StartCoroutine(SlowDownFor(x,dur));

    }
    private IEnumerator SlowDownFor(float x, float dur)
    {
        float og = speed;
        speed = speed * x;
        yield return new WaitForSeconds(dur);
        speed = og;
    }


    // === 스폰 데이터/보스 스펙 ===
    public void init(SpawnData data)
    {
        if (data == null) return;

        if (animCon != null && data.spriteType >= 0 && data.spriteType < animCon.Length)
            anim.runtimeAnimatorController = animCon[data.spriteType];

        speed = data.speed;
        maxHealth = data.health;
        health = data.health;
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

        isAreaAttack = spec.isAreaAttack;
        areaAttackRadius = spec.areaRadius;

        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr) sr.color = spec.tint;
        UpdateHPBar();
    }

    // 에디터에서 범위 확인
    void OnDrawGizmosSelected()
    {
        if (isAreaAttack)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, areaAttackRadius);
        }
    }
}
