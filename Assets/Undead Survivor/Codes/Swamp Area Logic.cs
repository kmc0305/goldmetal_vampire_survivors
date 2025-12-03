using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// [지형 효과 영역] 이 영역에 진입한 플레이어나 유닛에게 다양한 효과를 적용합니다.
/// - Swamp(늪): 이동 속도 감소
/// - Snow(눈): 이동 속도 증가
/// - Healing(힐): 지속적인 체력 회복
/// 이 스크립트는 isTrigger가 활성화된 Collider2D에 부착되어야 합니다.
/// </summary>
public class SwampArea : MonoBehaviour
{
    public enum AreaType
    {
        Swamp,    // 늪지대 - 속도 감소
        Snow,     // 눈 지역 - 속도 증가
        Healing   // 힐링 지역 - 체력 회복
    }

    [Header("지형 타입 설정")]
    public AreaType areaType = AreaType.Swamp;

    [Header("속도 효과 설정")]
    [Tooltip("원래 속도에 곱해지는 배율 (예: 0.5는 50% 속도, 1.3은 130% 속도)")]
    public float speedMultiplier = 0.5f;

    [Header("힐링 효과 설정 (Healing 타입 전용)")]
    [Tooltip("힐링 지역에서 초당 회복되는 체력량")]
    public float healPerSecond = 2f;

    [Header("시각 효과 설정")]
    [Tooltip("지형 효과 색상 틴트")]
    public Color areaTintColor = new Color(0.5f, 0.8f, 0.5f, 1f); // 기본: 초록빛

    // 힐링 영역 내 유닛 추적용 (Healing 타입 전용)
    private HashSet<Targetable> unitsInHealingArea = new HashSet<Targetable>();
    private float healTimer = 0f;

    private void Update()
    {
        // 힐링 타입일 때만 지속적인 힐 적용
        if (areaType != AreaType.Healing) return;
        if (unitsInHealingArea.Count == 0) return;

        healTimer += Time.deltaTime;
        if (healTimer >= 1f)
        {
            healTimer = 0f;
            // 힐링 영역 내 모든 유닛에게 힐 적용
            unitsInHealingArea.RemoveWhere(t => t == null || !t.gameObject.activeInHierarchy);
            foreach (var target in unitsInHealingArea)
            {
                if (target != null && !target.isDead)
                {
                    target.Heal(healPerSecond);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ApplyAreaEffect(collision, true);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        ApplyAreaEffect(collision, false);
    }

    /// <summary>
    /// 충돌한 대상에게 지형 효과를 적용하거나 해제합니다.
    /// </summary>
    void ApplyAreaEffect(Collider2D col, bool entering)
    {
        float multiplier = entering ? speedMultiplier : 1.0f;

        // Targetable 컴포넌트가 있으면 색상 틴트 적용/해제
        Targetable targetable = col.GetComponent<Targetable>();
        if (targetable != null)
        {
            if (entering)
            {
                targetable.ApplyTerrainTint(areaTintColor);
                // 힐링 영역이면 유닛 추적 목록에 추가
                if (areaType == AreaType.Healing)
                {
                    unitsInHealingArea.Add(targetable);
                }
            }
            else
            {
                targetable.RemoveTerrainTint();
                // 힐링 영역이면 유닛 추적 목록에서 제거
                if (areaType == AreaType.Healing)
                {
                    unitsInHealingArea.Remove(targetable);
                }
            }
        }

        // 속도 효과 적용 (Swamp, Snow 타입)
        if (areaType == AreaType.Swamp || areaType == AreaType.Snow)
        {
            ApplySpeedEffect(col, multiplier);
        }
    }

    /// <summary>
    /// 이동 속도 효과를 적용합니다.
    /// </summary>
    void ApplySpeedEffect(Collider2D col, float multiplier)
    {
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

        // 5. UnitMover2D (수동 조작 유닛)
        UnitMover2D mover = col.GetComponent<UnitMover2D>();
        if (mover != null)
        {
            if (multiplier == 1.0f)
                mover.ResetSpeedMultiplier();
            else
                mover.ApplySpeedMultiplier(multiplier);
            return;
        }
    }
}