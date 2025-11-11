using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class UnitMover2D : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float stopDistance = 0.15f;
    public bool faceMoveDirection = true;

    [Header("Same-Team Pass Through (명령 중만)")]
    public float ignoreRadius = 1.0f;     // 주변 팀원 탐색 반경
    public float refreshInterval = 0.15f; // 갱신 주기

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector2? target;

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
        target = null;
        rb.linearVelocity = Vector2.zero;
    }

    void FixedUpdate()
    {
        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            RestoreAllIgnores(); // 명령 없을 땐 항상 복원 상태
            return;
        }

        Vector2 pos = rb.position;
        Vector2 tgt = target.Value;
        Vector2 to = tgt - pos;
        float dist = to.magnitude;

        if (dist <= stopDistance)
        {
            rb.linearVelocity = Vector2.zero;
            target = null;
            RestoreAllIgnores();
            return;
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

    public void SetMoveTarget(Vector2 worldPos)
    {
        target = worldPos;
        RefreshIgnoreSameTeam(); // 명령 시작 즉시 한 번 적용
    }

    public bool HasCommand() => target != null;

    public void ClearCommand()
    {
        target = null;
        RestoreAllIgnores();
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
    }
}
