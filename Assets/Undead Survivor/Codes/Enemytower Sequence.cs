using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyTowerSequence : MonoBehaviour
{
    public float interval = 10f; // 10초마다 다음 성 활성화
    List<EnemyTowerController> towers;

    void Awake()
    {
        towers = GetComponentsInChildren<EnemyTowerController>(true)
                    .OrderBy(t => t.transform.position.x)    // 왼→오 정렬
                    .ToList();
    }

    void Start()
    {
        // 전부 비활성으로 시작(안전)
        foreach (var t in towers) t.Deactivate();

        StartCoroutine(Seq());
    }

    IEnumerator Seq()
    {
        for (int i = 0; i < towers.Count; i++)
        {
            towers[i].Activate();                 // 활성화 → active 스프라이트 → Spawner.enabled = true
            if (i < towers.Count - 1)
                yield return new WaitForSeconds(interval);
        }
    }
}
