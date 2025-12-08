using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
// using Vector2 = UnityEngine.Vector2; // Ambiguity 방지를 위해 이 using을 제거하고, 대신 코드 전체에서 'UnityEngine.Vector2'를 명시적으로 사용합니다.

public class AllyAI : MonoBehaviour
{
    // [한국어 주석] 아군 유닛의 맵 아이콘 등 추적에 사용되는 정적 리스트 (Static List)
    public static List<AllyAI> ActiveAllies = new List<AllyAI>();

    [Header("기본 능력치")]
    public float speed = 2.5f;
    // [추가] 늪지대/외부 효과를 위한 속도 배율
    public float speedMultiplier = 1f;

    [Header("AI 설정")]
    public LayerMask targetLayer;
    public float baseDetectionRadius = 15f; // 기본(성 근처) 탐지 거리
    public float wideDetectionRadius = 100f; // 성 밖 탐지 거리
    public float castleSafeDistance = 20f;   // 성으로부터의 기준 거리 (이 거리 안이면 base, 밖이면 wide)

    public float aiUpdateFrequency = 0.5f;

    // 추적 포기 거리 배수
    private float giveUpRangeMultiplier = 1.3f;
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
    private Animator anim;
    private UnitMover2D mover; // [최적화] GetComponent 캐싱

    // 성(Castle) 참조 변수 추가
    private Transform castleTransform;

    // 집결(리콜) 모드
    private bool isRecallMode = false;
    private UnityEngine.Vector2 recallTargetPos; // UnityEngine.Vector2 명시
    private float recallStopDistance = 1.5f;

    // [최적화] Physics2D 버퍼 재사용 (GC 방지)
    private static readonly Collider2D[] targetBuffer = new Collider2D[100];

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        mover = GetComponent<UnitMover2D>(); // [최적화] 캐싱
    }

    void Start()
    {
        // 씬에서 "Castle" 태그를 가진 오브젝트를 찾음 (성의 태그를 꼭 Castle로 설정해줘!)
        GameObject castleObj = UnityEngine.GameObject.FindGameObjectWithTag("Castle");
        if (castleObj != null)
        {
            castleTransform = castleObj.transform;
        }
        else
        {
            // [수정] Debug 모호성 해결
            UnityEngine.Debug.LogWarning("AllyAI: 'Castle' 태그를 가진 오브젝트를 찾을 수 없어! 기본 탐지 거리만 사용됨.");
        }
    }

    void OnEnable()
    {
        isKnockedBack = false;
        isRecallMode = false;
        // [수정] 활성화 시 속도 배율 초기화
        speedMultiplier = 1f;

        // ★ [보강] ActiveAllies에 이미 포함되어 있지 않은지 확인 후 추가 (혹시 모를 중복 방지)
        if (!ActiveAllies.Contains(this))
        {
            ActiveAllies.Add(this);
        }

        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());

        // ★ [추가] 오브젝트가 재활용될 때 자식 오브젝트들을 다시 활성화합니다.
        // 유닛이 활성화될 때 자식 오브젝트도 시각적으로 보여야 합니다.
        SetChildrenActive(true);
    }

    void OnDisable()
    {
        // ★ [보강] 리스트에서 확실하게 제거
        if (ActiveAllies.Contains(this))
        {
            ActiveAllies.Remove(this);
        }

        // ★ [보강] OnDisable이 호출될 때 코루틴이 실행 중일 수 있으니 확실하게 중지합니다.
        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }
        currentTarget = null;
        if (rb != null) rb.linearVelocity = UnityEngine.Vector2.zero; // UnityEngine.Vector2 명시

        // ★ [핵심 수정] 오브젝트가 비활성화(풀로 반환)될 때 모든 자식 오브젝트를 명시적으로 비활성화합니다.
        // MiniMap Icon이나 Shadow처럼 분리되어 남아있는 자식들을 정리합니다.
        SetChildrenActive(false);
    }

    // ★ [추가] 모든 자식 오브젝트의 활성화 상태를 제어하는 헬퍼 함수
    private void SetChildrenActive(bool state)
    {
        // 부모 오브젝트의 자식들을 순회합니다.
        foreach (Transform child in transform)
        {
            // MiniMap Icon이나 Shadow가 자식으로 있다면, 해당 자식 오브젝트를 활성화/비활성화합니다.
            child.gameObject.SetActive(state);
        }
    }


    public void CommandMoveTo(UnityEngine.Vector2 targetPos) // UnityEngine.Vector2 명시
    {
        isRecallMode = true;
        recallTargetPos = targetPos;
        currentTarget = null;
    }

    // 현재 상황에 맞는 탐지 거리 계산 함수
    float GetCurrentDetectionRadius()
    {
        // 성을 못 찾았으면 기본값 반환
        if (castleTransform == null) return baseDetectionRadius;

        float distToCastle = UnityEngine.Vector2.Distance(transform.position, castleTransform.position); // UnityEngine.Vector2 명시

        // 성과의 거리가 20 이하(내부)면 15, 아니면 100 반환
        if (distToCastle <= castleSafeDistance)
        {
            return baseDetectionRadius;
        }
        else
        {
            return wideDetectionRadius;
        }
    }

    IEnumerator UpdateTargetCoroutineDelayed()
    {
        float initialDelay = Random.Range(0f, aiUpdateFrequency);
        yield return new UnityEngine.WaitForSeconds(initialDelay);
        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            if (!isRecallMode)
            {
                // 현재 적용되어야 할 탐지 거리 가져오기
                float currentRadius = GetCurrentDetectionRadius();

                if (currentTarget != null)
                {
                    if (currentTarget.isDead)
                    {
                        currentTarget = null;
                    }
                    else
                    {
                        float dist = UnityEngine.Vector2.Distance(transform.position, currentTarget.transform.position); // UnityEngine.Vector2 명시
                        // 추적 포기 거리도 현재 탐지 거리에 비례해서 계산
                        if (dist > currentRadius * giveUpRangeMultiplier)
                            currentTarget = null;
                    }
                }

                if (currentTarget == null)
                {
                    currentTarget = FindClosestTarget(currentRadius);
                }
            }

            float waitTime = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (waitTime < 0.1f) waitTime = 0.1f;
            yield return new UnityEngine.WaitForSeconds(waitTime);
        }
    }

    // [최적화] 인자로 탐지 범위를 받도록 수정 - 버퍼 재사용으로 GC 방지
    Targetable FindClosestTarget(float radius)
    {
        float closestSqr = float.MaxValue;
        Targetable best = null;

        // [최적화] 정적 버퍼 사용으로 매 호출마다 배열 할당 방지
        int hitCount = UnityEngine.Physics2D.OverlapCircleNonAlloc(transform.position, radius, targetBuffer, targetLayer); // UnityEngine.Physics2D 명시
        for (int i = 0; i < hitCount; i++)
        {
            UnityEngine.Collider2D col = targetBuffer[i]; // UnityEngine.Collider2D 명시
            Targetable t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                // [최적화] sqrMagnitude 사용으로 sqrt 연산 제거
                float sqrDist = ((UnityEngine.Vector2)transform.position - (UnityEngine.Vector2)col.transform.position).sqrMagnitude; // UnityEngine.Vector2 명시
                if (sqrDist < closestSqr)
                {
                    closestSqr = sqrDist;
                    best = t;
                }
            }
        }
        return best;
    }

    void FixedUpdate()
    {
        // [최적화] 캐싱된 mover 사용
        if (mover != null && mover.HasCommand())
        {
            anim.SetFloat("Speed", 0f);
            return;
        }

        if (isKnockedBack) return;

        // [추가] 실제 이동 속도 계산 (기본 속도 * 속도 배율)
        float currentMoveSpeed = speed * speedMultiplier;

        if (isRecallMode)
        {
            float dist = UnityEngine.Vector2.Distance(transform.position, recallTargetPos); // UnityEngine.Vector2 명시
            if (dist <= recallStopDistance)
            {
                isRecallMode = false;
                rb.linearVelocity = UnityEngine.Vector2.zero; // UnityEngine.Vector2 명시
            }
            else
            {
                // [수정] 이동 속도에 speedMultiplier 적용
                UnityEngine.Vector2 dir = (recallTargetPos - (UnityEngine.Vector2)transform.position).normalized; // UnityEngine.Vector2 명시
                UnityEngine.Vector2 step = dir * (currentMoveSpeed * 1.2f) * UnityEngine.Time.fixedDeltaTime; // UnityEngine.Time 명시
                rb.MovePosition(rb.position + step);
            }
            return;
        }

        if (currentTarget == null)
        {
            rb.linearVelocity = UnityEngine.Vector2.zero; // UnityEngine.Vector2 명시
            anim.SetFloat("Speed", 0f);
            return;
        }

        // [수정] 이동 속도에 speedMultiplier 적용
        UnityEngine.Vector2 dir2 = (currentTarget.transform.position - transform.position).normalized; // UnityEngine.Vector2 명시
        UnityEngine.Vector2 step2 = dir2 * currentMoveSpeed * UnityEngine.Time.fixedDeltaTime; // UnityEngine.Time 명시
        rb.MovePosition(rb.position + step2);

        anim.SetFloat("Speed", step2.magnitude);
    }

    void LateUpdate()
    {
        if (isKnockedBack) return;

        if (isRecallMode)
        {
            spriter.flipX = recallTargetPos.x > rb.position.x;
            return;
        }

        if (currentTarget == null) return;
        spriter.flipX = currentTarget.transform.position.x > rb.position.x;
    }

    void OnCollisionStay2D(UnityEngine.Collision2D collision) // UnityEngine.Collision2D 명시
    {
        if (isKnockedBack) return;
        if (isRecallMode) return;
        if (currentTarget == null) return;

        if (UnityEngine.Time.time < lastAttackTime + attackCooldown) return; // UnityEngine.Time 명시

        if (collision.gameObject == currentTarget.gameObject)
        {
            anim.SetTrigger("Attack");
            currentTarget.TakeDamage(attackDamage, transform);
            lastAttackTime = UnityEngine.Time.time; // UnityEngine.Time 명시
        }
    }

    public void ApplyKnockback(UnityEngine.Vector2 dir, float power, float duration) // UnityEngine.Vector2 명시
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(dir, power, duration));
    }

    private IEnumerator KnockbackRoutine(UnityEngine.Vector2 dir, float power, float duration) // UnityEngine.Vector2 명시
    {
        isKnockedBack = true;
        isRecallMode = false;
        rb.linearVelocity = dir.normalized * power;
        anim.SetFloat("Speed", 0f);
        yield return new UnityEngine.WaitForSeconds(duration); // UnityEngine.WaitForSeconds 명시
        rb.linearVelocity = UnityEngine.Vector2.zero; // UnityEngine.Vector2 명시
        isKnockedBack = false;
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

    void OnDrawGizmosSelected()
    {
        // 기즈모에서도 현재 상태에 따라 범위를 보여줌
        float currentRadius = baseDetectionRadius;

        // 에디터 모드에서는 castleTransform이 연결 안 되어 있을 수 있으니 태그로 임시 찾기 (선택사항)
        if (UnityEngine.Application.isPlaying && castleTransform != null) // UnityEngine.Application 명시
        {
            float distToCastle = UnityEngine.Vector2.Distance(transform.position, castleTransform.position); // UnityEngine.Vector2 명시
            if (distToCastle > castleSafeDistance) currentRadius = wideDetectionRadius;
        }

        UnityEngine.Gizmos.color = UnityEngine.Color.cyan; // UnityEngine.Gizmos 명시
        UnityEngine.Gizmos.DrawWireSphere(transform.position, currentRadius); // UnityEngine.Gizmos 명시

        UnityEngine.Gizmos.color = UnityEngine.Color.yellow; // UnityEngine.Gizmos 명시
        UnityEngine.Gizmos.DrawWireSphere(transform.position, currentRadius * giveUpRangeMultiplier); // UnityEngine.Gizmos 명시

        // 성 안전 거리도 빨간색으로 살짝 표시 (참고용)
        if (castleTransform != null)
        {
            UnityEngine.Gizmos.color = UnityEngine.Color.red; // UnityEngine.Gizmos 명시
            UnityEngine.Gizmos.DrawWireSphere(castleTransform.position, castleSafeDistance); // UnityEngine.Gizmos 명시
        }
    }
}