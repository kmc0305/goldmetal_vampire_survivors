using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random; // 모호함 방지
using Vector2 = UnityEngine.Vector2; // ✅ Vector2 모호함 방지 명시

/// <summary>
/// [아군 유닛] AI 추적 및 공격 로직을 담당합니다.
/// Targetable.cs (생명)과 Rigidbody2D (물리)에 의존합니다.
/// [최적화 적용됨]: 코루틴 시작 지연 및 갱신 주기 랜덤화
/// [수정됨]: 적이 탐지 범위를 벗어나면 추적을 포기하고 멈추는 기능 추가
/// [최종 추가]: 플레이어 위치로 집결하는 기능 (Static List 활용)
/// </summary>
public class AllyAI : MonoBehaviour
{
    // ✅ [최적화] 활성화된 모든 아군 유닛을 관리하는 정적 리스트 (전역 접근 가능)
    public static List<AllyAI> ActiveAllies = new List<AllyAI>();

    [Header("기본 능력치")]
    public float speed = 2.5f;

    [Header("AI 설정")]
    public LayerMask targetLayer;       // 공격할 대상(적)의 레이어
    public float detectionRadius = 5f; // [추천] 좁은 범위 수비를 원하시면 이 값을 3~5로 줄이세요.
    public float aiUpdateFrequency = 0.5f; // 타겟 갱신 주기(초)

    // ✅ [추가] 추적 포기 거리 비율 (탐지 반경의 1.3배만큼 멀어지면 추적 중단)
    private float giveUpRangeMultiplier = 1.3f;

    // ✅ [최적화] 타겟 갱신 주기에 줄 랜덤 지연 시간
    private float aiUpdateRandomDelay = 0.1f;

    [Header("공격 설정")]
    public float attackDamage = 1f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    // 내부 변수
    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Coroutine aiCoroutine;
    private Targetable currentTarget;
    private bool isKnockedBack = false;

    // ✅ [추가] 집결(리콜) 모드 관련 변수
    private bool isRecallMode = false;
    private Vector2 recallTargetPos;
    private float recallStopDistance = 1.5f; // 목표 지점 도달 판정 거리

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        isKnockedBack = false;
        isRecallMode = false;

        // ✅ 리스트에 자신 등록
        ActiveAllies.Add(this);

        // ✅ [최적화] 유닛 활성화 시, 즉시 AI를 켜지 않고 랜덤하게 지연시켜 부하를 분산합니다.
        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
    }

    void OnDisable()
    {
        // ✅ 리스트에서 자신 제거
        ActiveAllies.Remove(this);

        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }

        currentTarget = null;
        rb.linearVelocity = Vector2.zero;
    }

    // ==================================================================================
    // ✅ [기능 추가] 외부(Player)에서 호출하는 이동 명령 함수
    // ==================================================================================
    public void CommandMoveTo(Vector2 targetPos)
    {
        isRecallMode = true;
        recallTargetPos = targetPos;
        currentTarget = null; // 이동 중에는 적 추적 중지

        // 시각적 피드백이 필요하다면 여기에 추가 (예: 느낌표 이펙트)
    }

    // ✅ [최적화] 초기 실행 지연 코루틴
    IEnumerator UpdateTargetCoroutineDelayed()
    {
        // 0초에서 갱신 주기 사이의 랜덤한 시간만큼 대기 후 시작
        float initialDelay = Random.Range(0f, aiUpdateFrequency);
        yield return new WaitForSeconds(initialDelay);

        // 실제 루프 시작
        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            // ✅ [최적화] 집결 모드일 때는 적 탐색 로직을 건너뜀 (성능 절약)
            if (isRecallMode)
            {
                yield return new WaitForSeconds(0.2f); // 짧게 대기하며 상태 확인
                continue;
            }

            // 1. 현재 타겟 상태 확인
            if (currentTarget != null)
            {
                // 타겟이 죽었으면 해제
                if (currentTarget.isDead)
                {
                    currentTarget = null;
                }
                // ✅ [기능 추가] 타겟이 너무 멀어졌는지 확인 (추적 포기 로직)
                else
                {
                    float dist = Vector2.Distance(transform.position, currentTarget.transform.position);
                    // 적이 (탐지반경 * 1.3배) 밖으로 나가면 추적을 멈춥니다.
                    if (dist > detectionRadius * giveUpRangeMultiplier)
                    {
                        currentTarget = null; // 타겟 해제 -> FixedUpdate에서 멈춤 처리됨
                    }
                }
            }

            // 2. 타겟이 없으면 새로 탐색 (탐지 반경 안에서만)
            if (currentTarget == null)
            {
                currentTarget = FindClosestTarget();
            }

            // 3. 다음 갱신까지 대기 (Jitter 적용)
            float waitTime = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (waitTime < 0.1f) waitTime = 0.1f;

            yield return new WaitForSeconds(waitTime);
        }
    }

    Targetable FindClosestTarget()
    {
        float closestDist = float.MaxValue;
        Targetable bestTarget = null;

        // 내 주변 detectionRadius 반경 내의 콜라이더만 가져옵니다.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);

        foreach (Collider2D col in hits)
        {
            Targetable t = col.GetComponent<Targetable>();
            // 타겟이 존재하고 죽지 않았을 때만
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
        // 넉백 중이면 이동 불가
        if (isKnockedBack) return;

        // ✅ [기능 추가] 집결 모드 이동 로직
        if (isRecallMode)
        {
            float dist = Vector2.Distance(transform.position, recallTargetPos);

            // 목표 지점에 거의 도착했으면 집결 모드 해제 (다시 AI 작동)
            if (dist <= recallStopDistance)
            {
                isRecallMode = false;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                // 목표 지점으로 이동
                Vector2 dir = (recallTargetPos - (Vector2)transform.position).normalized;

                // 서로 너무 겹치지 않게 약간의 랜덤성을 주거나 회피력을 줄 수 있음 (선택사항)
                // 여기서는 단순 이동
                Vector2 step = dir * speed * 1.2f * Time.fixedDeltaTime; // 복귀할 땐 1.2배 속도로
                rb.MovePosition(rb.position + step);
            }
            return;
        }

        // 타겟이 없으면 정지 (가만히 있음)
        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 타겟 방향으로 이동
        Vector2 targetDir = (currentTarget.transform.position - transform.position).normalized;
        Vector2 moveStep = targetDir * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + moveStep);
    }

    void LateUpdate()
    {
        if (isKnockedBack) return;

        // ✅ 집결 모드일 때도 바라보는 방향 처리
        if (isRecallMode)
        {
            spriter.flipX = recallTargetPos.x < rb.position.x;
            return;
        }

        if (currentTarget == null) return;

        // 타겟 위치에 따라 스프라이트 좌우 반전
        spriter.flipX = currentTarget.transform.position.x < rb.position.x;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // 공격 처리
        if (isKnockedBack) return;
        if (isRecallMode) return; // ✅ 집결 중에는 공격 안 함 (무시하고 이동)
        if (currentTarget == null) return;

        // 공격 쿨타임 체크
        if (Time.time < lastAttackTime + attackCooldown) return;

        if (collision.gameObject == currentTarget.gameObject)
        {
            currentTarget.TakeDamage(attackDamage, transform);
            lastAttackTime = Time.time;
        }
    }

    public void ApplyKnockback(Vector2 dir, float power, float duration)
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(dir, power, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir, float power, float duration)
    {
        isKnockedBack = true;
        isRecallMode = false; // 넉백 당하면 집결 모드도 풀리는 게 자연스러움
        rb.linearVelocity = dir.normalized * power;

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }

    // 에디터에서 탐지 범위를 눈으로 확인하기 위한 기즈모
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 추적 포기 범위 (노란색)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius * giveUpRangeMultiplier);
    }
}