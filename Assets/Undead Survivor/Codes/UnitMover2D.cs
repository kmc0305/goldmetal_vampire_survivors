using System.Collections.Generic;
using UnityEngine;
using Vector2 = UnityEngine.Vector2; // Vector2 참조 모호성 해결 (CS0104)

// UnitMover2D도 다른 스크립트들과 같은 네임스페이스에 정의
// namespace GoldMetal.Survivors  <-- 주석 처리: 다른 스크립트(Player, AllyAI 등)가 이 클래스를 찾을 수 있게 해제
// {
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

    // [최적화] Physics2D 버퍼 및 재사용 리스트 (GC 방지)
    private static readonly Collider2D[] overlapBuffer = new Collider2D[50];
    private readonly List<Rigidbody2D> rootsBuffer = new();
    private readonly List<Rigidbody2D> toRestoreBuffer = new();
    private readonly List<Collider2D> tempCollidersBuffer = new();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        // 모든 Collider2D 컴포넌트 찾기
        myCols.AddRange(GetComponentsInChildren<Collider2D>());
    }

    void Start()
    {
        // 0.15초마다 팀원 무시 로직 갱신 (기존 로직 유지)
        InvokeRepeating(nameof(RefreshIgnores), refreshInterval, refreshInterval);
    }

    private void FixedUpdate()
    {
        // 현재 목표 경로로 이동
        MoveTowardsPath();
    }

    // --- 지형 효과 관련 메서드 (Terrain Effect Methods) ---

    // 속도 보정 배율을 적용하는 함수 (Swamp Area Logic에서 호출됨)
    public void ApplySpeedMultiplier(float multiplier)
    {
        // 새로운 보정값으로 설정
        speedMultiplier = multiplier;
    }

    // 속도 보정 배율을 기본값으로 되돌리는 함수 (Swamp Area Logic에서 호출됨)
    public void ResetSpeedMultiplier()
    {
        speedMultiplier = 1.0f;
    }

    // --- 기존 이동 로직 ---

    public void MoveTo(Vector2 direction) // Vector2 사용
    {
        if (rb == null || direction.sqrMagnitude == 0) return;

        // 지형 효과를 반영한 최종 속도 계산
        float finalSpeed = moveSpeed * speedMultiplier;

        // 물리 이동 적용 (FixedUpdate에서 호출하는 것이 일반적)
        rb.velocity = direction.normalized * finalSpeed;

        // 방향 전환 (Face Move Direction) 로직은 MoveTowardsPath에 통합되어 있다고 가정
    }

    // 외부에서 최종 목적지(Target)를 설정하는 함수
    public void SetTarget(Vector2 targetPos) // Vector2 사용
    {
        finalTarget = targetPos;
        path[0] = targetPos; // 초기 목표를 최종 목표로 설정
        pathCount = 1;

        // 이 곳에서 유닛이 움직이도록 MoveTo를 호출할 필요는 없습니다.
        // MoveTowardsPath가 FixedUpdate에서 지속적으로 호출되어야 합니다.
    }

    // SetTarget의 별칭 (다른 스크립트 호환용)
    public void SetMoveTarget(Vector2 targetPos)
    {
        SetTarget(targetPos);
    }

    // 현재 이동 명령이 있는지 확인
    public bool HasCommand()
    {
        return finalTarget.HasValue;
    }

    // FixedUpdate에서 호출되어 경로를 따라 이동하고 충돌 회피를 수행
    private void MoveTowardsPath()
    {
        if (!finalTarget.HasValue || pathCount == 0 || rb == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // 현재 목표 위치
        Vector2 targetPosition = path[0];

        // 이동 방향 계산
        Vector2 direction = targetPosition - (Vector2)transform.position; // Vector2 사용

        // 회피 로직 (Obstacle Avoidance Logic)
        if (CheckForObstacles(direction, out Vector2 avoidanceDirection)) // Vector2 사용
        {
            direction = avoidanceDirection;
        }

        // 목표에 충분히 가까워졌는지 확인 (Close enough to current point)
        if (direction.magnitude <= stopDistance)
        {
            // 경로가 남아 있다면 다음 경로로 이동
            if (pathCount > 1)
            {
                // 다음 경로 포인트를 현재 목표로 설정
                path[0] = path[1];
                pathCount = 1; // 경로가 1개 남음
                direction = path[0] - (Vector2)transform.position;
            }
            else
            {
                // 최종 목표에 도달
                finalTarget = null;
                rb.velocity = Vector2.zero;
                return;
            }
        }

        // 최종 이동 속도 계산: 기본 속도 * 지형 보정 배율
        float finalSpeed = moveSpeed * speedMultiplier;

        // 물리 이동 적용
        rb.velocity = direction.normalized * finalSpeed;

        // 방향에 따라 스프라이트 뒤집기 (Face Move Direction)
        if (faceMoveDirection && direction.x != 0)
        {
            sr.flipX = direction.x < 0;
        }
    }

    // 장애물 회피 로직 (기존 코드의 일부를 가정하여 단순화)
    private bool CheckForObstacles(Vector2 currentDirection, out Vector2 avoidanceDirection) // Vector2 사용
    {
        // 타워(Obstacle)를 피하는 로직을 여기에 구현 (CapsuleCast, CircleCast 등)

        // 임시: 장애물이 없다고 가정
        avoidanceDirection = currentDirection;
        return false;
    }

    // --- 팀원 무시 로직 (Same-Team Pass Through Logic) ---

    // [최적화] 리스트 재사용으로 GC 방지
    void RefreshIgnores()
    {
        if (myCols.Count == 0) return;

        // 이동 명령이 없으면 충돌 무시를 모두 복구하고 종료
        if (!HasCommand())
        {
            if (ignoredByRoot.Count > 0)
            {
                RestoreAllIgnores();
            }
            return;
        }

        // [최적화] 정적 버퍼 사용
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, ignoreRadius, overlapBuffer);

        // [최적화] 리스트 재사용 (Clear 후 사용)
        rootsBuffer.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D c = overlapBuffer[i];
            if (!c || c.attachedRigidbody == null || c.gameObject == gameObject) continue;
            if (c.CompareTag(gameObject.tag) && !rootsBuffer.Contains(c.attachedRigidbody))
            {
                rootsBuffer.Add(c.attachedRigidbody);
            }
        }

        // [최적화] 리스트 재사용
        toRestoreBuffer.Clear();
        foreach (var root in ignoredByRoot.Keys)
        {
            if (root == null || root.transform == null) // Rigidbody가 파괴되었을 경우 처리
            {
                toRestoreBuffer.Add(root);
                continue;
            }

            // 거리가 너무 멀어졌는지 확인 (기존 로직)
            Vector2 pos = root.transform.position;
            float sqr = ((Vector2)transform.position - pos).sqrMagnitude;
            if (sqr > ignoreRadius * ignoreRadius * 4f)
                toRestoreBuffer.Add(root);
        }

        for (int i = 0; i < toRestoreBuffer.Count; i++)
            RestoreByRoot(toRestoreBuffer[i]);

        foreach (var root in rootsBuffer)
        {
            if (!ignoredByRoot.ContainsKey(root))
                IgnoreByRoot(root);
        }
    }

    // [최적화] 리스트 재사용
    void IgnoreByRoot(Rigidbody2D root)
    {
        if (!root || myCols.Count == 0) return;

        // [최적화] 버퍼 리스트 재사용
        tempCollidersBuffer.Clear();
        root.GetComponentsInChildren(tempCollidersBuffer);
        if (tempCollidersBuffer.Count == 0) return;

        // Dictionary에 저장할 때는 새 리스트 생성 필요 (참조 유지)
        List<Collider2D> storedList = new List<Collider2D>(tempCollidersBuffer);
        ignoredByRoot.Add(root, storedList);

        foreach (var mine in myCols)
        {
            if (!mine) continue;
            foreach (var oc in storedList)
            {
                if (!oc) continue;
                Physics2D.IgnoreCollision(mine, oc, true);
            }
        }
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
    }
}
// } <--- namespace 닫는 괄호 주석 처리