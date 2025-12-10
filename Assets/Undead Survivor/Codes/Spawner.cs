using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // 리스트 랜덤 섞기를 위해 필요

public class Spawner : MonoBehaviour
{
    public static Spawner Instance { get; private set; }

    [Header("난이도 / 스폰 데이터")]
    public SpawnData[] spawnData;
    public float Lv_Time = 10f;
    public int Level { get; private set; } = 0;

    [Header("타워 활성화 순서")]
    public List<SpawnPoint> towerSequence;

    [Header("활성화 간격 (초)")]
    public float activationInterval = 10f;

    [Header("보스 스폰 설정 (시간/마리수)")]
    public List<BossWaveData> bossWaves = new List<BossWaveData>()
    {
        new BossWaveData(300f, 1),  // 5분 (300초) -> 1마리
        new BossWaveData(600f, 2),  // 10분 (600초) -> 2마리
        new BossWaveData(900f, 3)   // 15분 (900초) -> 3마리
    };

    void Awake()
    {
        Instance = this;
        foreach (var tower in towerSequence)
        {
            if (tower != null) tower.ResetRuntimeFlags();
        }

        // 보스 웨이브 상태 초기화
        foreach (var wave in bossWaves) wave.hasTriggered = false;
    }

    void Start()
    {
        StartCoroutine(ActivationRoutine());
    }

    void Update()
    {
        if (GameManager.instance == null) return;

        float t = GameManager.instance.gameTime;

        // 레벨 계산
        if (spawnData != null && spawnData.Length > 0)
        {
            Level = Mathf.Min(Mathf.FloorToInt(t / Lv_Time), spawnData.Length - 1);
        }

        // ★ 보스 스폰 시간 체크
        CheckBossSpawnTime(t);
    }

    // ★ 시간대별 보스 소환 체크 로직
    void CheckBossSpawnTime(float currentTime)
    {
        foreach (var wave in bossWaves)
        {
            if (!wave.hasTriggered && currentTime >= wave.spawnTime)
            {
                wave.hasTriggered = true;
                SpawnBossesRandomly(wave.count);
            }
        }
    }

    // ★ 랜덤한 활성 타워에서 보스 소환
    void SpawnBossesRandomly(int countToSpawn)
    {
        // 1. 현재 활성화되어 있고(IsEnabled), 파괴되지 않은(!PermanentlyOff) 타워만 추림
        List<SpawnPoint> activeTowers = towerSequence
            .Where(t => t != null && t.IsEnabled && !t.PermanentlyOff)
            .ToList();

        if (activeTowers.Count == 0)
        {
            Debug.LogWarning("보스를 소환하려 했으나 활성화된 타워가 하나도 없습니다.");
            return;
        }

        // 2. 소환해야 할 수와 타워 수 중 작은 값 선택 (타워 수보다 많이 소환 못함)
        int actualSpawnCount = Mathf.Min(countToSpawn, activeTowers.Count);

        Debug.Log($"[{currentTimeFormatted()}] 보스 웨이브 시작! 목표: {countToSpawn}마리, 실제 소환: {actualSpawnCount}마리 (활성 타워: {activeTowers.Count})");

        // 3. 리스트를 랜덤하게 섞음 (Fisher-Yates Shuffle)
        for (int i = 0; i < activeTowers.Count; i++)
        {
            SpawnPoint temp = activeTowers[i];
            int randomIndex = Random.Range(i, activeTowers.Count);
            activeTowers[i] = activeTowers[randomIndex];
            activeTowers[randomIndex] = temp;
        }

        // 4. 앞에서부터 필요한 개수만큼 보스 소환
        for (int i = 0; i < actualSpawnCount; i++)
        {
            activeTowers[i].ForceSpawnBoss();
        }
    }

    string currentTimeFormatted()
    {
        float t = GameManager.instance.gameTime;
        return $"{Mathf.Floor(t / 60):00}:{Mathf.Floor(t % 60):00}";
    }

    IEnumerator ActivationRoutine()
    {
        foreach (var tower in towerSequence)
        {
            if (tower != null)
            {
                Debug.Log($"타워 활성화: {tower.name}");
                tower.ActivateOnce();
            }
            yield return new WaitForSeconds(activationInterval);
        }
    }

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

// ★ 보스 웨이브 관리용 클래스
[System.Serializable]
public class BossWaveData
{
    public float spawnTime; // 소환 시간 (초)
    public int count;       // 소환 마리 수
    [HideInInspector] public bool hasTriggered; // 실행 여부 체크

    public BossWaveData(float time, int count)
    {
        this.spawnTime = time;
        this.count = count;
        this.hasTriggered = false;
    }
}