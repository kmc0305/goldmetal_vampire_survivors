using UnityEngine;

/// <summary>
/// [맵 생성기]
/// 게임 시작 시 정해진 범위 내에 장애물(늪 등)을 랜덤하게 배치합니다.
/// System 네임스페이스와의 충돌을 방지하기 위해 UnityEngine 타입을 명시합니다.
/// </summary>
public class MapSpawner : MonoBehaviour
{
    [Header("생성 설정")]
    public GameObject swampPrefab; // 생성할 늪 프리팹
    public int swampCount = 30;    // 늪 생성 개수

    [Header("맵 범위")]
    public UnityEngine.Vector2 mapSize = new UnityEngine.Vector2(100f, 100f);

    void Start()
    {
        SpawnSwamps();
    }

    void SpawnSwamps()
    {
        if (swampPrefab == null) return;

        for (int i = 0; i < swampCount; i++)
        {
            // 1. 랜덤 좌표 생성 (UnityEngine.Random 사용 명시)
            float x = UnityEngine.Random.Range(-mapSize.x, mapSize.x);
            float y = UnityEngine.Random.Range(-mapSize.y, mapSize.y);

            // 2. 위치 벡터 생성 (UnityEngine.Vector3 사용 명시)
            UnityEngine.Vector3 spawnPos = new UnityEngine.Vector3(x, y, 0);

            // 3. 늪 생성 및 배치 (UnityEngine.Quaternion 사용 명시)
            GameObject swamp = Instantiate(swampPrefab, spawnPos, UnityEngine.Quaternion.identity);
            swamp.transform.parent = this.transform;

            // 4. 크기 랜덤 설정 (UnityEngine.Random 및 Vector3 명시)
            float randomScale = UnityEngine.Random.Range(0.8f, 1.5f);
            swamp.transform.localScale = UnityEngine.Vector3.one * randomScale;
        }
    }
}