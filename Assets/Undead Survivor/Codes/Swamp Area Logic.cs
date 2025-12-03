using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// AreaType Enum을 GoldMetal.Survivors 네임스페이스 안으로 이동하여 SwampAreaSpawner와 참조 통일
namespace GoldMetal.Survivors
{
    // 새로운 지형 타입을 정의하는 Enum을 추가합니다.
    public enum AreaType
    {
        Swamp,   // 기존 늪 (남동쪽, 이동속도 감소)
        Snow,    // 눈 지형 (북쪽, 이동속도 증가)
        Healing  // 힐 지형 (남서쪽, 유닛 힐)
    }

    public class SwampAreaLogic : MonoBehaviour
    {
        // 지형의 타입을 인스펙터에서 설정할 수 있도록 public 변수로 선언합니다.
        public AreaType areaType = AreaType.Swamp; // 기본값은 늪으로 유지
        public float effectDuration = 1.0f; // 효과 적용 주기 (초)
        public float effectValue = 0.5f; // 늪: 속도 감소 배율, 눈: 속도 증가 배율, 힐: 회복량

        [Header("진영 설정 (Layer System)")]
        [Tooltip("이 지형이 영향을 주는 대상의 레이어")]
        public LayerMask targetLayer; // 인스펙터에서 Ally 또는 Enemy 레이어 선택

        [Header("시각 효과 색상")]
        public Color swampTint = new Color(0.6f, 0.4f, 0.2f, 1f);   // 갈색 (둔화)
        public Color snowTint = new Color(0.7f, 0.9f, 1f, 1f);      // 하늘색 (가속)
        public Color healingTint = new Color(0.5f, 1f, 0.5f, 1f);   // 연녹색 (힐)

        private Dictionary<GameObject, Coroutine> activeCoroutines = new Dictionary<GameObject, Coroutine>();

        // [최적화] WaitForSeconds 캐싱
        private WaitForSeconds cachedEffectWait;

        private void Awake()
        {
            cachedEffectWait = new WaitForSeconds(effectDuration);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 레이어 마스크로 대상 확인
            if (((1 << other.gameObject.layer) & targetLayer) == 0) return;

            // 유닛인지 확인 (UnitMover2D 또는 Targetable 컴포넌트 필요)
            bool hasUnitMover = other.GetComponent<UnitMover2D>() != null;
            bool hasTargetable = other.GetComponent<Targetable>() != null;
            if (!hasUnitMover && !hasTargetable) return;

            // 이미 효과가 적용 중인지 확인하여 중복 실행을 방지합니다.
            if (!activeCoroutines.ContainsKey(other.gameObject))
            {
                Debug.Log($"[SwampAreaLogic] Applying {areaType} effect to {other.name}");
                // 코루틴을 시작하고 딕셔너리에 추가합니다.
                Coroutine coroutine = StartCoroutine(ApplyEffect(other.gameObject));
                activeCoroutines.Add(other.gameObject, coroutine);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // 대상이 영역을 벗어났을 때 효과를 멈춥니다.
            if (activeCoroutines.ContainsKey(other.gameObject))
            {
                // 해당 코루틴을 멈추고 딕셔너리에서 제거합니다.
                StopCoroutine(activeCoroutines[other.gameObject]);
                activeCoroutines.Remove(other.gameObject);

                // 시각 효과 색상 제거
                Targetable targetable = other.GetComponent<Targetable>();
                if (targetable != null)
                {
                    targetable.RemoveTerrainTint();
                }

                // 속도 효과를 되돌립니다.
                if (areaType == AreaType.Swamp || areaType == AreaType.Snow)
                {
                    UnitMover2D unitMover = other.GetComponent<UnitMover2D>();
                    if (unitMover != null)
                    {
                        unitMover.ResetSpeedMultiplier();
                    }

                    AllyAI allyAI = other.GetComponent<AllyAI>();
                    if (allyAI != null)
                    {
                        allyAI.speedMultiplier = 1f;
                    }

                    Player player = other.GetComponent<Player>();
                    if (player != null)
                    {
                        player.speedMultiplier = 1f;
                    }
                }
            }
        }

        IEnumerator ApplyEffect(GameObject target)
        {
            // 대상 유닛의 컴포넌트들을 가져옵니다.
            UnitMover2D unitMover = target.GetComponent<UnitMover2D>();
            Targetable targetable = target.GetComponent<Targetable>();
            AllyAI allyAI = target.GetComponent<AllyAI>();
            Player player = target.GetComponent<Player>();

            // 시각 효과 색상 적용
            if (targetable != null)
            {
                Color tint = GetTintColor();
                targetable.ApplyTerrainTint(tint);
            }

            // 속도 변경 로직 (진입 시 한 번 적용)
            if (areaType == AreaType.Swamp || areaType == AreaType.Snow)
            {
                float newSpeedMultiplier;
                if (areaType == AreaType.Swamp)
                {
                    newSpeedMultiplier = 1f - effectValue; // 늪: 속도 감소
                }
                else
                {
                    newSpeedMultiplier = 1f + effectValue; // 눈: 속도 증가
                }

                // UnitMover2D가 있으면 적용
                if (unitMover != null)
                {
                    unitMover.ApplySpeedMultiplier(newSpeedMultiplier);
                }

                // AllyAI가 있으면 적용
                if (allyAI != null)
                {
                    allyAI.speedMultiplier = newSpeedMultiplier;
                }

                // Player가 있으면 적용
                if (player != null)
                {
                    player.speedMultiplier = newSpeedMultiplier;
                }

                Debug.Log($"[SwampAreaLogic] {target.name} speed multiplier set to {newSpeedMultiplier}");
            }

            // 지속 효과 (힐링 - 매 주기마다 적용)
            while (true)
            {
                if (areaType == AreaType.Healing)
                {
                    if (targetable != null && !targetable.isDead)
                    {
                        targetable.Heal(effectValue);
                    }
                }

                // [최적화] 캐싱된 WaitForSeconds 사용
                yield return cachedEffectWait;
            }
        }

        // 지형 타입에 따른 색상 반환
        private Color GetTintColor()
        {
            switch (areaType)
            {
                case AreaType.Swamp:
                    return swampTint;
                case AreaType.Snow:
                    return snowTint;
                case AreaType.Healing:
                    return healingTint;
                default:
                    return Color.white;
            }
        }
    }
}