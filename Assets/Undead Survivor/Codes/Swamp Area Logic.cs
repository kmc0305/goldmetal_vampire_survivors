using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// [늪지대/둔화 영역] 이 영역에 진입한 플레이어나 유닛의 이동 속도를 늦춥니다.
/// 이 스크립트는 isTrigger가 활성화된 Collider2D에 부착되어야 합니다.
/// </summary>
public class SwampArea : MonoBehaviour
{
    [Header("둔화 설정")]
    [Tooltip("원래 속도에 곱해지는 배율 (예: 0.5는 50% 속도)")]
    public float slowMultiplier = 0.5f; // 늪지대 속도 배율 (기본 50% 느리게)

    // 플레이어, 아군, 적 유닛의 이동 속도 배율을 설정하는 범용적인 함수 시그니처입니다.
    private const string SET_SPEED_MULTIPLIER_METHOD = "SetSpeedMultiplier";

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ApplySlow(collision, slowMultiplier);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // 영역을 벗어날 때는 속도 배율을 1.0f (원래 속도)로 되돌립니다.
        ApplySlow(collision, 1.0f);
    }

    /// <summary>
    /// 충돌한 대상에게 둔화 효과를 적용하거나 해제합니다.
    /// </summary>
    /// <param name="col">충돌한 대상의 Collider2D</param>
    /// <param name="multiplier">적용할 속도 배율 (1.0f = 정상, 0.5f = 50% 둔화)</param>
    void ApplySlow(Collider2D col, float multiplier)
    {
        // 리플렉션(Reflection) 대신 GetComponent를 사용해 성능을 확보합니다.

        // 1. 플레이어 (Player.cs)
        Player p = col.GetComponent<Player>();
        if (p != null)
        {
            p.SetSpeedMultiplier(multiplier);
            return;
        }

        // 2. 아군 (AllyAI.cs)
        AllyAI ally = col.GetComponent<AllyAI>();
        if (ally != null)
        {
            ally.SetSpeedMultiplier(multiplier);
            return;
        }

        // 3. 근접 적 (Enemy.cs)
        Enemy enemy = col.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.SetSpeedMultiplier(multiplier);
            return;
        }

        // 4. 원거리 적 (RangedEnemy.cs)
        RangedEnemy rangedEnemy = col.GetComponent<RangedEnemy>();
        if (rangedEnemy != null)
        {
            rangedEnemy.SetSpeedMultiplier(multiplier);
            return;
        }
    }
}