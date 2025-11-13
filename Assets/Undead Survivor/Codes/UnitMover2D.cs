using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMover2D : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float stopDistance = 0.15f;
    public bool faceMoveDirection = true;

    [Header("Tower Obstacle Avoidance (예측 경로 회피)")]
    public LayerMask towerObstacleMask; // TowerObstacle 레이어만 체크
    public float colliderRadius = 0.3f; // 유닛 반지름(캡슐 크기에 맞게)
    public float avoidMargin = 0.2f;    // 타워에서 얼마나 더 떨어져서 도는지

    [Header("Same-Team Pass Through (명령 중만)")]
    public float ignoreRadius = 1.0f;     // 주변 팀원 탐색 반경
    public float refreshInterval = 0.15f; // 갱신 주기

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    // 최종 목적지(디버그용/상태 확인용)
    private Vector2? finalTarget;

    // 간단한 2포인트 경로: [0] = 현재 목표, [1] = 최종 목표(있을 때)
    private readonly Vector2[] path = new Vector2[2];
    private int pathCount = 0;

    // 내 모든 콜라이더
    private readonly List<Collider2D> myCols = new();

    // 무시 중인 "상대 루트 Rigidbody" -> 그 리지드의 모든 콜라이더
    private readonly Dictionary<Rigidbody2D, List<Collider2D>> ignoredByRoot = new();

    private float nextRefresh;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        // 내 자식 포함 모든 Collider2D 수집
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            if (c) myCols.Add(c);
    }

    void OnDisable()
    {
        RestoreAllIgnores();
        finalTarget = null;
        pathCount = 0;
        rb.linearVelocity = Vector2.zero;
    }

    void FixedUpdate()
    {
        // 이동 명령 없으면 정지 + 아군 충돌 복원
        if (pathCount == 0)
        {
            rb.linearVelocity = Vector2.zero;
            RestoreAllIgnores(); // 명령 없을 땐 항상 복원 상태
            return;
        }

        Vector2 pos = rb.position;
        Vector2 tgt = path[0];           // 현재 따라가야 할 지점
        Vector2 to = tgt - pos;
        float dist = to.magnitude;

        // 현재 목표 지점에 거의 도착
        if (dist <= stopDistance)
        {
            if (pathCount == 1)
            {
                // 최종 목적지 도착
                rb.linearVelocity = Vector2.zero;
                finalTarget = null;
                pathCount = 0;
                RestoreAllIgnores();
                return;
            }
            else
            {
                // 코너를 지나쳤으니 다음(최종) 지점으로
                path[0] = path[1];
                pathCount = 1;
                return;
            }
        }

        Vector2 dir = to / Mathf.Max(dist, 0.0001f);
        rb.MovePosition(pos + dir * moveSpeed * Time.fixedDeltaTime);

        if (faceMoveDirection && sr) sr.flipX = dir.x < 0f;

        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + refreshInterval;
            RefreshIgnoreSameTeam();
        }
    }

    /// <summary>
    /// 외부(AllyAI / RTS Selection)에서 호출하는 이동 명령
    /// </summary>
    public void SetMoveTarget(Vector2 worldPos)
    {
        finalTarget = worldPos;
        BuildPredictedPath(worldPos); // 타워 예측해서 경로 작성
        RefreshIgnoreSameTeam();      // 명령 시작 즉시 한 번 적용
    }

    public bool HasCommand() => pathCount > 0;

    public void ClearCommand()
    {
        finalTarget = null;
        pathCount = 0;
        RestoreAllIgnores();
    }

    // ------------------------------------------------------------------
    //  타워 예측 회피 경로 생성 (현재 위치 → finalTarget)
    // ------------------------------------------------------------------
    void BuildPredictedPath(Vector2 finalPos)
    {
        pathCount = 0;

        Vector2 origin = rb.position;
        Vector2 toTarget = finalPos - origin;
        float dist = toTarget.magnitude;
        if (dist < 0.01f) return;

        Vector2 dir = toTarget / dist;

        // 예측: 현재 위치에서 최종 목표까지 CircleCast
        // 중간에 TowerObstacle 에 부딪힐 예정인지 확인
        RaycastHit2D hit = Physics2D.CircleCast(
            origin,
            colliderRadius,
            dir,
            dist,
            towerObstacleMask
        );

        if (!hit)
        {
            // 막힌 것 없으면 직선 경로만 사용
            path[0] = finalPos;
            pathCount = 1;
            return;
        }

        // 여기부터는 "타워 콜라이더에 충돌할 예정"인 경우
        Collider2D col = hit.collider;
        Vector2 center = col.bounds.center;

        // 타워 반경(대략) + 내 반지름 + 여유 거리
        float towerRadius =
            Mathf.Max(col.bounds.extents.x, col.bounds.extents.y)
            + colliderRadius + avoidMargin;

        // 목적지 방향과 수직인 두 방향(왼/오른쪽) 중 더 짧은 쪽 선택
        Vector2 tangent = Vector2.Perpendicular(dir); // 왼쪽
        Vector2 cand1 = center + tangent * towerRadius;
        Vector2 cand2 = center - tangent * towerRadius;

        float d1 = Vector2.Distance(cand1, finalPos);
        float d2 = Vector2.Distance(cand2, finalPos);

        Vector2 corner = (d1 < d2) ? cand1 : cand2;

        // 경로: 현재 → corner → 최종 목적지
        path[0] = corner;
        path[1] = finalPos;
        pathCount = 2;
    }

    // ------------------------------------------------------------------
    // 같은 진영(루트 Rigidbody 기준) 충돌 무시/복원
    // ------------------------------------------------------------------
    void RefreshIgnoreSameTeam()
    {
        if (myCols.Count == 0) return;

        // 반경 내 모든 콜라이더(레이어 제한 없이) 조회
        var hits = Physics2D.OverlapCircleAll(transform.position, ignoreRadius);
        int myLayer = gameObject.layer;

        // 새로 발견된 팀원에게 Ignore Collision 적용
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (!col) continue;

            var otherRoot = col.attachedRigidbody;
            if (!otherRoot) continue;                   // 정적 콜라이더는 무시
            if (otherRoot == rb) continue;              // 자기 자신
            if (otherRoot.gameObject.layer != myLayer)  // 같은 진영(같은 레이어)만
                continue;

            if (ignoredByRoot.ContainsKey(otherRoot)) continue; // 이미 처리

            // 해당 루트의 모든 자식 콜라이더 수집
            var others = new List<Collider2D>();
            otherRoot.GetComponentsInChildren(true, others);

            // 내 모든 콜라이더 ↔ 상대 루트의 모든 콜라이더 쌍에 대해 IgnoreCollision
            foreach (var mine in myCols)
            {
                if (!mine) continue;
                foreach (var oc in others)
                {
                    if (!oc) continue;
                    Physics2D.IgnoreCollision(mine, oc, true);
                }
            }
            ignoredByRoot.Add(otherRoot, others);
        }

        // 너무 멀어진 팀원은 복원(성능 위해 조금 넓은 반경으로 해제)
        var toRestore = new List<Rigidbody2D>();
        foreach (var kv in ignoredByRoot)
        {
            var root = kv.Key;
            if (!root) { toRestore.Add(root); continue; }

            float sqr = (root.position - rb.position).sqrMagnitude;
            if (sqr > ignoreRadius * ignoreRadius * 4f)
                toRestore.Add(root);
        }

        for (int i = 0; i < toRestore.Count; i++)
            RestoreByRoot(toRestore[i]);
    }

    void RestoreByRoot(Rigidbody2D root)
    {
        if (!root) { return; }
        if (!ignoredByRoot.TryGetValue(root, out var others)) return;

        foreach (var mine in myCols)
        {
            if (!mine) continue;
            foreach (var oc in others)
            {
                if (!oc) continue;
                Physics2D.IgnoreCollision(mine, oc, false);
            }
        }
        ignoredByRoot.Remove(root);
    }

    void RestoreAllIgnores()
    {
        if (ignoredByRoot.Count == 0 || myCols.Count == 0) return;

        foreach (var kv in ignoredByRoot)
        {
            var others = kv.Value;
            foreach (var mine in myCols)
            {
                if (!mine) continue;
                foreach (var oc in others)
                {
                    if (!oc) continue;
                    Physics2D.IgnoreCollision(mine, oc, false);
                }
            }
        }
        ignoredByRoot.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, ignoreRadius);

        // 최종 목적지 디버그용
        if (finalTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(finalTarget.Value, 0.1f);
        }
    }
}
