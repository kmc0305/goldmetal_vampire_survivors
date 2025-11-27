using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [아군 유닛] AI 추적 및 공격 로직을 담당합니다.
/// Targetable.cs (생명)과 Rigidbody2D (물리)에 의존합니다.
/// [최적화 적용] OverlapCircleNonAlloc, sqrMagnitude, UnityEngine 명시, 캐싱(Caching)
/// </summary>
public class AllyAI : MonoBehaviour
{
    [Header("기본 능력치")]
    public float speed = 2.5f;

    [Header("AI 설정")]
    public LayerMask targetLayer;       // 공격할 대상(적)의 레이어
    public float detectionRadius = 15f; // 탐지 반경

    [Header("AI 최적화 설정")]
    public float aiUpdateFrequency = 0.5f; // 타겟 갱신 주기(초)
    // [최적화] 탐색 결과를 담을 재사용 배열 (최대 20마리까지만 고려)
    private Collider2D[] scanBuffer = new Collider2D[20];

    [Header("공격 설정")]
    public float attackDamage = 1f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    // 내부 변수
    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Coroutine aiCoroutine;
    private Targetable currentTarget;

    // [최적화] 자주 쓰는 컴포넌트 캐싱 변수
    private UnitMover2D unitMover; // GetComponent 호출 줄이기
    private Transform myTf;        // transform 프로퍼티 접근 줄이기
    private Targetable myTargetable; // [수정] 넉백 상태 공유용

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();

        // [최적화] FixedUpdate에서 매번 찾지 않도록 미리 저장(Caching)
        unitMover = GetComponent<UnitMover2D>();
        myTargetable = GetComponent<Targetable>(); // [수정]
        myTf = transform;
    }

    void OnEnable()
    {
        if (myTargetable) myTargetable.IsKnockedBack = false;

        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutine());
    }

    void OnDisable()
    {
        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }

        currentTarget = null;
        rb.linearVelocity = UnityEngine.Vector2.zero;
    }

    IEnumerator UpdateTargetCoroutine()
    {
        // [최적화] 모든 유닛이 동시에 연산하지 않도록 시작 시 랜덤 딜레이 부여
        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, aiUpdateFrequency));

        while (gameObject.activeSelf)
        {
            // [수정] Targetable의 상태 확인
            if (myTargetable != null && !myTargetable.IsKnockedBack)
                currentTarget = FindClosestTarget();

            yield return new WaitForSeconds(aiUpdateFrequency);
        }
    }

    Targetable FindClosestTarget()
    {
        float closestDistSqr = float.MaxValue; // 거리 제곱 비교용
        Targetable best = null;

        // [최적화] NonAlloc 함수 사용으로 메모리 할당(Garbage) 방지
        // [수정] 경고 메시지(Obsolete) 무시 처리
#pragma warning disable 0618
        int count = Physics2D.OverlapCircleNonAlloc(myTf.position, detectionRadius, scanBuffer, targetLayer);
#pragma warning restore 0618

        for (int i = 0; i < count; i++)
        {
            Collider2D col = scanBuffer[i];
            Targetable t = col.GetComponent<Targetable>();

            if (t && !t.isDead)
            {
                UnityEngine.Vector3 myPos = myTf.position;
                UnityEngine.Vector3 targetPos = t.transform.position;
                float distSqr = (myPos - targetPos).sqrMagnitude;

                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    best = t;
                }
            }
        }
        return best;
    }

    void FixedUpdate()
    {
        // ✅ 이동 명령 중이면 AI 이동 멈춤
        if (unitMover && unitMover.HasCommand()) return;

        // ✅ 넉백 중이면 이동 정지
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

        if (currentTarget == null)
        {
            rb.linearVelocity = UnityEngine.Vector2.zero;
            return;
        }

        // 추적 이동
        UnityEngine.Vector2 dir = (currentTarget.transform.position - myTf.position).normalized;
        UnityEngine.Vector2 step = dir * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + step);
    }

    void LateUpdate()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null) return;

        spriter.flipX = currentTarget.transform.position.x < rb.position.x;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // 공격 처리
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null) return;
        if (Time.time < lastAttackTime + attackCooldown) return;

        if (collision.gameObject == currentTarget.gameObject)
        {
            currentTarget.TakeDamage(attackDamage, transform);
            lastAttackTime = Time.time;
        }
    }

    // ===========================================
    // ✅ 넉백 함수 (Targetable이 호출)
    // ===========================================
    public void ApplyKnockback(UnityEngine.Vector2 dir, float power, float duration)
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        StartCoroutine(KnockbackRoutine(dir, power, duration));
    }

    private IEnumerator KnockbackRoutine(UnityEngine.Vector2 dir, float power, float duration)
    {
        if (myTargetable) myTargetable.IsKnockedBack = true; // [수정] 상태 동기화
        rb.linearVelocity = dir.normalized * power;

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = UnityEngine.Vector2.zero;
        if (myTargetable) myTargetable.IsKnockedBack = false; // [수정] 상태 해제
    }
}