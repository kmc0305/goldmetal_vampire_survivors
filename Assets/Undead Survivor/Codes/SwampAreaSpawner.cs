using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 게임 시작 시 늪지대(SwampArea) 오브젝트를 맵에 랜덤하게 배치하는 스포너입니다.
/// </summary>
public class SwampAreaSpawner : MonoBehaviour
{
    [Header("Swamp Prefab 설정")]
    [Tooltip("SwampArea.cs가 부착된 늪지대 프리팹")]
    public GameObject swampAreaPrefab;

    [Header("배치 설정")]
    [Tooltip("게임 시작 시 생성할 늪지대 개수")]
    public int numberOfSwamps = 5;

    [Header("맵 경계 설정")]
    [Tooltip("늪지대가 배치될 맵의 절반 길이 (예: 50이면 -50부터 +50까지)")]
    public float mapHalfSize = 50f;
    [Tooltip("생성 시 플레이어 주변에서 최소 이 거리 이상 떨어진 곳에 배치")]
    public float minDistanceToPlayer = 15f;

    void Start()
    {
        if (swampAreaPrefab == null)
        {
            // 오류 수정: UnityEngine.Debug으로 명시적 지정
            UnityEngine.Debug.LogError("SwampAreaSpawner: swampAreaPrefab이 할당되지 않았습니다! 프리팹을 할당해주세요.");
            return;
        }

        // 플레이어 위치를 가져옵니다. (없으면 맵 중앙을 기준으로 합니다)
        UnityEngine.Vector3 playerPos = UnityEngine.Vector3.zero;
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            playerPos = GameManager.instance.player.transform.position;
        }

        for (int i = 0; i < numberOfSwamps; i++)
        {
            SpawnSwampArea(playerPos);
        }

        // 스포너 자신은 역할을 다 했으므로 비활성화하거나 파괴할 수 있습니다.
        // Destroy(gameObject); // (선택 사항: 씬 계층 구조를 깨끗하게 유지)
    }

    /// <summary>
    /// 늪지대를 랜덤한 위치에 생성하고, 플레이어로부터 최소 거리를 확보합니다.
    /// </summary>
    void SpawnSwampArea(UnityEngine.Vector3 excludeCenter)
    {
        UnityEngine.Vector3 spawnPosition = UnityEngine.Vector3.zero;
        int maxAttempts = 30; // 최대 시도 횟수

        for (int i = 0; i < maxAttempts; i++)
        {
            // 1. 맵 경계 내에서 랜덤 좌표 생성
            float x = Random.Range(-mapHalfSize, mapHalfSize);
            float y = Random.Range(-mapHalfSize, mapHalfSize);
            spawnPosition = new UnityEngine.Vector3(x, y, 0);

            // 2. 플레이어와의 최소 거리 확인
            if (UnityEngine.Vector3.Distance(spawnPosition, excludeCenter) >= minDistanceToPlayer)
            {
                // 조건 만족: 생성
                GameObject swamp = Instantiate(swampAreaPrefab, spawnPosition, UnityEngine.Quaternion.identity);
                // 스포너의 자식으로 유지하여 Hierarchy를 정리할 수 있습니다.
                swamp.transform.SetParent(this.transform);

                // 오류 수정: UnityEngine.Debug으로 명시적 지정
                UnityEngine.Debug.Log($"Swamp Area spawned at {spawnPosition}.");
                return;
            }
        }

        // 최대 시도 횟수 초과 시
        // 오류 수정: UnityEngine.Debug으로 명시적 지정
        UnityEngine.Debug.LogWarning("Swamp Area: 최대 시도 횟수 내에 적절한 스폰 위치를 찾지 못했습니다.");
    }
}