using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [아군 유닛] AI 추적 및 공격 로직을 담당합니다.
/// Targetable.cs (생명)과 Rigidbody2D (물리)에 의존합니다.
/// </summary>
public class AllyAI : MonoBehaviour
{
    [Header("기본 능력치")]
    public float speed = 2.5f;

    [Header("AI 설정")]
    public LayerMask targetLayer;       // 공격할 대상(적)의 레이어
    public float detectionRadius = 15f; // 탐지 반경
    public float aiUpdateFrequency = 0.5f; // 타겟 갱신 주기(초)

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        isKnockedBack = false;

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
        rb.linearVelocity = Vector2.zero;
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            if (!isKnockedBack)
                currentTarget = FindClosestTarget();

            yield return new WaitForSeconds(aiUpdateFrequency);
        }
    }

    Targetable FindClosestTarget()
    {
        float closest = float.MaxValue;
        Targetable best = null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);
        foreach (Collider2D col in hits)
        {
            Targetable t = col.GetComponent<Targetable>();
            if (t && !t.isDead)
            {
                float dist = Vector2.Distance(transform.position, t.transform.position);
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
        // ✅ 이동 명령 중이면 AI 이동 멈춤 (UnitMover2D와 연동)
        var mover = GetComponent<UnitMover2D>();
        if (mover && mover.HasCommand()) return;

        // ✅ 넉백 중이면 이동 정지
        if (isKnockedBack) return;

        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 추적 이동
        Vector2 dir = (currentTarget.transform.position - transform.position).normalized;
        Vector2 step = dir * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + step);
    }

    void LateUpdate()
    {
        if (isKnockedBack) return;
        if (currentTarget == null) return;

        spriter.flipX = currentTarget.transform.position.x < rb.position.x;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // 공격 처리
        if (isKnockedBack) return;
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
    public void ApplyKnockback(Vector2 dir, float power, float duration)
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(dir, power, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir, float power, float duration)
    {
        isKnockedBack = true;
        rb.linearVelocity = dir.normalized * power;

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }
}
