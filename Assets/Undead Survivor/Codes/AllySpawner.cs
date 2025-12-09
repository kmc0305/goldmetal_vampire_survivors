using System.Collections;
using System.Collections.Generic;
using UnityEditor.EditorTools; // (참고) 이 using은 현재 코드에서 사용되지 않으므로 제거해도 됩니다.
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput; // (참고) 이 using도 현재 코드에서 사용되지 않으므로 제거해도 됩니다.

/// <summary>
/// [아군 스포너]
/// 'mytower' (성)에 부착되어, 주기적으로 아군 유닛을 스폰(Spawn)합니다.
/// PoolManager에 의존합니다.
/// </summary>
public class AllySpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    /// <summary>
    /// PoolManager의 'prefabs' 배열에서 가져올 아군 유닛의 인덱스(번호).
    /// (예: 0=적A, 1=적B, 2=아군A 라면, 이 값은 2가 되어야 함)
    /// </summary>
    public int allyPrefabIndex = 2;

    /// <summary>아군 유닛 스폰 주기 (초)</summary>
    public float spawnInterval = 5f;

    [Header("스폰 위치")]
    /// <summary>이 오브젝트(성) 중심으로부터의 최대 스폰 반경</summary>
    public float spawnRadius = 2f;

    /// <summary>스폰 주기를 계산하기 위한 내부 타이머</summary>
    private float timer;

    /// <summary>PoolManager 참조</summary>
    private PoolManager poolManager;

    // ★★★ 수정: Awake() 대신 Start()를 사용 ★★★
    /// <summary>
    /// [Unity 이벤트] Start() - 모든 Awake()가 실행된 후 호출
    /// </summary>
    void Start()
    {
        // (중요) GameManager.instance는 Awake()에서 설정됩니다.
        // 혹시 모를 실행 순서 오류를 방지하기 위해,
        // instance를 참조하는 코드는 Start()에서 실행하는 것이 더 안전합니다.

        // 1. 전역(Global) GameManager에서 PoolManager 참조를 가져옵니다.
        poolManager = GameManager.instance.Pool;

        // 2. [오류 방지] 만약 PoolManager를 가져오지 못했다면 (GameManager에 연결이 안 되어 있다면)
        if (poolManager == null)
        {
            // 개발자가 문제를 인지할 수 있도록 콘솔(Console)에 에러 로그를 남깁니다.
            Debug.LogError("GameManager에서 PoolManager를 찾을 수 없습니다! GameManager 인스펙터 창에서 'Pool' 변수가 비어있는지 확인하세요.");
        }
    }

    /// <summary>
    /// [Unity 이벤트] Update() - 매 프레임마다 호출
    /// </summary>
    void Update()
    {
        // Start()에서 poolManager를 찾지 못했다면 Update 로직을 실행하지 않습니다. (에러 방지)
        if (poolManager == null) return;

        // ★ 추가됨 : 게임 시작 전이면 아군 스폰 중지
        if (!GameManager.instance.isGameLive) return;

        // 1. 'Time.deltaTime' (이전 프레임부터 현재까지 걸린 시간)을 타이머에 계속 더합니다.
        timer += Time.deltaTime;

        // 2. 타이머가 스폰 주기(spawnInterval)를 넘어서면
        if (timer > spawnInterval)
        {
            SpawnAlly();   // 3. 아군을 스폰합니다.
            timer = 0;     // 4. 타이머를 0으로 초기화합니다.
        }
    }

    /// <summary>
    /// PoolManager에서 아군 유닛을 가져와 스폰합니다.
    /// (Update 함수에서 주기적으로 호출됩니다.)
    /// </summary>
    void SpawnAlly()
    {
        if (poolManager == null)
        {
            Debug.LogError("PoolManager가 null이라서 스폰할 수 없습니다!");
            return;
        }

        // 1. PoolManager에게 'allyPrefabIndex'번의 오브젝트를 달라고 요청(Get)합니다.
        GameObject ally = poolManager.Get(allyPrefabIndex);

        // 2. 성(이 스크립트가 붙은 오브젝트) 주변의 랜덤한 위치를 계산합니다.
        //    (Random.insideUnitCircle = (x, y) 좌표가 -1~1 사이인 원 안의 랜덤한 2D 벡터)
        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;

        // 3. 아군 유닛의 위치를 '성 위치 + 랜덤 위치'로 설정합니다.
        ally.transform.position = transform.position + (Vector3)randomOffset;
    }
}
