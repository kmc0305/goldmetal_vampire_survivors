using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMover2D : MonoBehaviour
{
    [Header("Physics")]
    public bool forceZeroFriction = true;
    [Header("Move")]
    public float moveSpeed = 3.6f;
    public float stopDistance = 0.15f;
    public bool faceMoveDirection = true;

    [Header("Agent Shape")]
    public float agentRadius = 0.4f;   // 유닛 콜라이더 대략 반경
    public float clearance = 0.7f;   // 성 표면과의 여유 거리

    [Header("Same-team pass-through")]
    public float ignoreRadius = 1.0f;   // 같은 진영 '동적' 유닛 관통 반경
    public float refreshInterval = 0.15f;

    [Header("Wall-follow (Friendly Tower)")]
    public LayerMask towerMask;                 // 비워두면 런타임에 같은 레이어만
    public bool includeStaticTargetableAsTower = true; // Rigidbody 없는 Targetable을 성으로 간주
    public float wallBias = 0.03f;             // 표면 반발(겹침 방지 바이어스)
    public float exitCheckInterval = 0.1f;      // 시야 복귀 체크 주기
    public float maxWallFollowTime = 3.0f;      // 무한 루프 방지

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    // 명령
    private Vector2? mainTarget;
    private readonly Queue<Vector2> waypoints = new();

    // 같은 팀 동적 유닛 관통
    private readonly List<Collider2D> myCols = new();
    private readonly Dictionary<Rigidbody2D, List<Collider2D>> ignoredByRoot = new();
    private float nextRefresh;

    // 벽따라가기 상태
    enum MoveState { Direct, WallFollow }
    private MoveState state = MoveState.Direct;
    private Collider2D wallCol;        // 현재 따라가는 성(콜라이더)
    private int wallSide = 1;          // +1 시계 / -1 반시계 (선택된 접선 방향)
    private float wallFollowTimer = 0f;
    private float nextExitCheck = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
        if (c) myCols.Add(c);

    // ★ 성이 있는 레이어를 포함하도록 직접 지정 (예: Ally)
    // 인스펙터에서 설정할 수 있다면 이 부분은 생략해도 됩니다.
        if (towerMask.value == 0)
           towerMask = LayerMask.GetMask("Ally"); // 성 레이어명을 정확히 넣으세요
        Physics2D.queriesHitTriggers = true;
        // Awake() 끝에 추가
        if (forceZeroFriction)
        {
            var mat = new PhysicsMaterial2D("NoFriction2D") { friction = 0f, bounciness = 0f };
            foreach (var c in myCols) if (c) c.sharedMaterial = mat;
        }
    }


    void OnDisable()
    {
        RestoreAllIgnores();
        mainTarget = null;
        waypoints.Clear();
        wallCol = null;
        state = MoveState.Direct;
        rb.linearVelocity = Vector2.zero;
    }

    public void SetMoveTarget(Vector2 worldPos)
    {
        mainTarget = worldPos;
        waypoints.Clear();
        state = MoveState.Direct;
        wallCol = null;
        wallFollowTimer = 0f;
        RefreshIgnoreSameTeamDynamics();
    }

    public void ClearCommand()
    {
        mainTarget = null;
        waypoints.Clear();
        state = MoveState.Direct;
        wallCol = null;
        RestoreAllIgnores();
        rb.linearVelocity = Vector2.zero;
    }

    public bool HasCommand() => mainTarget != null;

    void FixedUpdate()
    {
        if (mainTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            RestoreAllIgnores();
            return;
        }

        // 동적 같은팀 관통 토글
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + refreshInterval;
            RefreshIgnoreSameTeamDynamics();
        }

        Vector2 pos = rb.position;
        Vector2 target = waypoints.Count > 0 ? waypoints.Peek() : mainTarget.Value;

        // 도착
        if (Vector2.Distance(pos, target) <= stopDistance)
        {
            if (waypoints.Count > 0) waypoints.Dequeue();
            else { ClearCommand(); return; }
        }

        // 상태별 이동
        switch (state)
        {
            case MoveState.Direct:
                DirectMove(pos, target);
                break;
            case MoveState.WallFollow:
                WallFollowMove(pos, target);
                break;
        }
    }

    // ───────────────────────────── Direct 모드: 직진, 충돌 시 벽따라기로 진입
    void DirectMove(Vector2 pos, Vector2 target)
    {
        Vector2 dir = (target - pos).normalized;
        Vector2 step = dir * moveSpeed * Time.fixedDeltaTime;

        // ★ 캐스트 거리 보강: 최소 이동거리 = step 또는 (agentRadius+clearance)
        float castDist = Mathf.Max(step.magnitude, agentRadius + clearance * 0.5f);

        var hit = Physics2D.CircleCast(pos, agentRadius, dir, castDist, towerMask);
        if (hit.collider && IsFriendlyTower(hit.collider.transform))
        {
            EnterWallFollow(hit.collider, hit.point, hit.normal);
            // 첫 프레임 접선 슬라이드로 시작
            Vector2 n = hit.normal.sqrMagnitude > 1e-6f ? hit.normal : Vector2.up;
            Vector2 tCW = new Vector2(-n.y, n.x);
            Vector2 tCCW = new Vector2(n.y, -n.x);
            Vector2 toGoal = (mainTarget.Value - hit.point).normalized;
            int side = (Vector2.Dot(tCW, toGoal) >= Vector2.Dot(tCCW, toGoal)) ? +1 : -1;
            Vector2 slide = (side == 1 ? tCW : tCCW) * step.magnitude + n * wallBias;
            rb.MovePosition(pos + slide);
            if (faceMoveDirection && sr) sr.flipX = slide.x < 0f;
            return;
        }

        // ★ 캐스트가 실패해도 '시야'가 막히면 근처 성을 잡아 벽따라기로 진입
        if (!HasLineOfSightToTarget(pos, mainTarget.Value))
        {
            if (AcquireWallFromLOSBlock(pos, dir, castDist, out var col, out var nrm, out var p))
            {
                EnterWallFollow(col, p, nrm);
                return;
            }
        }

        // 정상 직진
        rb.MovePosition(pos + step);
        if (faceMoveDirection && sr) sr.flipX = dir.x < 0f;
    }

    void EnterWallFollow(Collider2D col, Vector2 hitPoint, Vector2 hitNormal)
    {
        Debug.Log($"Enter WallFollow: {wallCol?.name}", this);

        wallCol = col;
        // 접선 방향 선택(목표에 유리한 쪽)
        Vector2 n = hitNormal.sqrMagnitude > 1e-6f ? hitNormal : Vector2.up;
        Vector2 tCW = new Vector2(-n.y, n.x);
        Vector2 tCCW = new Vector2(n.y, -n.x);
        Vector2 toGoal = (mainTarget.Value - hitPoint).normalized;
        wallSide = (Vector2.Dot(tCW, toGoal) >= Vector2.Dot(tCCW, toGoal)) ? +1 : -1;

        state = MoveState.WallFollow;
        wallFollowTimer = 0f;
        nextExitCheck = 0f;
    }

    // 시야가 막혔을 때 주변 성을 찾아 '벽따라기' 시작점 잡기
    bool AcquireWallFromLOSBlock(Vector2 pos, Vector2 dir, float radius,
        out Collider2D col, out Vector2 nrm, out Vector2 p)
    {
        col = null; nrm = Vector2.up; p = pos;

        // 내 주변에서 가장 가까운 '성'을 찾는다
        var overlaps = Physics2D.OverlapCircleAll(pos, Mathf.Max(radius * 2f, agentRadius + clearance), towerMask);
        float best = float.MaxValue;
        foreach (var c in overlaps)
        {
            if (!c) continue;
            if (!IsFriendlyTower(c.transform)) continue;
            float d = Vector2.SqrMagnitude((Vector2)c.bounds.ClosestPoint(pos) - pos);
            if (d < best) { best = d; col = c; }
        }
        if (!col) return false;

        Vector2 cp = col.ClosestPoint(pos);
        nrm = (pos - cp).sqrMagnitude > 1e-6f ? (pos - cp).normalized
                                              : (pos - (Vector2)col.bounds.center).normalized;
        p = cp;
        return true;
    }
    void OnCollisionEnter2D(Collision2D c)
    {
        if (mainTarget == null) return;
        if (c.collider && ((towerMask.value & (1 << c.gameObject.layer)) != 0) && IsFriendlyTower(c.collider.transform))
        {
            var contact = c.GetContact(0);
            EnterWallFollow(c.collider, contact.point,
                contact.normal.sqrMagnitude > 1e-6f ? contact.normal : (rb.position - contact.point).normalized);
        }
    }

    // ───────────────────────────── WallFollow 모드: 경계를 따라 이동, 시야가 열리면 Direct 복귀
    void WallFollowMove(Vector2 pos, Vector2 target)
    {
        if (!wallCol) { state = MoveState.Direct; return; }

        rb.linearVelocity = Vector2.zero; // 마찰/속도 누적 방지

        wallFollowTimer += Time.fixedDeltaTime;

        // 현재 표면 법선 (ClosestPoint 기반)
        Vector2 cp = wallCol.ClosestPoint(pos);
        Vector2 n = (pos - cp).sqrMagnitude > 1e-6f ? (pos - cp).normalized
                                                      : (pos - (Vector2)wallCol.bounds.center).normalized;
        Vector2 tangent = (wallSide == 1) ? new Vector2(-n.y, n.x) : new Vector2(n.y, -n.x);

        // 원하는 접선 스텝(기본)
        Vector2 step = tangent.normalized * moveSpeed * Time.fixedDeltaTime + n * wallBias;

        // ▶ 견고한 슬라이드: 막히면 법선 성분 제거 후 재시도 (최대 3회)
        Vector2 attempt = step;
        for (int i = 0; i < 3; i++)
        {
            var hit = Physics2D.CircleCast(pos, agentRadius, attempt.normalized, attempt.magnitude, towerMask);
            if (!hit.collider || !IsFriendlyTower(hit.collider.transform))
                break; // 더 이상 안 막힘 → 이 스텝으로 이동

            // 새로운 접선/법선으로 재투영
            Vector2 hn = hit.normal.sqrMagnitude > 1e-6f ? hit.normal : n;
            Vector2 slide = attempt - Vector2.Dot(attempt, hn) * hn + hn * wallBias;
            attempt = slide;

            // 코너 넘어가면 현재 타는 벽 갱신
            if (hit.collider != wallCol) wallCol = hit.collider;
        }

        rb.MovePosition(pos + attempt);
        if (faceMoveDirection && sr) sr.flipX = attempt.x < 0f;

        // 일정 주기로 '직선 시야' 점검 → 열리면 Direct 복귀
        if (Time.time >= nextExitCheck)
        {
            nextExitCheck = Time.time + exitCheckInterval;
            if (HasLineOfSightToTarget(rb.position, mainTarget.Value) || wallFollowTimer >= maxWallFollowTime)
            {
                state = MoveState.Direct;
                wallCol = null;
            }
        }
    }


    // 목표까지 직선 경로에 성이 없는가?
    bool HasLineOfSightToTarget(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 1e-4f) return true;
        var hit = Physics2D.CircleCast(from, agentRadius, dir / dist, dist, towerMask);
        return !(hit.collider && IsFriendlyTower(hit.collider.transform));
    }

    // ───────────────────────────── 같은 진영 ‘동적 유닛’ 관통 (성/타워는 제외)
    void RefreshIgnoreSameTeamDynamics()
    {
        if (myCols.Count == 0 || mainTarget == null) return;

        var hits = Physics2D.OverlapCircleAll(transform.position, ignoreRadius);
        int myLayer = gameObject.layer;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (!col) continue;

            var otherRB = col.attachedRigidbody;
            if (!otherRB) continue;                       // 정적(성)은 제외
            if (otherRB == rb) continue;
            if (otherRB.gameObject.layer != myLayer) continue;
            if (IsFriendlyTower(otherRB.transform)) continue; // 성/타워는 제외

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

        // 멀어진 대상 복원
        var toRestore = new List<Rigidbody2D>();
        foreach (var kv in ignoredByRoot)
        {
            var root = kv.Key;
            if (!root) { toRestore.Add(root); continue; }
            if ((root.position - rb.position).sqrMagnitude > ignoreRadius * ignoreRadius * 4f)
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

    // ───────────────────────────── ‘아군 성/타워’ 판별
    // ‘아군 성/타워’ 판별: 같은 레이어 + FriendlyTower 마커
    bool IsFriendlyTower(Transform tr)
    {
        if (!tr) return false;
        // 레이어 필터: 같은 진영 레이어만 보려면 유지, 다른 레이어도 성으로 보려면 이 줄 삭제
        if (tr.gameObject.layer != gameObject.layer) return false;

        // 마커가 부모 어디에 있어도 OK
        return tr.GetComponentInParent<FriendlyTower>() != null;
    }

}
