using UnityEngine;

/// <summary>
/// [지형] 늪 (Swamp)
/// 이 영역에 들어온 유닛의 이동 속도를 감소시킵니다.
/// </summary>
public class Swamp : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("속도 배율 (예: 0.6이면 속도가 60%로 감소)")]
    public float slowFactor = 0.6f;

    // 영역에 들어왔을 때
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 플레이어 감지 및 속도 감소
        Player player = collision.GetComponent<Player>();
        if (player != null) { player.speed *= slowFactor; return; }

        // 2. 적 감지 및 속도 감소
        Enemy enemy = collision.GetComponent<Enemy>();
        if (enemy != null) { enemy.speed *= slowFactor; return; }

        // 3. 아군 AI 감지 및 속도 감소
        AllyAI ally = collision.GetComponent<AllyAI>();
        if (ally != null) { ally.speed *= slowFactor; return; }

        // 4. RTS 이동 유닛 감지 및 속도 감소
        UnitMover2D mover = collision.GetComponent<UnitMover2D>();
        if (mover != null) { mover.moveSpeed *= slowFactor; return; }
    }

    // 영역에서 나갔을 때
    private void OnTriggerExit2D(Collider2D collision)
    {
        // 속도 복구 (감소시켰던 값을 다시 나누어 줌)

        Player player = collision.GetComponent<Player>();
        if (player != null) { player.speed /= slowFactor; return; }

        Enemy enemy = collision.GetComponent<Enemy>();
        if (enemy != null) { enemy.speed /= slowFactor; return; }

        AllyAI ally = collision.GetComponent<AllyAI>();
        if (ally != null) { ally.speed /= slowFactor; return; }

        UnitMover2D mover = collision.GetComponent<UnitMover2D>();
        if (mover != null) { mover.moveSpeed /= slowFactor; return; }
    }
}