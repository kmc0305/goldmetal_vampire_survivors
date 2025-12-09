using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오브젝트 풀링(Object Pooling)을 관리하는 클래스입니다.
/// 오브젝트를 반복적으로 생성(Instantiate)하고 파괴(Destroy)하는 대신,
/// 미리 만들어 둔 오브젝트를 재활용(SetActive)하여 성능을 최적화합니다.
/// 
/// (예: 총알 100개를 쏘는 경우, 100개를 Destroy하고 100개를 Instantiate하는 것이 아니라,
///  '비활성화'된 총알 100개를 다시 '활성화'시켜 재사용합니다.)
/// </summary>
public class PoolManager : MonoBehaviour
{
    /// <summary>
    /// [핵심 설정] 인스펙터에서 재활용할 모든 원본 프리팹(Prefab)들을 등록합니다.
    /// (예: prefabs[0] = EnemyA, prefabs[1] = EnemyB, prefabs[2] = AllyUnitA, prefabs[3] = ExpGem)
    /// **이 배열의 '순서(인덱스)'가 매우 중요합니다.**
    /// </summary>
    public GameObject[] prefabs;

    /// <summary>
    /// 각 프리팹(Prefab)에 해당하는 오브젝트 풀(Pool)을 담을 리스트 배열입니다.
    /// (예: pools[0] = EnemyA 리스트, pools[1] = EnemyB 리스트, ...)
    /// </summary>
    List<GameObject>[] pools;

    /// <summary>
    /// [Unity 이벤트] Awake() - 스크립트가 로드될 때 1회 호출
    /// </summary>
    void Awake()
    {
        // 1. 'prefabs' 배열의 크기(개수)만큼 'pools' 리스트 배열의 공간을 확보합니다.
        pools = new List<GameObject>[prefabs.Length];

        // 2. 'pools' 배열의 각 칸을 빈 리스트(List<GameObject>)로 초기화합니다.
        //    (이 작업을 하지 않으면 pools[index]는 null 상태가 되어 에러가 납니다.)
        for (int index = 0; index < pools.Length; index++)
        {
            pools[index] = new List<GameObject>();
        }
    }

    /// <summary>
    /// [핵심 함수] 지정된 인덱스(index)에 해당하는 프리팹(Prefab)의 오브젝트를 풀(Pool)에서 가져옵니다.
    /// (Spawner.cs, AllySpawner.cs, Targetable.cs, Weapon.cs 등에서 호출됩니다.)
    /// </summary>
    /// <param name="index">가져올 프리팹의 'prefabs' 배열 인덱스 (순서)</param>
    /// <returns>활성화된(SetActive(true)) 게임 오브젝트</returns>
    public GameObject Get(int index)
    {
        GameObject select = null; // 반환할 게임 오브젝트 (초기값 null)

        // --- 1. 재활용할 오브젝트 탐색 ---
        // 해당 인덱스(index)의 풀(Pool) 리스트를 순회(foreach)합니다.
        foreach (GameObject item in pools[index])
        {
            // 만약 풀(Pool) 안에 '비활성화'(activeSelf == false)된 오브젝트가 있다면
            // (즉, 현재 사용 중이지 않고 '쉬고 있는' 오브젝트가 있다면)
            if (!item.activeSelf)
            {
                // 그 오브젝트를 'select' 변수에 저장하고
                select = item;
                // 즉시 활성화(SetActive(true))시킵니다.
                // (이때 이 오브젝트에 붙어있는 스크립트들의 OnEnable() 함수가 호출됩니다.)
                select.SetActive(true);
                // 탐색을 중지(break)합니다. (하나 찾았으면 됐음)
                break;
            }
        }

        // --- 2. 재활용할 오브젝트가 없는 경우 (새로 생성) ---
        // 위에서 탐색했지만 'select'가 여전히 null이라면
        // (즉, 풀이 비었거나, 풀에 있는 모든 오브젝트가 현재 사용 중(활성화)이라면)
        if (!select)
        {
            // 'prefabs[index]' (원본 프리팹)를 새로 생성(Instantiate)하고, 
            // 새로 생성된 오브젝트의 부모(parent)를 이 PoolManager 오브젝트(transform)로 설정합니다.
            // (Hierarchy 뷰가 깔끔해집니다.)
            select = Instantiate(prefabs[index], transform);

            // [중요] 새로 생성한 오브젝트를 해당 풀(Pool) 리스트에 추가(Add)합니다.
            // (이렇게 해야 다음번에 이 오브젝트를 재활용할 수 있습니다.)
            pools[index].Add(select);
        }

        // 3. 선택된 오브젝트(select)를 반환합니다. (재활용했든, 새로 만들었든)
        return select;
    }


    /// <summary>
    /// 특정 인덱스(index)의 풀(Pool)에 있는 모든 오브젝트를 비활성화합니다.
    /// (예: 특정 종류의 적만 모두 제거할 때)
    /// </summary>
    public void Clear(int index)
    {
        foreach (GameObject item in pools[index])
        {
            // SetActive(false)를 호출하면, 해당 오브젝트 스크립트의
            // OnDisable() 함수가 호출됩니다.
            item.SetActive(false);
        }
    }

    /// <summary>
    /// 모든 풀(Pool)의 모든 오브젝트를 비활성화합니다.
    /// (예: 스테이지 클리어 또는 게임 오버 시)
    /// </summary>
    public void ClearAll()
    {
        // 모든 'pools' 배열을 순회
        for (int index = 0; index < pools.Length; index++)
        {
            // 해당 풀의 모든 'item'을 순회하며 비활성화
            foreach (GameObject item in pools[index])
                item.SetActive(false);
        }
    }
}
