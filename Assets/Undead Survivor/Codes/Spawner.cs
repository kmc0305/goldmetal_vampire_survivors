// Spawner.cs 교체/추가 부분만 발췌
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static Spawner Instance { get; private set; }

    [Header("난이도 / 스폰 데이터 (스폰 간격용)")]
    public SpawnData[] spawnData;   // 비어 있어도 동작하도록 방어코드 처리
    public float Lv_Time = 10f;     // (원한다면 유지)

    [Header("타워 활성화 타이머")]
    public float activateInterval = 10f;  // ★ 10초마다 다음 성 활성화
    float activateTimer = 0f;

    readonly List<SpawnPoint> points = new List<SpawnPoint>();
    int prevLevel = -1;
    public int Level { get; private set; } = 0;

    public SpawnData CurrentSpawnData
    {
        get
        {
            if (spawnData == null || spawnData.Length == 0) return null;
            return spawnData[Mathf.Clamp(Level, 0, spawnData.Length - 1)];
        }
    }

    void Awake()
    {
        Instance = this;
        points.Clear();
        GetComponentsInChildren(true, points); // 또는 씬 전역 수집 버전이면 FindObjectsOfType 사용

        foreach (var p in points) p?.ResetRuntimeFlags();
    }

    void Start()
    {
        ActivateOneVirginPoint();          // 시작: 왼쪽 성 1개 켜기
        prevLevel = ComputeLevel();        // (spawnData 없어도 무관)
        Level = prevLevel;
    }

    void Update()
    {
        // ① 스폰 난이도(Level)는 기존대로(선택)
        Level = ComputeLevel();

        // ② ★ 타워 활성화는 별도 타이머로 진행 (spawnData 개수와 무관)
        activateTimer += Time.deltaTime;
        if (activateTimer >= activateInterval)
        {
            activateTimer = 0f;
            ActivateOneVirginPoint();      // 다음 성 하나 더 켬
        }
    }

    int ComputeLevel()
    {
        // spawnData 없어도 0 반환 (스폰 포인트 활성화에는 영향 X)
        if (spawnData == null || spawnData.Length == 0) return 0;
        float t = GameManager.instance.gameTime;
        return Mathf.Min(Mathf.FloorToInt(t / Lv_Time), spawnData.Length - 1);
    }

    void ActivateOneVirginPoint()
    {
        foreach (var p in points)
        {
            if (p == null) continue;
            if (!p.PermanentlyOff && !p.EverActivated)
            {
                if (p.ActivateOnce())
                    return; // 한 번에 1개만
            }
        }
    }

}
[System.Serializable]
public class SpawnData
{
    public float spawnTime; // SpawnPoint가 useSpawnerSpawnTime = true일 때 쓰는 간격(초)
    public int spriteType;
    public int health;
    public float speed;
}