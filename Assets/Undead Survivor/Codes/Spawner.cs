using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static Spawner Instance { get; private set; }

    [Header("난이도 / 스폰 데이터")]
    public SpawnData[] spawnData;
    public float Lv_Time = 10f;
    public int Level { get; private set; } = 0;

    [Header("타워 활성화 순서 (드래그해서 순서를 정해주세요)")]
    // ★ 여기에 EnemyTower1, 2, 3을 순서대로 넣으세요.
    public List<SpawnPoint> towerSequence;

    [Header("활성화 간격 (초)")]
    public float activationInterval = 10f; // 10초마다 다음 타워 활성화

    void Awake()
    {
        Instance = this;
        // 시작 전에 모든 타워의 상태를 리셋해줍니다.
        foreach (var tower in towerSequence)
        {
            if (tower != null) tower.ResetRuntimeFlags();
        }
    }

    void Start()
    {
        // 순차적 활성화 코루틴 시작
        StartCoroutine(ActivationRoutine());
    }

    void Update()
    {
        // 레벨 계산 (적 강해지는 로직) 유지
        if (spawnData != null && spawnData.Length > 0)
        {
            float t = GameManager.instance.gameTime;
            Level = Mathf.Min(Mathf.FloorToInt(t / Lv_Time), spawnData.Length - 1);
        }
    }

    // ★ 1개 켜고 -> 10초 대기 -> 다음 거 켜고 -> 반복
    IEnumerator ActivationRoutine()
    {
        foreach (var tower in towerSequence)
        {
            if (tower != null)
            {
                Debug.Log($"타워 활성화: {tower.name}");
                tower.ActivateOnce();
            }

            // 다음 타워를 켜기 전 대기 (마지막 타워 켜고 나서는 대기해도 상관없으니 단순하게 처리)
            yield return new WaitForSeconds(activationInterval);
        }
    }

    // 데이터 참조용
    public SpawnData CurrentSpawnData
    {
        get
        {
            if (spawnData == null || spawnData.Length == 0) return null;
            return spawnData[Mathf.Clamp(Level, 0, spawnData.Length - 1)];
        }
    }
}

[System.Serializable]
public class SpawnData
{
    public float spawnTime;
    public int spriteType;
    public int health;
    public float speed;
}