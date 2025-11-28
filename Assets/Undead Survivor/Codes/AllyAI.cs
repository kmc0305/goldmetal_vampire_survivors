using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;

public class AllyAI : MonoBehaviour
{
    // 활성화된 모든 아군 유닛 전역 리스트
    public static List<AllyAI> ActiveAllies = new List<AllyAI>();

    [Header("기본 능력치")]
    public float speed = 2.5f;

    [Header("AI 설정")]
    public LayerMask targetLayer;       // 공격할 대상(적)의 레이어
    public float detectionRadius = 15f; // 기본은 15f (너 작업 버전 유지)
    public float aiUpdateFrequency = 0.5f;

    // 추적 포기 거리 = 탐지 반경 x 1.3
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

    // 집결(리콜) 모드
    private bool isRecallMode = false;
    private Vector2 recallTargetPos;
    private float recallStopDistance = 1.5f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    void OnEnable()
    {
        isKnockedBack = false;
        isRecallMode = false;

        ActiveAllies.Add(this);

        // 초기 AI 시작은 랜덤 딜레이 후
        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
    }

    void OnDisable()
    {
        ActiveAllies.Remove(this);

        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }

        currentTarget = null;
        rb.linearVelocity = Vector2.zero;
    }

    // 플레이어 명령 집결
    public void CommandMoveTo(Vector2 targetPos)
    {
        isRecallMode = true;
        recallTargetPos = targetPos;
        currentTarget = null;
    }

    IEnumerator UpdateTargetCoroutineDelayed()
    {
        float initialDelay = Random.Range(0f, aiUpdateFrequency);
        yield return new WaitForSeconds(initialDelay);

        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            if (!isRecallMode)
            {
                // 타겟이 있다면 상태 체크
                if (currentTarget != null)
                {
                    if (currentTarget.isDead)
                    {
                        currentTarget = null;
                    }
                    else
                    {
                        float dist = Vector2.Distance(transform.position, currentTarget.transform.position);
                        if (dist > detectionRadius * giveUpRangeMultiplier)
                            currentTarget = null;
                    }
                }

                // 타겟 없으면 탐색
                if (currentTarget == null)
                {
                    currentTarget = FindClosestTarget();
                }
            }

            float waitTime = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (waitTime < 0.1f) waitTime = 0.1f;
            yield return new WaitForSeconds(waitTime);
        }
    }

    Targetable FindClosestTarget()
    {
        float closest = float.MaxValue;
        Targetable best = null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);
        foreach (var col in hits)
        {
            Targetable t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                float dist = Vector2.Distance(transform.position, col.transform.position);
                if (dist < closest)
                {
                    closest = dist;
                    best = t;
                }
            }
        }
        return best;
    }

    void FixedUpdate()
    {
        // 플레이어가 선택해서 이동 시키는 중이면 AI 멈춤
        var mover = GetComponent<UnitMover2D>();
        if (mover && mover.HasCommand())
        {
            anim.SetFloat("Speed", 0f);
            return;
        }

        // 넉백 중이면 이동 불가
        if (isKnockedBack) return;

        // 집결 모드 이동
        if (isRecallMode)
        {
            float dist = Vector2.Distance(transform.position, recallTargetPos);

            if (dist <= recallStopDistance)
            {
                isRecallMode = false;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                Vector2 dir = (recallTargetPos - (Vector2)transform.position).normalized;
                Vector2 step = dir * speed * 1.2f * Time.fixedDeltaTime;

                rb.MovePosition(rb.position + step);
            }
            return;
        }

        // 일반 AI 이동
        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            anim.SetFloat("Speed", 0f);
            return;
        }

        Vector2 dir2 = (currentTarget.transform.position - transform.position).normalized;
        Vector2 step2 = dir2 * speed * Time.fixedDeltaTime;
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

    void OnCollisionStay2D(Collision2D collision)
    {
        if (isKnockedBack) return;
        if (isRecallMode) return;
        if (currentTarget == null) return;

        if (Time.time < lastAttackTime + attackCooldown) return;

        if (collision.gameObject == currentTarget.gameObject)
        {
            anim.SetTrigger("Attack");

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
        isRecallMode = false;

        rb.linearVelocity = dir.normalized * power;
        anim.SetFloat("Speed", 0f);

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }

    // 기즈모 표시
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius * giveUpRangeMultiplier);
    }
}
