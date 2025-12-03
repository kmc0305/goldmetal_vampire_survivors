using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// SwampAreaLogic 클래스 및 AreaType Enum에 접근하기 위해 필요
using GoldMetal.Survivors;

namespace GoldMetal.Survivors
{
    // CS0101, CS0111 오류 해결: 클래스 이름을 GroundEffectSpawner로 변경하여 충돌을 방지합니다.
    public class GroundEffectSpawner : MonoBehaviour
    {
        // 인스펙터에서 연결할 프리팹들
        public GameObject _SwampArea;        // 늪 지형 (아군 둔화)
        public GameObject _Castle;           // 성 오브젝트 참조
        public GameObject _SnowAreaPrefab;   // 눈 지형 (아군 이속 증가)
        public GameObject _HealingAreaPrefab;// 힐 지형 (적군 힐)

        // 맵 크기 및 생성 관련 변수는 기존대로 유지한다고 가정합니다.
        private float mapSizeX = 100f;
        private float mapSizeY = 100f;

        // Start 메서드: 게임 시작 시 지형 스폰 코루틴 시작
        private void Start()
        {
            // 성 오브젝트 위치를 얻어옵니다.
            UnityEngine.Vector3 castlePosition = _Castle.transform.position;

            // 지형 스폰 코루틴을 시작합니다.
            StartCoroutine(SpawnSwampArea(castlePosition));
        }

        // 지형 생성 코루틴
        IEnumerator SpawnSwampArea(UnityEngine.Vector3 castlePosition)
        {
            // 맵 크기에 맞게 주기적으로 지형을 생성합니다.
            while (true)
            {
                // 맵 전체에서 랜덤 위치를 결정합니다.
                float randomX = UnityEngine.Random.Range(-mapSizeX / 2f, mapSizeX / 2f); // UnityEngine.Random로 수정
                float randomY = UnityEngine.Random.Range(-mapSizeY / 2f, mapSizeY / 2f); // UnityEngine.Random로 수정
                UnityEngine.Vector3 randomPos = new UnityEngine.Vector3(randomX, randomY, 0f); // UnityEngine.Vector3로 수정

                // 성의 위치를 기준으로 맵의 4분면을 나눕니다.
                float relativeX = randomPos.x - castlePosition.x;
                float relativeY = randomPos.y - castlePosition.y;

                GameObject areaToSpawn = null;
                AreaType currentAreaType = AreaType.Swamp;

                // 1. 북쪽 지역 (Y > castleY) - 눈 지형 (Snow, 아군 속도 증가)
                if (relativeY > 0)
                {
                    areaToSpawn = _SnowAreaPrefab;
                    currentAreaType = AreaType.Snow;
                }
                // 2. 남쪽 지역 (Y < castleY)
                else
                {
                    // 남서쪽 (X < castleX) - 힐 지형 (Healing, 적군 힐)
                    if (relativeX < 0)
                    {
                        areaToSpawn = _HealingAreaPrefab;
                        currentAreaType = AreaType.Healing;
                    }
                    // 남동쪽 (X >= castleX) - 늪 지형 (Swamp, 아군 속도 감소)
                    else
                    {
                        areaToSpawn = _SwampArea;
                        currentAreaType = AreaType.Swamp;
                    }
                }

                // 생성할 프리팹이 설정된 경우에만 생성합니다.
                if (areaToSpawn != null)
                {
                    // Quaternion 모호성 해결을 위해 UnityEngine.Quaternion 사용
                    GameObject newArea = Instantiate(areaToSpawn, randomPos, UnityEngine.Quaternion.identity);

                    // 생성된 오브젝트에 SwampAreaLogic 컴포넌트가 있다면 타입을 설정합니다.
                    SwampAreaLogic logic = newArea.GetComponent<SwampAreaLogic>();
                    if (logic != null)
                    {
                        logic.areaType = currentAreaType;

                        // 타입에 따라 효과값 및 타겟 레이어를 설정합니다. (레이어 시스템 반영)
                        if (currentAreaType == AreaType.Snow)
                        {
                            logic.effectValue = 0.3f; // 30% 속도 증가 (1.3배속)
                            logic.targetLayer = LayerMask.GetMask("Ally"); // 아군에게 이속 증가
                        }
                        else if (currentAreaType == AreaType.Healing)
                        {
                            logic.effectValue = 10.0f; // 초당 10 체력 회복
                            logic.targetLayer = LayerMask.GetMask("Enemy"); // 적에게 힐 적용
                        }
                        else if (currentAreaType == AreaType.Swamp)
                        {
                            logic.effectValue = 0.5f; // 50% 속도 감소 (0.5배속)
                            logic.targetLayer = LayerMask.GetMask("Ally"); // 아군에게 이속 감소
                        }
                    }

                    // Reposition 스크립트 처리
                    Reposition reposition = newArea.GetComponent<Reposition>();
                    if (reposition != null)
                    {
                        // Reposition.Init() 호출은 정의되지 않은 메서드 오류(CS1061)를 피하기 위해 주석 처리
                        // reposition.Init(); 
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Area to spawn is null for type: " + currentAreaType.ToString());
                }

                yield return new UnityEngine.WaitForSeconds(5f); // 5초마다 지형 생성 (UnityEngine.WaitForSeconds 사용)
            }
        }
    }
}