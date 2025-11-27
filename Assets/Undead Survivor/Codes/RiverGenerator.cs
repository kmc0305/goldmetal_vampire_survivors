using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵에 강(River) 오브젝트를 주기적으로 생성하고 관리하는 스크립트입니다.
/// [수정됨]: Prefab 대신 코드 내에서 GameObject 및 컴포넌트를 직접 생성합니다.
/// </summary>
public class RiverGenerator : MonoBehaviour
{
    [Header("강 생성 설정")]
    // public GameObject riverPrefab; // ★ 제거됨: Prefab을 사용하지 않습니다.
    public float spawnInterval = 30f; // 강 생성 주기 (초)
    private float timer;

    [Header("생성 위치 제한")]
    [Tooltip("강 생성 위치가 원점에서 최소 이 거리 이상 떨어져야 합니다.")]
    public float minSpawnDistance = 30f; // 원점(0,0)으로부터 최소 거리

    [Header("존속 시간 설정")]
    [Tooltip("강 오브젝트가 생성된 후 자동으로 사라지는 시간 (초)")]
    public float lifetime = 300f; // 5분 = 300초

    // --- 강 오브젝트의 임시 비주얼/충돌 설정 (코드에서만 설정) ---
    [Header("강 시각/충돌 설정")]
    public UnityEngine.Vector2 riverSize = new UnityEngine.Vector2(5f, 5f); // 강 영역 크기
    public UnityEngine.Color riverColor = new UnityEngine.Color(0.2f, 0.4f, 0.8f, 0.5f); // 강 색상 (반투명 파란색)

    [Header("시각적 디버그 설정")]
    [Tooltip("강이 다른 오브젝트 뒤에 가려지지 않도록 정렬 순서(Order)를 설정합니다.")]
    public int sortingOrder = -1; // 배경 타일보다 낮은 값으로 설정 (예: -1)


    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnRiver();
        }
    }

    /// <summary>
    /// 강 오브젝트를 코드에서 직접 생성하고 위치와 컴포넌트를 설정합니다.
    /// </summary>
    void SpawnRiver()
    {
        UnityEngine.Vector3 spawnPosition = UnityEngine.Vector3.zero;
        int maxAttempts = 10; // 무한 루프 방지를 위한 최대 시도 횟수

        // 1. 원점(0,0)에서 최소 거리 이상 떨어진 위치를 찾을 때까지 반복합니다.
        for (int i = 0; i < maxAttempts; i++)
        {
            // 맵의 최대 범위 내에서 무작위 위치를 선정합니다. (맵 크기에 따라 조정 필요)
            float x = UnityEngine.Random.Range(-50f, 50f);
            float y = UnityEngine.Random.Range(-50f, 50f);
            spawnPosition = new UnityEngine.Vector3(x, y, 0);

            // 원점에서부터의 거리를 확인합니다.
            if (spawnPosition.magnitude >= minSpawnDistance)
            {
                break;
            }

            if (i == maxAttempts - 1)
            {
                UnityEngine.Debug.LogWarning("RiverGenerator: 적절한 위치를 찾지 못했습니다. (최대 시도 횟수 초과)");
                // 마지막 시도까지 실패하면, 일단 minSpawnDistance 거리의 무작위 위치를 강제로 사용
                spawnPosition = UnityEngine.Random.onUnitSphere * minSpawnDistance;
                spawnPosition.z = 0; // 2D 게임이므로 z는 0으로 유지
            }
        }

        // 2. 강 오브젝트를 코드에서 직접 생성
        GameObject river = new GameObject("GeneratedRiver");
        river.transform.position = spawnPosition;
        river.transform.rotation = UnityEngine.Quaternion.identity;

        // 3. 필요한 컴포넌트 추가 및 설정 (시각적 + 충돌 + 기능)

        // 3-1. 시각적 표현 (SpriteRenderer) 추가 (임시 사각형 사용)
        SpriteRenderer sr = river.AddComponent<SpriteRenderer>();
        // ★ 수정: 유니티 기본 'Square' 스프라이트를 로드하여 할당합니다.
        // 이 과정이 없으면 SpriteRenderer는 스프라이트(Sprite)가 없어 화면에 아무것도 표시하지 않습니다.
        Sprite defaultSprite = Resources.Load<Sprite>("Square");
        if (defaultSprite != null)
        {
            sr.sprite = defaultSprite;
        }
        else
        {
            // Resource 폴더에 'Square'가 없을 경우를 대비한 경고
            UnityEngine.Debug.LogWarning("기본 'Square' 스프라이트를 찾을 수 없습니다. Resources 폴더에 넣어주세요.");
        }

        sr.color = riverColor;
        sr.sortingOrder = sortingOrder; // 오브젝트 정렬 순서 설정

        // 크기 설정 (riverSize에 맞게 로컬 스케일 조정)
        river.transform.localScale = new UnityEngine.Vector3(riverSize.x, riverSize.y, 1f);


        // 3-2. 충돌체 (Collider) 추가 (Trigger로 설정)
        BoxCollider2D col = river.AddComponent<BoxCollider2D>();
        // 콜라이더 크기는 이미 오브젝트 스케일에 맞춰졌으므로, size를 (1, 1)로 설정하여 크기 조정을 완료합니다.
        col.size = UnityEngine.Vector2.one;
        col.isTrigger = true; // 강은 통과 가능해야 하므로 Trigger로 설정

        // 3-3. 강 기능 (Swamp.cs) 스크립트 추가
        // 파일 목록에 Swamp.cs가 있으므로, 이 스크립트를 추가하여 기능을 활성화합니다.
        river.AddComponent<Swamp>();

        // 4. 5분 후 자동 제거를 위한 코루틴 시작
        StartCoroutine(DeactivateAfterTime(river));
    }

    /// <summary>
    /// 지정된 시간(초) 후에 오브젝트를 파괴합니다.
    /// </summary>
    IEnumerator DeactivateAfterTime(GameObject obj)
    {
        // obj가 null이 아닐 때만 대기 및 처리
        if (obj != null)
        {
            yield return new WaitForSeconds(lifetime);

            if (obj != null)
            {
                // 오브젝트를 파괴(Destroy)합니다.
                UnityEngine.Object.Destroy(obj);
                // ★ 수정된 부분: UnityEngine.Vector3로 명시적 참조
                UnityEngine.Debug.Log($"River object destroyed after {lifetime} seconds. Last position: {obj.transform.position}");
            }
        }
    }
}