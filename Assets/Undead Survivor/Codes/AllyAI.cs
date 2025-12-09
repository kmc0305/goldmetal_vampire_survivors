using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using Random = UnityEngine.Random; // 명시적 사용이 더 안전

public class AllyAI : MonoBehaviour
{
    public static List<AllyAI> ActiveAllies = new List<AllyAI>();

    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float speedMultiplier = 1f;

    [Header("AI 설정")]
    public LayerMask targetLayer;
    public float baseDetectionRadius = 15f;
    public float wideDetectionRadius = 100f;
    public float castleSafeDistance = 20f;
    public float aiUpdateFrequency = 0.5f;

    private float giveUpRangeMultiplier = 1.3f;
    private float aiUpdateRandomDelay = 0.1f;

    [Header("공격 설정")]
    public float attackDamage = 1f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    private Rigidbody2D rb;
    private SpriteRenderer spriter;
    private Coroutine aiCoroutine;
    private Targetable currentTarget;
    private bool isKnockedBack = false;
    private Animator anim;
    private UnitMover2D mover;
    private Transform castleTransform;

    private bool isRecallMode = false;
    private Vector2 recallTargetPos;
    private float recallStopDistance = 1.5f;
    private float sqrRecallStopDistance; // 제곱 거리

    // [최적화] Animator Hash ID 캐싱 (문자열 비교 제거)
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashAttack = Animator.StringToHash("Attack");

    // [최적화] Animator SetFloat 호출 줄이기용
    private float lastAnimSpeedValue = -1f;

    // [최적화] Physics2D 버퍼
    private static readonly Collider2D[] targetBuffer = new Collider2D[20]; // 100 -> 20 (가장 가까운 몇 놈만 보면 됨)

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        mover = GetComponent<UnitMover2D>();
        sqrRecallStopDistance = recallStopDistance * recallStopDistance;
    }

    void Start()
    {
        GameObject castleObj = GameObject.FindGameObjectWithTag("Castle");
        if (castleObj != null) castleTransform = castleObj.transform;
    }

    void OnEnable()
    {
        isKnockedBack = false;
        isRecallMode = false;
        speedMultiplier = 1f;
        lastAnimSpeedValue = -1f; // 초기화

        if (!ActiveAllies.Contains(this)) ActiveAllies.Add(this);
        if (aiCoroutine == null) aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
        SetChildrenActive(true);
    }

    void OnDisable()
    {
        if (ActiveAllies.Contains(this)) ActiveAllies.Remove(this);
        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }
        currentTarget = null;
        SetChildrenActive(false);
    }

    private void SetChildrenActive(bool state)
    {
        foreach (Transform child in transform) child.gameObject.SetActive(state);
    }

    public void CommandMoveTo(Vector2 targetPos)
    {
        isRecallMode = true;
        recallTargetPos = targetPos;
        currentTarget = null;
    }

    // [최적화] 거리 제곱 사용
    float GetCurrentDetectionRadiusSqr()
    {
        float radius = baseDetectionRadius;
        if (castleTransform != null)
        {
            // sqrMagnitude 사용
            float sqrDistToCastle = (transform.position - castleTransform.position).sqrMagnitude;
            if (sqrDistToCastle > castleSafeDistance * castleSafeDistance)
            {
                radius = wideDetectionRadius;
            }
        }
        return radius * radius; // 제곱값 반환
    }

    IEnumerator UpdateTargetCoroutineDelayed()
    {
        // 로드 밸런싱: 프레임 분산
        yield return new WaitForSeconds(Random.Range(0f, aiUpdateFrequency));
        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        // [최적화] WaitForSeconds 객체 캐싱 (GC 감소) - 시간 변동이 적다면 사용 고려
        // 여기서는 랜덤 시간이라 매번 생성하지만, 큰 부하는 아님.

        while (gameObject.activeSelf)
        {
            if (!isRecallMode)
            {
                // 제곱된 반지름 가져오기
                float currentRadiusSqr = GetCurrentDetectionRadiusSqr();

                if (currentTarget != null)
                {
                    if (currentTarget.isDead || !currentTarget.gameObject.activeInHierarchy)
                    {
                        currentTarget = null;
                    }
                    else
                    {
                        float sqrDist = (transform.position - currentTarget.transform.position).sqrMagnitude;
                        // 추적 포기 거리 확인
                        float giveUpSqr = currentRadiusSqr * (giveUpRangeMultiplier * giveUpRangeMultiplier);

                        if (sqrDist > giveUpSqr)
                            currentTarget = null;
                    }
                }

                if (currentTarget == null)
                {
                    // 제곱근 계산이 필요한지 확인 (OverlapCircle은 반지름 필요)
                    // Mathf.Sqrt는 비용이 좀 있지만 0.5초에 한번은 괜찮음.
                    // 더 최적화 하려면 OverlapCircle 대신 직접 거리 계산 루프를 돌려야하는데 코드가 복잡해짐.
                    currentTarget = FindClosestTarget(Mathf.Sqrt(currentRadiusSqr));
                }
            }

            float waitTime = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            yield return new WaitForSeconds(Mathf.Max(0.1f, waitTime));
        }
    }

    Targetable FindClosestTarget(float radius)
    {
        float closestSqr = float.MaxValue;
        Targetable best = null;

        // 레이어 마스크 필터링으로 1차 최적화
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, radius, targetBuffer, targetLayer);

        Vector2 myPos = transform.position;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = targetBuffer[i];
            // [최적화] GetComponent는 무거움. 태그나 레이어로 먼저 거를 수 있으면 좋음.
            // 여기선 이미 targetLayer로 걸렀으니 바로 접근.
            Targetable t = col.GetComponent<Targetable>();

            if (t != null && !t.isDead)
            {
                float sqrDist = (myPos - (Vector2)col.transform.position).sqrMagnitude;
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
        if (mover != null && mover.HasCommand())
        {
            UpdateAnimSpeed(0f);
            return;
        }

        if (isKnockedBack) return;

        float currentMoveSpeed = speed * speedMultiplier;

        if (isRecallMode)
        {
            float sqrDist = ((Vector2)transform.position - recallTargetPos).sqrMagnitude;
            if (sqrDist <= sqrRecallStopDistance)
            {
                isRecallMode = false;
                rb.linearVelocity = Vector2.zero;
                UpdateAnimSpeed(0f);
            }
            else
            {
                Vector2 dir = (recallTargetPos - (Vector2)transform.position).normalized;
                Vector2 step = dir * (currentMoveSpeed * 1.2f) * Time.fixedDeltaTime;
                rb.MovePosition(rb.position + step);
                // 리콜 중엔 보통 걷는 애니메이션
                UpdateAnimSpeed(currentMoveSpeed);
            }
            return;
        }

        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateAnimSpeed(0f);
            return;
        }

        Vector2 dir2 = (currentTarget.transform.position - transform.position).normalized;
        Vector2 step2 = dir2 * currentMoveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + step2);

        UpdateAnimSpeed(step2.magnitude); // 여기선 정확한 속도값 표현을 위해 magnitude 사용
    }

    // [최적화] Animator 파라미터가 실제로 크게 변했을 때만 호출
    void UpdateAnimSpeed(float newSpeed)
    {
        if (Mathf.Abs(lastAnimSpeedValue - newSpeed) > 0.01f)
        {
            anim.SetFloat(HashSpeed, newSpeed);
            lastAnimSpeedValue = newSpeed;
        }
    }

    void LateUpdate()
    {
        if (isKnockedBack) return;
        if (isRecallMode)
        {
            // 단순 float 비교
            spriter.flipX = recallTargetPos.x > transform.position.x;
            return;
        }
        if (currentTarget != null)
        {
            spriter.flipX = currentTarget.transform.position.x > transform.position.x;
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (isKnockedBack || isRecallMode || currentTarget == null) return;

        if (Time.time < lastAttackTime + attackCooldown) return;

        if (collision.gameObject == currentTarget.gameObject)
        {
            anim.SetTrigger(HashAttack); // Hash ID 사용
            currentTarget.TakeDamage(attackDamage, transform);
            lastAttackTime = Time.time;
        }
    }

    // 넉백 로직 등 나머지는 그대로 유지...
    public void ApplyKnockback(Vector2 dir, float power, float duration)
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(dir, power, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir, float power, float duration)
    {
        isKnockedBack = true;
        isRecallMode = false;
        rb.linearVelocity = dir.normalized * power;
        UpdateAnimSpeed(0f);
        yield return new WaitForSeconds(duration);
        rb.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }

    public void SetSpeedMultiplier(float multiplier) => speedMultiplier = multiplier;

    // Gizmos 코드는 빌드에 포함 안 되므로 최적화 불필요
}