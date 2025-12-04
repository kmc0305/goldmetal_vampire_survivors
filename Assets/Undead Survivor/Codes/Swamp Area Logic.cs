using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// [지형 효과 영역] 이 영역에 진입한 플레이어나 유닛에게 다양한 효과를 적용합니다.
/// - Swamp(늪): 이동 속도 감소
/// - Snow(눈): 이동 속도 증가
/// - Healing(힐): 지속적인 체력 회복 (음수 시 데미지)
/// 이 스크립트는 isTrigger가 활성화된 Collider2D에 부착되어야 합니다.
/// </summary>
public class SwampArea : MonoBehaviour
{
    public enum AreaType
    {
        Swamp,    // 늪지대 - 속도 감소
        Snow,     // 눈 지역 - 속도 증가
        Healing   // 힐링/데미지 지역 - 체력 회복 또는 감소
    }

    [Header("지형 타입 설정")]
    public AreaType areaType = AreaType.Swamp;

    [Header("적용 대상 레이어 설정")]
    [Tooltip("효과를 적용할 레이어 (아군: Layer 8, 적: Layer 9)")]
    public LayerMask targetLayers = ~0; // 기본: 모든 레이어

    [Header("속도 효과 설정")]
    [Tooltip("원래 속도에 곱해지는 배율 (예: 0.5는 50% 속도, 1.3은 130% 속도)")]
    public float speedMultiplier = 0.5f;

    [Header("힐링/데미지 효과 설정 (Healing 타입 전용)")]
    [Tooltip("초당 체력 변화량 (양수: 회복, 음수: 데미지)")]
    public float hpChangePerSecond = 2f;

    [Header("시각 효과 설정")]
    [Tooltip("지형 효과 색상 틴트")]
    public Color areaTintColor = new Color(0.5f, 0.8f, 0.5f, 1f); // 기본: 초록빛

    // 힐링/데미지 영역 내 유닛 추적용 (Healing 타입 전용)
    private HashSet<Targetable> unitsInHealingArea = new HashSet<Targetable>();
    private float healTimer = 0f;

    // ★ 컬렉션 수정 오류 방지를 위한 임시 리스트
    private readonly List<Targetable> unitsToProcessBuffer = new List<Targetable>();

    private void Update()
    {
        // Healing 타입일 때만 지속적인 힐/데미지 적용
        if (areaType != AreaType.Healing) return;
        if (unitsInHealingArea.Count == 0) return;

        healTimer += Time.deltaTime;
        if (healTimer >= 1f)
        {
            healTimer = 0f;

            // ★★★ [오류 해결] 안전하게 컬렉션 처리 시작 ★★★
            // 1. 순회용 임시 버퍼에 복사
            unitsToProcessBuffer.Clear();
            unitsToProcessBuffer.AddRange(unitsInHealingArea); // HashSet 내용을 List로 복사

            // 2. 복사된 리스트를 순회하면서 로직 적용 및 무효 유닛 제거
            for (int i = unitsToProcessBuffer.Count - 1; i >= 0; i--)
            {
                var target = unitsToProcessBuffer[i];

                if (target == null || !target.gameObject.activeInHierarchy || target.isDead)
                {
                    // 원본 컬렉션(HashSet)에서 무효 유닛을 제거 (수정 작업)
                    unitsInHealingArea.Remove(target);
                    continue; // 다음 유닛으로 이동
                }

                // 힐/데미지 적용 로직 (원래 foreach 내부의 로직)
                if (hpChangePerSecond >= 0)
                {
                    // 양수: 힐링
                    target.Heal(hpChangePerSecond);
                }
                else
                {
                    // 음수: 데미지 (절대값으로 변환), attacker는 지형 자체
                    target.TakeDamage(-hpChangePerSecond, transform);
                }
            }
            // ★★★ [오류 해결] 안전하게 컬렉션 처리 완료 ★★★
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
        // 레이어 필터 체크 - 대상 레이어에 포함되지 않으면 무시
        if ((targetLayers.value & (1 << col.gameObject.layer)) == 0)
            return;

        float multiplier = entering ? speedMultiplier : 1.0f;

        // Targetable 컴포넌트가 있으면 색상 틴트 적용/해제
        Targetable targetable = col.GetComponent<Targetable>();
        if (targetable != null)
        {
            if (entering)
            {
                targetable.ApplyTerrainTint(areaTintColor);
                // Healing 영역이면 유닛 추적 목록에 추가
                if (areaType == AreaType.Healing)
                {
                    unitsInHealingArea.Add(targetable);
                }
            }
            else
            {
                targetable.RemoveTerrainTint();
                // Healing 영역이면 유닛 추적 목록에서 제거
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