using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMover2D : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float stopDistance = 0.15f;
    public bool faceMoveDirection = true;

    [Header("Agent Shape")]
    public float agentRadius = 0.3f;     // 유닛 반경(콜라이더 대략치)
    public float clearance = 0.3f;     // 장애물과의 여유 거리

    [Header("Avoidance & Replan")]
    public float ignoreRadius = 1.0f;    // 같은 진영 '동적 유닛' 충돌무시 탐색 반경
    public float refreshInterval = 0.15f;// 무시 갱신 주기
    public float stuckTime = 0.6f;       // 이 시간 동안 거의 못 움직이면 재계획
    public float minProgress = 0.05f;    // '거의 못 움직임' 기준 거리

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector2? mainTarget;         // 최종 목적지
    private readonly Queue<Vector2> waypoints = new(); // 우회 경유지들

    // 내 모든 콜라이더(관통 토글용)
    private readonly List<Collider2D> myCols = new();
    // 같은 진영 '동적' 루트Rigid → 그 루트의 모든 콜라이더
    private readonly Dictionary<Rigidbody2D, List<Collider2D>> ignoredByRoot = new();

    // 진행도 추적
    private Vector2 lastPos;
    private float stuckTimer;
    private float nextRefresh;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            if (c) myCols.Add(c);
        lastPos = rb.position;
    }

    void OnDisable()
    {
        RestoreAllIgnores();
        mainTarget = null;
        waypoints.Clear();
        rb.velocity = Vector2.zero;
        stuckTimer = 0f;
    }

    void FixedUpdate()
    {
        if (mainTarget == null)
        {
            rb.velocity = Vector2.zero;
            RestoreAllIgnores();
            return;
        }

        // 현재 목표(있으면 경유지, 없으면 최종 목적지)
        Vector2 currentTarget = waypoints.Count > 0 ? waypoints.Peek() : mainTarget.Value;
        Vector2 pos = rb.position;

        // 목적지에 도착?
        float dist = Vector2.Distance(pos, currentTarget);
        if (dist <= stopDistance)
        {
            if (waypoints.Count > 0) waypoints.Dequeue();   // 다음 경유지로
            else ClearCommand();        // 최종 도착
            return;
        }

        // --- 동적 팀원 관통(명령 중에만) 갱신 ---
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + refreshInterval;
            RefreshIgnoreSameTeamDynamics();
        }

        // --- 장애물(아군 성/타워) 예측 충돌 → 경유지 생성 ---
        Vector2 desiredDir = (currentTarget - pos).normalized;
        if (PredictFriendlyTowerHit(pos, currentTarget, out RaycastHit2D hit))
        {
            Vector2 detour = ComputeDetourPoint(hit, currentTarget);
            // 무한 루프 방지: 경유지가 너무 가깝거나 이미 같은 점이면 건너뛴다
            if ((detour - pos).sqrMagnitude > 0.01f)
                waypoints.Enqueue(detour);
            // 한 번에 너무 많이 안 넣도록 안전장치
            if (waypoints.Count > 4) waypoints.Clear();
            return; // 다음 Fixed에서 새 경유지로 진행
        }

        // --- 이동 ---
        Vector2 step = desiredDir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(pos + step);
        if (faceMoveDirection && sr) sr.flipX = desiredDir.x < 0f;

        // --- 진행도 체크(막힘 시 재계획: 접선 경유지 추가) ---
        float moved = Vector2.Distance(rb.position, lastPos);
        lastPos = rb.position;
        if (moved < minProgress * Time.fixedDeltaTime)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckTime)
            {
                stuckTimer = 0f;
                // 현재 목표를 향한 Ray로 재계획(충돌 있으면 접선 경유지)
                if (PredictFriendlyTowerHit(rb.position, currentTarget, out RaycastHit2D hit2))
                {
                    Vector2 detour = ComputeDetourPoint(hit2, currentTarget);
                    if ((detour - rb.position).sqrMagnitude > 0.01f)
                        waypoints.Enqueue(detour);
                }
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    public void SetMoveTarget(Vector2 worldPos)
    {
        mainTarget = worldPos;
        waypoints.Clear();
        RefreshIgnoreSameTeamDynamics(); // 명령 시작 즉시 적용
        lastPos = rb.position;
        stuckTimer = 0f;
    }

    public bool HasCommand() => mainTarget != null;
    public void ClearCommand()
    {
        mainTarget = null;
        waypoints.Clear();
        RestoreAllIgnores();
        rb.velocity = Vector2.zero;
    }

    // ──────────────────────────────────────────────────────────────
    // 같은 진영 '동적 유닛' 관통 (정적 성/타워는 관통하지 X)
    // ──────────────────────────────────────────────────────────────
    void RefreshIgnoreSameTeamDynamics()
    {
        if (myCols.Count == 0 || mainTarget == null) return;

        // 반경 내 모든 콜라이더
        var hits = Physics2D.OverlapCircleAll(transform.position, ignoreRadius);

        int myLayer = gameObject.layer;
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (!col) continue;

            var otherRB = col.attachedRigidbody;
            if (!otherRB) continue;            // 정적(성/지형)은 여기서 다루지 않음
            if (otherRB == rb) continue;
            if (otherRB.gameObject.layer != myLayer) continue;

            // 성/타워는 정적이거나(대개), SpawnPoint/AllySpawner 보유 → 동적이어도 관통 제외
            if (IsFriendlyTower(otherRB.transform)) continue;

            if (ignoredByRoot.ContainsKey(otherRB)) continue;

            var others = new List<Collider2D>();
            otherRB.GetComponentsInChildren(true, others);

            foreach (var mine in myCols)
            {
                if (!mine) continue;
                foreach (var oc in others)
                {
                    if (!oc) continue;
                    Physics2D.IgnoreCollision(mine, oc, true);
                }
            }
            ignoredByRoot.Add(otherRB, others);
        }

        // 멀어진 동적 팀원 복원
        var toRestore = new List<Rigidbody2D>();
        foreach (var kv in ignoredByRoot)
        {
            var root = kv.Key;
            if (!root) { toRestore.Add(root); continue; }

            float sqr = (root.position - rb.position).sqrMagnitude;
            if (sqr > ignoreRadius * ignoreRadius * 4f)
                toRestore.Add(root);
        }
        foreach (var r in toRestore) RestoreByRoot(r);
    }

    void RestoreByRoot(Rigidbody2D root)
    {
        if (!root) return;
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

    // ──────────────────────────────────────────────────────────────
    // 아군 성/타워 예측 충돌: 원형 캐스트로 경유지 생성 트리거
    // ──────────────────────────────────────────────────────────────
    bool PredictFriendlyTowerHit(Vector2 from, Vector2 to, out RaycastHit2D hit)
    {
        Vector2 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 0.001f) { hit = new RaycastHit2D(); return false; }
        dir /= dist;

        // 모든 2D 콜라이더 대상, 레이어 필터는 코드에서 수동으로
        var res = Physics2D.CircleCast(from, agentRadius, dir, dist);
        if (res.collider == null) { hit = new RaycastHit2D(); return false; }

        // 같은 진영 + 성/타워?
        if (res.collider.gameObject.layer != gameObject.layer)
        {
            hit = new RaycastHit2D();
            return false;
        }
        // 자식 콜라이더 대응: 부모 쪽에서 판별
        if (!IsFriendlyTower(res.collider.transform))
        {
            hit = new RaycastHit2D();
            return false;
        }

        hit = res;
        return true;
    }

    Vector2 ComputeDetourPoint(RaycastHit2D hit, Vector2 finalTarget)
    {
        // 법선: 장애물 바깥(우리쪽)
        Vector2 n = hit.normal;
        Vector2 tangentCW = new Vector2(-n.y, n.x);
        Vector2 tangentCCW = new Vector2(n.y, -n.x);

        // 목적지 방향과 잘 맞는 접선 선택
        Vector2 toGoal = (finalTarget - hit.point).normalized;
        Vector2 chosen = (Vector2.Dot(tangentCW, toGoal) >= Vector2.Dot(tangentCCW, toGoal)) ? tangentCW : tangentCCW;

        // 접선 방향으로 살짝 돌아나갈 점 (반경+여유만큼)
        Vector2 detour = hit.point + chosen.normalized * Mathf.Max(agentRadius + clearance, 0.05f);
        return detour;
    }

    // ──────────────────────────────────────────────────────────────
    // ‘아군 성/타워’ 판별: 같은 레이어 + AllySpawner/SpawnPoint 존재
    // (정적 큰 오브젝트가 Targetable만 달렸어도 부모에 AllySpawner/SpawnPoint가 붙어있다면 잡힘)
    // ──────────────────────────────────────────────────────────────
    bool IsFriendlyTower(Transform tr)
    {
        if (tr == null) return false;
        int my = gameObject.layer;

        // 부모까지 검사(자식 콜라이더 포함)
        var go = tr.gameObject;
        if (go.layer != my) return false;

        return tr.GetComponentInParent<AllySpawner>() != null
            || tr.GetComponentInParent<SpawnPoint>() != null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.22f);
        Gizmos.DrawWireSphere(transform.position, ignoreRadius);
    }
}
