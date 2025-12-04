using UnityEngine;
using Random = UnityEngine.Random;
using System.Collections.Generic;

/// <summary>
/// 게임 시작 시 다양한 지형 효과 영역(Swamp, Snow, Healing)을 맵에 랜덤하게 배치하는 스포너입니다.
/// 성 좌표 기준:
/// - 북쪽: 눈 지형 (Snow)
/// - 남쪽 + 서쪽: 힐링 지형 (Healing)
/// - 남쪽 + 동쪽: 늪 지형 (Swamp)
/// </summary>
public class SwampAreaSpawner : MonoBehaviour
{
    [System.Serializable]
    public class TerrainSpawnConfig
    {
        [Tooltip("지형 효과 프리팹 (SwampArea 컴포넌트가 부착되어야 함)")]
        public GameObject prefab;
        [Tooltip("생성할 개수")]
        public int count = 3;
        [Tooltip("이 지형의 속도 배율 (Swamp: 0.5, Snow: 1.3 등)")]
        public float speedMultiplier = 0.5f;
        [Tooltip("지형 효과 색상")]
        public Color tintColor = Color.white;
    }

    [Header("성(기준점) 설정")]
    [Tooltip("성 타워의 Transform (비워두면 Castle 태그로 자동 검색)")]
    public Transform castleTransform;

    [Header("지형 프리팹 설정")]
    [Tooltip("늪지대 (속도 감소) - 남쪽+동쪽에 배치")]
    public TerrainSpawnConfig swampConfig;
    [Tooltip("눈 지역 (속도 증가) - 북쪽에 배치")]
    public TerrainSpawnConfig snowConfig;
    [Tooltip("힐링 지역 (체력 회복) - 남쪽+서쪽에 배치")]
    public TerrainSpawnConfig healingConfig;

    [Header("맵 경계 설정")]
    [Tooltip("지형이 배치될 맵의 절반 길이 (예: 50이면 -50부터 +50까지)")]
    public float mapHalfSize = 50f;
    [Tooltip("성 주변에서 최소 이 거리 이상 떨어진 곳에 배치")]
    public float minDistanceToCastle = 15f;
    [Tooltip("지형끼리 최소 이 거리 이상 떨어져서 배치")]
    public float minDistanceBetweenAreas = 10f;
    [Tooltip("지형 콜라이더 반경 (겹침 방지용)")]
    public float terrainRadius = 5f;

    // 이미 배치된 지형 위치 및 반경 추적
    private List<Vector3> spawnedPositions = new List<Vector3>();
    private Vector3 castlePosition;

    void Start()
    {
        // 성 위치 찾기
        if (castleTransform == null)
        {
            GameObject castleObj = GameObject.FindGameObjectWithTag("Castle");
            if (castleObj != null)
            {
                castleTransform = castleObj.transform;
            }
        }

        castlePosition = castleTransform != null ? castleTransform.position : Vector3.zero;

        // 각 지형 타입별로 지정된 구역에 스폰
        // 눈: 북쪽 (y > castleY)
        SpawnTerrainInRegion(snowConfig, SwampArea.AreaType.Snow, SpawnRegion.North);
        // 힐링: 남쪽 + 서쪽 (y < castleY && x < castleX)
        SpawnTerrainInRegion(healingConfig, SwampArea.AreaType.Healing, SpawnRegion.SouthWest);
        // 늪: 남쪽 + 동쪽 (y < castleY && x > castleX)
        SpawnTerrainInRegion(swampConfig, SwampArea.AreaType.Swamp, SpawnRegion.SouthEast);
    }

    enum SpawnRegion
    {
        North,      // 성 북쪽 (y > castleY)
        SouthWest,  // 성 남쪽 + 서쪽 (y < castleY && x < castleX)
        SouthEast   // 성 남쪽 + 동쪽 (y < castleY && x >= castleX)
    }

    void SpawnTerrainInRegion(TerrainSpawnConfig config, SwampArea.AreaType areaType, SpawnRegion region)
    {
        if (config == null || config.prefab == null || config.count <= 0) return;

        for (int i = 0; i < config.count; i++)
        {
            SpawnSingleAreaInRegion(config, areaType, region);
        }
    }

    void SpawnSingleAreaInRegion(TerrainSpawnConfig config, SwampArea.AreaType areaType, SpawnRegion region)
    {
        Vector3 spawnPosition = Vector3.zero;
        int maxAttempts = 50;

        for (int i = 0; i < maxAttempts; i++)
        {
            // 구역에 맞는 랜덤 좌표 생성
            spawnPosition = GetRandomPositionInRegion(region);

            // 성과의 거리 확인
            if (Vector3.Distance(spawnPosition, castlePosition) < minDistanceToCastle)
                continue;

            // 다른 지형과의 거리 확인 (겹침 방지)
            bool tooClose = false;
            foreach (var pos in spawnedPositions)
            {
                // 두 지형의 반경을 고려한 최소 거리
                float requiredDist = Mathf.Max(minDistanceBetweenAreas, terrainRadius * 2f);
                if (Vector3.Distance(spawnPosition, pos) < requiredDist)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            // 조건 만족: 생성
            GameObject area = Instantiate(config.prefab, spawnPosition, Quaternion.identity);
            area.transform.SetParent(this.transform);

            // SwampArea 컴포넌트 설정
            SwampArea swampArea = area.GetComponent<SwampArea>();
            if (swampArea != null)
            {
                swampArea.areaType = areaType;
                swampArea.speedMultiplier = config.speedMultiplier;
                swampArea.areaTintColor = config.tintColor;
            }

            spawnedPositions.Add(spawnPosition);
            return;
        }

        UnityEngine.Debug.LogWarning($"TerrainSpawner: {areaType} 지형({region}) 스폰 위치를 찾지 못했습니다.");
    }

    Vector3 GetRandomPositionInRegion(SpawnRegion region)
    {
        float x, y;
        float cx = castlePosition.x;
        float cy = castlePosition.y;

        switch (region)
        {
            case SpawnRegion.North:
                // 성 북쪽: y는 성보다 위, x는 전체 범위
                x = Random.Range(-mapHalfSize, mapHalfSize);
                y = Random.Range(cy + minDistanceToCastle * 0.5f, mapHalfSize);
                break;

            case SpawnRegion.SouthWest:
                // 성 남쪽 + 서쪽: y는 성보다 아래, x는 성보다 왼쪽
                x = Random.Range(-mapHalfSize, cx);
                y = Random.Range(-mapHalfSize, cy - minDistanceToCastle * 0.5f);
                break;

            case SpawnRegion.SouthEast:
                // 성 남쪽 + 동쪽: y는 성보다 아래, x는 성보다 오른쪽
                x = Random.Range(cx, mapHalfSize);
                y = Random.Range(-mapHalfSize, cy - minDistanceToCastle * 0.5f);
                break;

            default:
                x = Random.Range(-mapHalfSize, mapHalfSize);
                y = Random.Range(-mapHalfSize, mapHalfSize);
                break;
        }

        return new Vector3(x, y, 0);
    }
}