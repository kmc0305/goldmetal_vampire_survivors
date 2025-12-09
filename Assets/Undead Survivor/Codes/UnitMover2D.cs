using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMover2D : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float stopDistance = 0.15f;
    public bool faceMoveDirection = true;

    // 지형 효과 배율
    private float speedMultiplier = 1.0f;

    [Header("Tower Obstacle Avoidance")]
    public LayerMask towerObstacleMask;
    public float colliderRadius = 0.3f;
    public float avoidMargin = 0.2f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector2? finalTarget;

    // 경로 최적화 (Vector2 구조체는 가벼우므로 그대로 사용)
    private Vector2[] path = new Vector2[2];
    private int pathCount = 0;

    // [최적화] 거리 비교를 위해 제곱된 stopDistance 미리 계산
    private float sqrStopDistance;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        // [최적화] 매 프레임 제곱근 연산을 피하기 위해 제곱값 저장
        sqrStopDistance = stopDistance * stopDistance;
    }

    void OnDisable()
    {
        finalTarget = null;
        pathCount = 0;
        rb.linearVelocity = Vector2.zero;
        // speedMultiplier = 1.0f; // 필요하다면 초기화
    }

    void FixedUpdate()
    {
        if (pathCount == 0)
        {
            // 움직일 필요 없으면 속도 0 만들고 리턴 (Sleep 유도)
            if (rb.linearVelocity.sqrMagnitude > 0.001f)
                rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 pos = rb.position;
        Vector2 tgt = path[0];
        Vector2 to = tgt - pos;

        // [최적화] sqrMagnitude 사용 (sqrt 제거)
        float sqrDist = to.sqrMagnitude;

        if (sqrDist <= sqrStopDistance)
        {
            rb.linearVelocity = Vector2.zero;

            if (pathCount == 1)
            {
                finalTarget = null;
                pathCount = 0;
                return;
            }
            else
            {
                path[0] = path[1];
                pathCount = 1;
                return;
            }
        }

        // 방향 벡터 정규화 (이건 어쩔 수 없이 sqrt 필요하지만 1회)
        Vector2 dir = to.normalized;

        float finalSpeed = moveSpeed * speedMultiplier;
        rb.MovePosition(pos + dir * finalSpeed * Time.fixedDeltaTime);

        // 물리 충돌로 인한 밀림 방지용 zero 세팅은 필요할 때만
        // rb.linearVelocity = Vector2.zero; 
    }

    public void SetMoveTarget(Vector2 worldPos)
    {
        finalTarget = worldPos;
        BuildPredictedPath(worldPos);
    }

    public bool HasCommand() => pathCount > 0;

    public void ApplySpeedMultiplier(float multiplier) => speedMultiplier = multiplier;
    public void ResetSpeedMultiplier() => speedMultiplier = 1.0f;

    public void ClearCommand()
    {
        finalTarget = null;
        pathCount = 0;
        rb.linearVelocity = Vector2.zero;
    }

    void BuildPredictedPath(Vector2 finalPos)
    {
        pathCount = 0;
        Vector2 origin = rb.position;
        Vector2 toTarget = finalPos - origin;
        float dist = toTarget.magnitude; // Raycast 거리용이라 1회 계산 필요

        if (dist < 0.01f) return;

        Vector2 dir = toTarget / dist; // Normalized

        RaycastHit2D hit = Physics2D.CircleCast(origin, colliderRadius, dir, dist, towerObstacleMask);

        if (!hit)
        {
            path[0] = finalPos;
            pathCount = 1;
            return;
        }

        Collider2D col = hit.collider;
        Vector2 center = col.bounds.center;

        // Bounds extents는 가벼운 연산
        float towerRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) + colliderRadius + avoidMargin;

        Vector2 tangent = Vector2.Perpendicular(dir);
        Vector2 cand1 = center + tangent * towerRadius;
        Vector2 cand2 = center - tangent * towerRadius;

        // [최적화] sqrMagnitude로 거리 비교
        float sqrD1 = (cand1 - finalPos).sqrMagnitude;
        float sqrD2 = (cand2 - finalPos).sqrMagnitude;

        Vector2 corner = (sqrD1 < sqrD2) ? cand1 : cand2;

        path[0] = corner;
        path[1] = finalPos;
        pathCount = 2;
    }

    void LateUpdate()
    {
        if (!faceMoveDirection || sr == null || finalTarget == null) return;
        // 간단한 좌표 비교는 매우 빠름
        sr.flipX = finalTarget.Value.x > rb.position.x;
    }

    // OnDrawGizmosSelected 등 디버그 코드는 빌드 시 자동 제외되므로 성능 영향 적음
}