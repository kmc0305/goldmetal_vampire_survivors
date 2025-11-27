using System;
using System.Collections;
using System.Collections.Generic;
// using System; // 사용되지 않으므로 제거
// using System.Numerics; // 네임스페이스 충돌의 원인이므로 제거
using UnityEngine;

/// <summary>
/// [적 유닛] AI 추적 및 공격 로직을 담당합니다. (AllyAI.cs와 동일한 구조)
/// Targetable.cs (생명)과 Rigidbody2D (물리)에 의존합니다.
/// </summary>
public class Enemy : MonoBehaviour
{
    // === [회피 설정] 같은 진영 '성'에 부딪히면 잠시 접선 방향으로 회피 ===
    [Header("Friendly Tower Avoidance")]
    public float avoidDuration = 0.6f;      // 회피 지속 시간(초)
    public float avoidSpeedMul = 1.2f;      // 회피 중 속도 배수

    // === [회피 필터/그레이스] 스폰 직후 우회 금지 + '막힘' 상황에서만 우회 ===
    [Header("Avoidance Filters")]
    public float avoidanceGrace = 0.35f;    // 스폰 직후 우회 비활성 시간(초)
    public float minSpeedToAvoid = 0.1f;    // 너무 느리면 우회 X
    public float minDotBlock = 0.25f;       // '정말 막혔는지' 판정 임계값(0~1)

    private float avoidUntil = 0f;          // 회피 종료 시각
    private UnityEngine.Vector2 avoidDir = UnityEngine.Vector2.zero;// 회피 이동 방향(접선)
    private float spawnGraceUntil = 0f;     // 스폰 그레이스 만료 시각

    [Header("Boss HP Bar")]
    public Transform hpBarRoot;        // HPBarRoot
    public Transform hpFill;           // HPFill (SpriteRenderer 달린 오브젝트)
    public float barWidth = 2.0f;      // 바 전체 가로 길이
    public float barHeight = 0.25f;    // 바 높이
    public UnityEngine.Vector3 barOffset = new UnityEngine.Vector3(0f, 1.5f, 0f); // 머리 위 오프셋

    [Header("범위공격 옵션 (Boss/일반 공용)")]
    public bool isAreaAttack = false;      // ★ 인스펙터 또는 BossSpec로 제어
    public float areaAttackRadius = 3.0f;  // ★ 반경 (BossSpec.areaRadius로도 세팅됨)

    // ★★★ [추가] 광역 공격 시 생성할 이펙트 프리팹 ★★★
    public GameObject areaAttackEffectPrefab;

    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float health;
    public float maxHealth;
    public RuntimeAnimatorController[] animCon;

    [Header("AI 설정")]
    public LayerMask targetLayer;          // 인스펙터에서 'Ally' 선택
    public float detectionRadius = 15f;

    [Header("AI 최적화 설정")]
    private float aiUpdateFrequency = 0.5f;
    // [최적화] 탐색 결과를 담을 재사용 배열 (최대 20마리까지만 고려)
    private Collider2D[] scanBuffer = new Collider2D[20];

    [Header("공격 설정")]
    public float attackDamage = 5f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    // 컴포넌트
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private Coroutine aiCoroutine;
    private Animator anim;
    private Targetable myTargetable; // [수정] 넉백 상태 공유용

    // 타겟 관련
    private Targetable currentTarget;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        myTargetable = GetComponent<Targetable>(); // [수정] 캐싱
    }

    void OnEnable()
    {
        if (myTargetable) myTargetable.IsKnockedBack = false;
        spawnGraceUntil = Time.time + avoidanceGrace;  // 스폰 그레이스 시작
        avoidDir = UnityEngine.Vector2.zero;
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
            rigid.linearVelocity = UnityEngine.Vector2.zero;

        avoidDir = UnityEngine.Vector2.zero;
        avoidUntil = 0f;
    }

    IEnumerator UpdateTargetCoroutine()
    {
        // [최적화] 모든 유닛이 동시에 연산하지 않도록 시작 시 랜덤 딜레이 부여
        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, aiUpdateFrequency));

        while (gameObject.activeSelf)
        {
            // [수정] Targetable 상태 확인
            if (myTargetable != null && !myTargetable.IsKnockedBack)
            {
                currentTarget = FindClosestTarget();
            }
            yield return new WaitForSeconds(aiUpdateFrequency);
        }
    }

    Targetable FindClosestTarget()
    {
        float closestDistSqr = float.MaxValue; // 거리의 제곱 비교용
        Targetable bestTarget = null;

        // [최적화] NonAlloc 함수 사용으로 메모리 할당(Garbage) 방지
        // [수정] 경고 메시지(Obsolete) 무시 처리
#pragma warning disable 0618
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectionRadius, scanBuffer, targetLayer);
#pragma warning restore 0618

        for (int i = 0; i < count; i++)
        {
            Collider2D col = scanBuffer[i];
            var t = col.GetComponent<Targetable>();

            if (t != null && !t.isDead)
            {
                // [최적화] Vector3.Distance 대신 sqrMagnitude 사용 (제곱근 연산 제거)
                UnityEngine.Vector3 myPos = transform.position;
                UnityEngine.Vector3 targetPos = t.transform.position;
                float distSqr = (myPos - targetPos).sqrMagnitude;

                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
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
        // ★ 오류 수정: UnityEngine.Vector3 명시
        hpFill.localScale = new UnityEngine.Vector3(w, barHeight, 1f);
        // ★ 오류 수정: UnityEngine.Vector3 명시
        hpFill.localPosition = new UnityEngine.Vector3(-(barWidth - w) * 0.5f, 0f, 0f);

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
            UnityEngine.Vector2 step = avoidDir.normalized * speed * avoidSpeedMul * Time.fixedDeltaTime;
            rigid.MovePosition(rigid.position + step);
            rigid.linearVelocity = UnityEngine.Vector2.zero;
            return;
        }

        // 넉백 중이면 이동 로직 정지
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

        // 타겟 없으면 정지
        if (currentTarget == null)
        {
            rigid.linearVelocity = UnityEngine.Vector2.zero;
            return;
        }

        // 일반 추적 이동
        UnityEngine.Vector2 dirVec = currentTarget.transform.position - transform.position;
        UnityEngine.Vector2 nextVec = dirVec.normalized * speed * Time.fixedDeltaTime;

        rigid.MovePosition(rigid.position + nextVec);
        rigid.linearVelocity = UnityEngine.Vector2.zero;
    }

    void LateUpdate()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
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
        if (Time.time < lastAttackTime + attackCooldown) return;
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

        // 현재타겟이 없으면 굳이 공격 X (원하면 삭제 가능)
        if (currentTarget == null) return;

        // 충돌한 대상이 '현재 타겟'일 때만 트리거
        if (collision.gameObject != currentTarget.gameObject) return;

        if (isAreaAttack)
        {
            DoAreaAttack();  // ★ 범위 공격
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
        UnityEngine.Vector2 n = collision.GetContact(0).normal; // 성 표면에서 '밖'으로 (→ 우리쪽)
        UnityEngine.Vector2 desired;

        if (currentTarget != null)
        {
            UnityEngine.Vector2 toTarget = (UnityEngine.Vector2)(currentTarget.transform.position - transform.position);
            desired = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : rigid.linearVelocity.normalized;
        }
        else
        {
            desired = rigid.linearVelocity.sqrMagnitude > 0.0001f ? rigid.linearVelocity.normalized : UnityEngine.Vector2.zero;
        }

        if (desired == UnityEngine.Vector2.zero) return;

        // desired가 타워 안쪽(-n)으로 얼마나 향하는지 (값↑ = 진짜 막힘)
        float intoWall = UnityEngine.Vector2.Dot(desired, -n);
        if (intoWall < minDotBlock) return; // 충분히 막힌 상황 아니면 우회 X

        // (e) 두 접선 중, 목표 방향과 더 잘 맞는 쪽 선택
        UnityEngine.Vector2 t1 = new UnityEngine.Vector2(-n.y, n.x);
        UnityEngine.Vector2 t2 = new UnityEngine.Vector2(n.y, -n.x);

        UnityEngine.Vector2 chosen = (UnityEngine.Vector2.Dot(t1, desired) >= UnityEngine.Vector2.Dot(t2, desired)) ? t1 : t2;

        avoidDir = chosen.normalized;
        avoidUntil = Time.time + avoidDuration;
    }

    // === 범위 공격 구현 ===
    void DoAreaAttack()
    {
        // ★★★ [추가] 공격 이펙트 생성 ★★★
        if (areaAttackEffectPrefab != null)
        {
            // 보스 위치(transform.position)에 이펙트 생성
            Instantiate(areaAttackEffectPrefab, transform.position, UnityEngine.Quaternion.identity);
        }

        // 반경 내 '타겟 레이어'에 해당하는 모든 대상 탐색 (Buffer사용 권장이나, 공격은 빈도가 낮으므로 Alloc 함수도 허용)
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaAttackRadius, targetLayer);

        for (int i = 0; i < hits.Length; i++)
        {
            var t = hits[i].GetComponent<Targetable>();
            if (t == null || t.isDead) continue;

            t.TakeDamage(attackDamage, transform);
        }
    }

    // === 넉백 ===
    public void ApplyKnockback(UnityEngine.Vector2 knockbackDir, float power, float duration)
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        StartCoroutine(KnockbackRoutine(knockbackDir, power, duration));
    }

    private IEnumerator KnockbackRoutine(UnityEngine.Vector2 knockbackDir, float power, float duration)
    {
        if (myTargetable) myTargetable.IsKnockedBack = true; // [수정] 상태 동기화
        rigid.linearVelocity = knockbackDir * power;
        yield return new WaitForSeconds(duration);

        rigid.linearVelocity = UnityEngine.Vector2.zero;
        if (myTargetable) myTargetable.IsKnockedBack = false; // [수정] 상태 해제
    }

    public void slowDown(float x, float dur)
    {
        StartCoroutine(SlowDownFor(x, dur));

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