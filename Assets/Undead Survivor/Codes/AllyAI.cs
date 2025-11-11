using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [아군 유닛] AI 추적 및 공격 로직을 담당합니다. (Enemy.cs와 동일한 구조)
/// Targetable.cs (생명)과 Rigidbody2D (물리)에 의존합니다.
/// </summary>
public class AllyAI : MonoBehaviour
{
    [Header("기본 능력치")]
    public float speed = 2.5f;

    [Header("AI 설정")]
    /// <summary>공격할 대상의 레이어 (인스펙터에서 'Enemy'로 설정해야 함)</summary>
    public LayerMask targetLayer;
    /// <summary>적을 감지할 수 있는 최대 반경</summary>
    public float detectionRadius = 15f;
    /// <summary>AI가 새로운 타겟을 탐색하는 주기 (초). 너무 낮으면 성능 저하, 높으면 반응이 느려짐.</summary>
    private float aiUpdateFrequency = 0.3f;

    [Header("공격 설정")]
    /// <summary>'몸통 박치기' 공격의 데미지</summary>
    public float attackDamage = 5f;
    /// <summary>공격 주기 (초). 1f = 1초에 한 번 공격</summary>
    public float attackCooldown = 1f;
    /// <summary>마지막으로 공격한 시간을 저장 (쿨다운 계산용)</summary>
    private float lastAttackTime;

    // 컴포넌트 참조 (성능을 위해 Awake에서 미리 캐싱)
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    /// <summary>AI 타겟 탐색 코루틴(Coroutine)을 제어하기 위한 변수</summary>
    private Coroutine aiCoroutine;

    // 타겟 관련
    /// <summary>현재 추적 중인 대상 (Targetable 컴포넌트)</summary>
    private Targetable currentTarget;

    /// <summary>현재 넉백 상태인지 여부. true이면 AI 이동/공격/타겟팅이 모두 중지됩니다.</summary>
    private bool isKnockedBack = false;

    // =====================================================================
    // ★★★ [신규] 같은 진영 성(타워) 접촉 시 '접선 회피' 기능
    // =====================================================================
    [Header("Friendly Tower Avoidance (접선 회피)")]
    public float avoidDuration = 0.3f;     // 회피 지속 시간(초)
    public float avoidSpeedMul = 1.5f;     // 회피 중 속도 배수
    public bool randomizeTangent = true;  // true면 시계/반시계 방향을 랜덤 선택

    private float avoidUntil = 0f;         // 회피 종료 시각 (Time.time 기준)
    private Vector2 avoidDir = Vector2.zero; // 회피 이동 방향(접선)

    // 같은 진영 성 판별: Layer 동일 + SpawnPoint(적성) / AllySpawner(아군성) 중 하나라도 붙어 있으면 '성'
    bool IsFriendlyTower(GameObject other)
    {
        if (other.layer != gameObject.layer) return false;
        // 프로젝트 구조에 맞춘 '성' 마커 컴포넌트
        bool hasTowerComponent =
              other.GetComponent<SpawnPoint>() != null
           || other.GetComponent<AllySpawner>() != null;
        return hasTowerComponent;
    }
    // =====================================================================

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
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
        rigid.linearVelocity = Vector2.zero;
        // 회피 상태 리셋
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

        Collider2D[] targetsInView = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);

        foreach (Collider2D collider in targetsInView)
        {
            Targetable potentialTarget = collider.GetComponent<Targetable>();
            if (potentialTarget != null && !potentialTarget.isDead)
            {
                float distance = Vector3.Distance(transform.position, potentialTarget.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestTarget = potentialTarget;
                }
            }
        }
        return bestTarget;
    }

    void FixedUpdate()
    {
        // 넉백 중이면 이동 정지
        if (isKnockedBack) return;

        // ★★★ 회피 중이면 접선 방향으로 우선 이동
        if (Time.time < avoidUntil && avoidDir.sqrMagnitude > 0.0001f)
        {
            Vector2 step = avoidDir.normalized * speed * avoidSpeedMul * Time.fixedDeltaTime;
            rigid.MovePosition(rigid.position + step);
            rigid.linearVelocity = Vector2.zero;
            return; // 일반 추적 로직 잠시 중단
        }

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
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // 공격 처리 (기존)
        if (currentTarget != null && Time.time >= lastAttackTime + attackCooldown && !isKnockedBack)
        {
            if (collision.gameObject == currentTarget.gameObject)
            {
                currentTarget.TakeDamage(attackDamage, transform);
                lastAttackTime = Time.time;
            }
        }
    }

    // =====================================================================
    // ★★★ 접선 회피 트리거: 같은 진영 성과의 충돌 이벤트에서 방향 설정
    // =====================================================================
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsFriendlyTower(collision.gameObject) && collision.contactCount > 0)
        {
            // 접촉점 법선(성 표면에서 '밖'으로 향함)
            Vector2 n = collision.GetContact(0).normal;

            // 접선(시계/반시계 중 선택)
            Vector2 tangentCW = new Vector2(-n.y, n.x);
            Vector2 tangentCCW = new Vector2(n.y, -n.x);

            Vector2 tangent = randomizeTangent
                ? (Random.value > 0.5f ? tangentCW : tangentCCW)
                : tangentCW; // 기본은 시계방향

            avoidDir = tangent.normalized;
            avoidUntil = Time.time + avoidDuration;
        }
    }

    void OnCollisionStay2D_ExtensionForAvoid(Collision2D collision) // 호출용 별도 함수로 빼고 싶을 때 사용 가능
    {
        if (IsFriendlyTower(collision.gameObject) && collision.contactCount > 0)
        {
            Vector2 n = collision.GetContact(0).normal;
            Vector2 tangentCW = new Vector2(-n.y, n.x);
            Vector2 tangentCCW = new Vector2(n.y, -n.x);
            Vector2 tangent = randomizeTangent
                ? (Random.value > 0.5f ? tangentCW : tangentCCW)
                : tangentCW;

            avoidDir = tangent.normalized;

            // 떨림 방지: 회피 잔여 시간이 너무 짧으면 살짝 연장
            if (avoidUntil < Time.time + avoidDuration * 0.5f)
                avoidUntil = Time.time + avoidDuration * 0.5f;
        }
    }

    // Unity는 동일 시그니처의 OnCollisionStay2D가 하나만 호출되므로,
    // 위 확장 함수를 실제 OnCollisionStay2D에서 호출해 준다.
    void OnCollisionStay2D_AvoidBridge(Collision2D collision) { OnCollisionStay2D_ExtensionForAvoid(collision); }
    void OnCollisionEnter2D_AvoidBridge(Collision2D collision) { /* editor helper (optional) */ }
    // =====================================================================

    // -----------------------------------------------------------------
    // ★★★ 넉백 수신 함수
    // -----------------------------------------------------------------
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
}
