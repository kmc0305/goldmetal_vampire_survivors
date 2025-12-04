using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMover2D : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float stopDistance = 0.15f;
    public bool faceMoveDirection = true;

    // --- 지형 효과 관련 변수 (Terrain Effect Variables) ---
    // UnitMover2D가 기본 속도 외에 지형 효과로 인해 최종적으로 곱해지는 배율
    private float speedMultiplier = 1.0f;
    // ----------------------------------------------------

    [Header("Tower Obstacle Avoidance (예측 경로 회피)")]
    public LayerMask towerObstacleMask; // TowerObstacle 레이어만 체크
    public float colliderRadius = 0.3f; // 유닛 반지름(캡슐 크기에 맞게)
    public float avoidMargin = 0.2f;    // 타워에서 얼마나 더 떨어져서 도는지

    [Header("Same-Team Pass Through (명령 중만)")]
    public float ignoreRadius = 1.0f;     // 주변 팀원 탐색 반경
    public float refreshInterval = 0.15f; // 갱신 주기

    // [추가] 도착 후 충돌 복원 지연 설정
    [Header("도착 후 충돌 복원 지연")]
    public float restoreDelay = 0.5f; // 0.5초 동안 충돌 무시 유지
    private float restoreTime;         // 충돌 무시를 해제할 시간 (Time.time)

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    // 최종 목적지(디버그용/방향 기준)
    private Vector2? finalTarget;

    // 간단한 2포인트 경로: [0] = 현재 목표, [1] = 최종 목표(있을 때)
    private readonly Vector2[] path = new Vector2[2];
    private int pathCount = 0;

    // 내 모든 콜라이더
    private readonly List<Collider2D> myCols = new();

    // 무시 중인 "상대 루트 Rigidbody" -> 그 리지드의 모든 콜라이더
    private readonly Dictionary<Rigidbody2D, List<Collider2D>> ignoredByRoot = new();

    private float nextRefresh;

    // [최적화] Physics2D 버퍼 및 재사용 리스트 (GC 방지)
    private static readonly Collider2D[] overlapBuffer = new Collider2D[50];
    private readonly List<Rigidbody2D> toRestoreBuffer = new();

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
        // 비활성화 시 즉시 복원
        restoreTime = 0;
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

            // [수정] restoreDelay가 만료되었을 때만 충돌 복원
            if (Time.time >= restoreTime)
            {
                RestoreAllIgnores();
            }
            return;
        }

        Vector2 pos = rb.position;
        Vector2 tgt = path[0];           // 현재 따라가야 할 지점
        Vector2 to = tgt - pos;
        float dist = to.magnitude;

        // 현재 목표 지점에 거의 도착
        if (dist <= stopDistance)
        {
            rb.linearVelocity = Vector2.zero;

            if (pathCount == 1)
            {
                // 최종 목적지 도착
                finalTarget = null;
                pathCount = 0;

                // [수정] 즉시 복원 대신 타이머 설정
                restoreTime = Time.time + restoreDelay;
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

        // 지형 효과 반영한 속도
        float finalSpeed = moveSpeed * speedMultiplier;

        // Enemy 와 비슷하게: MovePosition 사용, velocity 는 0 처리
        rb.MovePosition(pos + dir * finalSpeed * Time.fixedDeltaTime);
        rb.linearVelocity = Vector2.zero;

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

    // --- 지형 효과 관련 메서드 (Terrain Effect Methods) ---

    // 속도 보정 배율을 적용하는 함수 (Swamp Area Logic에서 호출됨)
    public void ApplySpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    // 속도 보정 배율을 기본값으로 되돌리는 함수 (Swamp Area Logic에서 호출됨)
    public void ResetSpeedMultiplier()
    {
        speedMultiplier = 1.0f;
    }

    public void ClearCommand()
    {
        finalTarget = null;
        pathCount = 0;
        rb.linearVelocity = Vector2.zero;

        // [수정] 명령 취소 시 즉시 복원
        restoreTime = 0;
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
    //  방향 전환 (Enemy.LateUpdate 스타일)
    // ------------------------------------------------------------------
    void LateUpdate()
    {
        if (!faceMoveDirection) return;
        if (sr == null) return;
        if (finalTarget == null) return;   // 이동 중이 아니면 그대로

        // Ally(수동 이동): 목표 위치 기준으로 좌/우만 판단
        Vector2 pos = rb.position;
        sr.flipX = finalTarget.Value.x > pos.x;
    }

    // ------------------------------------------------------------------
    // 같은 진영(루트 Rigidbody 기준) 충돌 무시/복원
    // [최적화] 정적 버퍼 재사용으로 GC 방지
    // ------------------------------------------------------------------
    void RefreshIgnoreSameTeam()
    {
        if (myCols.Count == 0) return;

        // [최적화] 정적 버퍼 사용
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, ignoreRadius, overlapBuffer);
        int myLayer = gameObject.layer;

        // 새로 발견된 팀원에게 Ignore Collision 적용
        for (int i = 0; i < hitCount; i++)
        {
            var col = overlapBuffer[i];
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

        // [최적화] 재사용 버퍼로 GC 방지
        toRestoreBuffer.Clear();
        foreach (var kv in ignoredByRoot)
        {
            var root = kv.Key;
            // 유닛이 파괴되었거나, 무시 반경의 2배(이탈 감지)를 벗어났다면 복원
            if (!root) { toRestoreBuffer.Add(root); continue; }

            float sqr = (root.position - rb.position).sqrMagnitude;
            if (sqr > ignoreRadius * ignoreRadius * 4f)
                toRestoreBuffer.Add(root);
        }

        for (int i = 0; i < toRestoreBuffer.Count; i++)
            RestoreByRoot(toRestoreBuffer[i]);
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