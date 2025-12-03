using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 게임 시작 시 다양한 지형 효과 영역(Swamp, Snow, Healing)을 맵에 랜덤하게 배치하는 스포너입니다.
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

    [Header("지형 프리팹 설정")]
    [Tooltip("늪지대 (속도 감소)")]
    public TerrainSpawnConfig swampConfig;
    [Tooltip("눈 지역 (속도 증가)")]
    public TerrainSpawnConfig snowConfig;
    [Tooltip("힐링 지역 (체력 회복)")]
    public TerrainSpawnConfig healingConfig;

    [Header("맵 경계 설정")]
    [Tooltip("지형이 배치될 맵의 절반 길이 (예: 50이면 -50부터 +50까지)")]
    public float mapHalfSize = 50f;
    [Tooltip("생성 시 플레이어 주변에서 최소 이 거리 이상 떨어진 곳에 배치")]
    public float minDistanceToPlayer = 15f;
    [Tooltip("지형끼리 최소 이 거리 이상 떨어져서 배치")]
    public float minDistanceBetweenAreas = 10f;

    // 이미 배치된 지형 위치 추적
    private System.Collections.Generic.List<Vector3> spawnedPositions = new System.Collections.Generic.List<Vector3>();

    void Start()
    {
        // 플레이어 위치를 가져옵니다. (없으면 맵 중앙을 기준으로 합니다)
        Vector3 playerPos = Vector3.zero;
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            playerPos = GameManager.instance.player.transform.position;
        }

        // 각 지형 타입별로 스폰
        SpawnTerrainAreas(swampConfig, SwampArea.AreaType.Swamp, playerPos);
        SpawnTerrainAreas(snowConfig, SwampArea.AreaType.Snow, playerPos);
        SpawnTerrainAreas(healingConfig, SwampArea.AreaType.Healing, playerPos);
    }

    void SpawnTerrainAreas(TerrainSpawnConfig config, SwampArea.AreaType areaType, Vector3 playerPos)
    {
        if (config == null || config.prefab == null || config.count <= 0) return;

        for (int i = 0; i < config.count; i++)
        {
            SpawnSingleArea(config, areaType, playerPos);
        }
    }

    void SpawnSingleArea(TerrainSpawnConfig config, SwampArea.AreaType areaType, Vector3 excludeCenter)
    {
        Vector3 spawnPosition = Vector3.zero;
        int maxAttempts = 30;

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(-mapHalfSize, mapHalfSize);
            float y = Random.Range(-mapHalfSize, mapHalfSize);
            spawnPosition = new Vector3(x, y, 0);

            // 플레이어와의 거리 확인
            if (Vector3.Distance(spawnPosition, excludeCenter) < minDistanceToPlayer)
                continue;

            // 다른 지형과의 거리 확인
            bool tooClose = false;
            foreach (var pos in spawnedPositions)
            {
                if (Vector3.Distance(spawnPosition, pos) < minDistanceBetweenAreas)
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

        UnityEngine.Debug.LogWarning($"TerrainSpawner: {areaType} 지형 스폰 위치를 찾지 못했습니다.");
    }
}