using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMover2D : MonoBehaviour
{
    [Header("Move Settings")]
    public float moveSpeed = 3.5f;
    public float stopDistance = 0.15f;
    public bool faceMoveDirection = true;

    // 지형 효과 배율
    private float speedMultiplier = 1.0f;

    [Header("Obstacle Avoidance")]
    public LayerMask towerObstacleMask;
    public float colliderRadius = 0.3f;
    public float avoidMargin = 0.2f;

    [Header("Optimization (Layer Switching)")]
    [Tooltip("이동 중에 변경될 레이어 이름 (Project Settings에서 설정한 이름과 같아야 함)")]
    public string movingLayerName = "AllyMoving";

    [Tooltip("도착 후 원래 충돌 상태로 복구되기까지의 지연 시간")]
    public float restoreDelay = 0.5f;

    // 내부 변수
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private int defaultLayer; // 원래 레이어 (Ally)
    private int movingLayer;  // 이동 중 레이어 (AllyMoving)
    private float restoreTime; // 충돌 복원 타이머

    // 경로 관련
    private Vector2? finalTarget;
    private Vector2[] path = new Vector2[2];
    private int pathCount = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        // 레이어 인덱스 캐싱 (문자열 비교 제거)
        defaultLayer = gameObject.layer;
        movingLayer = LayerMask.NameToLayer(movingLayerName);

        if (movingLayer == -1)
        {
            Debug.LogError($"[UnitMover2D] '{movingLayerName}' 레이어가 없습니다! Unity Tags & Layers에서 추가해주세요.");
            movingLayer = defaultLayer; // 오류 방지용
        }
    }

    void OnDisable()
    {
        ClearCommand();
    }

    void FixedUpdate()
    {
        // 1. 이동 명령이 없을 때 (정지 상태)
        if (pathCount == 0)
        {
            // 아직 복원되지 않았고, 복원 시간이 지났다면 -> 원래 레이어로 복구
            if (gameObject.layer == movingLayer && Time.time >= restoreTime)
            {
                gameObject.layer = defaultLayer;
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        // 2. 이동 로직
        Vector2 pos = rb.position;
        Vector2 tgt = path[0];
        Vector2 to = tgt - pos;
        float sqrDist = to.sqrMagnitude; // sqrt 연산 제거 최적화

        // 도착 판정 (StopDistance의 제곱과 비교)
        if (sqrDist <= stopDistance * stopDistance)
        {
            rb.linearVelocity = Vector2.zero;

            if (pathCount == 1) // 최종 목적지 도착
            {
                finalTarget = null;
                pathCount = 0;

                // 도착했으니 잠시 후 충돌 복원 (restoreDelay 후)
                restoreTime = Time.time + restoreDelay;
                return;
            }
            else // 경유지 도착
            {
                path[0] = path[1];
                pathCount = 1;
                return;
            }
        }

        // 이동 실행
        Vector2 dir = to.normalized;
        float finalSpeed = moveSpeed * speedMultiplier;
        rb.MovePosition(pos + dir * finalSpeed * Time.fixedDeltaTime);
    }

    // --- 외부 명령 메서드 ---

    public void SetMoveTarget(Vector2 worldPos)
    {
        finalTarget = worldPos;

        // ★ [핵심 최적화] 이동 시작 시 "통과 가능한 레이어"로 변경
        // Physics.IgnoreCollision 루프 대신 단 한 줄로 처리됨 (CPU 부하 0)
        if (movingLayer != -1) gameObject.layer = movingLayer;

        BuildPredictedPath(worldPos);
    }

    public void ClearCommand()
    {
        finalTarget = null;
        pathCount = 0;
        rb.linearVelocity = Vector2.zero;

        // 명령 취소 시 즉시 원래 레이어로 복구 (즉시 충돌)
        gameObject.layer = defaultLayer;
    }

    public bool HasCommand() => pathCount > 0;

    public void ApplySpeedMultiplier(float multiplier) => speedMultiplier = multiplier;
    public void ResetSpeedMultiplier() => speedMultiplier = 1.0f;

    // --- 경로 예측 (기존 로직 유지) ---
    void BuildPredictedPath(Vector2 finalPos)
    {
        pathCount = 0;
        Vector2 origin = rb.position;
        Vector2 toTarget = finalPos - origin;
        float dist = toTarget.magnitude;

        if (dist < 0.01f) return;

        Vector2 dir = toTarget / dist;
        RaycastHit2D hit = Physics2D.CircleCast(origin, colliderRadius, dir, dist, towerObstacleMask);

        if (!hit)
        {
            path[0] = finalPos;
            pathCount = 1;
            return;
        }

        // 타워 회피 로직
        Collider2D col = hit.collider;
        Vector2 center = col.bounds.center;
        float towerRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) + colliderRadius + avoidMargin;

        Vector2 tangent = Vector2.Perpendicular(dir);
        Vector2 cand1 = center + tangent * towerRadius;
        Vector2 cand2 = center - tangent * towerRadius;

        if ((cand1 - finalPos).sqrMagnitude < (cand2 - finalPos).sqrMagnitude)
            path[0] = cand1;
        else
            path[0] = cand2;

        path[1] = finalPos;
        pathCount = 2;
    }

    void LateUpdate()
    {
        if (!faceMoveDirection || sr == null || finalTarget == null) return;
        sr.flipX = finalTarget.Value.x > rb.position.x;
    }

    void OnDrawGizmosSelected()
    {
        if (finalTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(finalTarget.Value, 0.1f);
        }
    }
}