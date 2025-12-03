using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵에 힐 장판(Statue + HealingArea)을 주기적으로 생성하는 스포너입니다.
/// [수정됨]: 프리팹을 사용하여 오브젝트를 생성하며, 원점 주변 제약을 해제합니다.
/// </summary>
public class StatueSpawner : MonoBehaviour
{
    [Header("프리팹 설정")]
    [Tooltip("HealingArea.cs와 Collider2D가 부착된 힐 장판 프리팹")]
    public GameObject healingStatuePrefab;

    [Header("생성 설정")]
    public float spawnInterval = 45f; // 힐 장판 생성 주기 (초)
    private float timer;

    // [제거됨] public float minSpawnDistance = 30f; // 원점(0,0)으로부터 최소 거리

    private void Start()
    {
        SpawnHealingArea();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnHealingArea();
        }
    }

    /// <summary>
    /// 힐 장판 프리팹을 무작위 위치에 생성합니다. (위치 제약 없음)
    /// </summary>
    void SpawnHealingArea()
    {
        if (healingStatuePrefab == null)
        {
            UnityEngine.Debug.LogError("StatueSpawner: healingStatuePrefab이 할당되지 않았습니다! 프리팹을 할당해주세요.");
            return;
        }

        UnityEngine.Vector3 spawnPosition = UnityEngine.Vector3.zero;

        // ★ 수정된 로직: 위치 제약 없이 맵 중앙 영역 내 무작위 위치 선정
        // 맵 크기(-50f, 50f)를 가정하고, 그 안에 무작위 위치를 선정합니다.
        float x = UnityEngine.Random.Range(-50f, 50f);
        float y = UnityEngine.Random.Range(-50f, 50f);
        spawnPosition = new UnityEngine.Vector3(x, y, 0);

        // 2. 프리팹 인스턴스화 (Instantiation) 및 생성
        GameObject healingArea = Instantiate(healingStatuePrefab, spawnPosition, UnityEngine.Quaternion.identity);

        UnityEngine.Debug.Log($"Healing Statue spawned at {spawnPosition}.");
    }
}
