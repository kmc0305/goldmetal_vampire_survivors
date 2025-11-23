using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    /// <summary>스포너가 관리하는 스폰 간격(SpawnData.spawnTime)을 따를지 여부</summary>
    public bool useSpawnerSpawnTime = true;
    /// <summary>개별 스폰 포인트가 가지는 고유 스폰 간격 (useSpawnerSpawnTime이 false일 때 사용)</summary>
    public float spawnInterval = 1f;

    /// <summary>이 스폰 포인트가 영구적으로 비활성화되었는지 여부</summary>
    public bool PermanentlyOff { get; private set; } = false;

    /// <summary>이 스폰 포인트가 한 번이라도 활성화된 적이 있는지 여부 (중복 활성화 방지용)</summary>
    public bool EverActivated { get; private set; } = false;

    private float timer = 0f;

    // [추가] 원거리 유닛의 프리팹 인덱스 (PoolManager에 등록된 순서와 같아야 함)
    // 인스펙터 창에서 알맞은 번호(예: 1)로 설정해주세요.
    public int rangedEnemyId = 1;

    // [추가] 번갈아 생성하기 위한 카운터
    private int spawnCount = 0;

    /// <summary>
    /// [Unity 이벤트] OnEnable() - 오브젝트가 활성화될 때 호출
    /// </summary>
    private void OnEnable()
    {
        // 활성화되자마자 타이머를 0으로 초기화하여 바로 스폰 준비
        timer = 0f;
    }

    /// <summary>
    /// [Unity 이벤트] Update() - 매 프레임 호출
    /// 타이머를 갱신하고 스폰 조건이 되면 적을 생성합니다.
    /// </summary>
    private void Update()
    {
        // 1. 스폰 간격(interval) 결정
        float interval = spawnInterval;

        // 2. Spawner의 데이터를 따르기로 했다면, 현재 레벨의 스폰 간격을 가져옴
        if (useSpawnerSpawnTime && Spawner.Instance != null)
        {
            var data = Spawner.Instance.CurrentSpawnData;
            if (data != null && data.spawnTime > 0f)
                interval = data.spawnTime;
        }

        // 3. 타이머 갱신
        timer += Time.deltaTime;

        // 4. 타이머가 간격보다 커지면 적 생성
        if (timer > interval)
        {
            timer = 0f;
            Spawn();
        }
    }

    /// <summary>
    /// 실제로 적을 생성(풀링)하는 함수
    /// </summary>
    void Spawn()
    {
        int spriteType = 0;

        // 1. Spawner에서 현재 레벨에 맞는 적 데이터 가져오기 (기본값)
        if (Spawner.Instance != null && Spawner.Instance.CurrentSpawnData != null)
        {
            var d = Spawner.Instance.CurrentSpawnData;
            spriteType = d.spriteType;
        }

        // 2. [수정] 번갈아 가며 생성하는 로직
        // spawnCount가 홀수일 때(1, 3, 5...) 원거리 적 ID로 교체
        if (spawnCount % 2 != 0)
        {
            spriteType = rangedEnemyId;
        }

        // 3. 풀 매니저에서 적 오브젝트 가져오기 (변경된 spriteType 사용)
        GameObject enemyObj = GameManager.instance.Pool.Get(spriteType);

        // 4. 위치 설정
        enemyObj.transform.position = transform.position;
        // Quaternion 모호성 해결을 위해 UnityEngine.Quaternion 명시
        enemyObj.transform.rotation = UnityEngine.Quaternion.identity;

        // 5. 적 초기화 (컴포넌트 확인 후 초기화)
        // 근접 적(Enemy)인지, 원거리 적(RangedEnemy)인지 확인
        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy != null)
        {
            // 근접 적이면 기존 데이터로 초기화
            enemy.init(Spawner.Instance.CurrentSpawnData);
        }
        // 원거리 적(RangedEnemy)은 별도의 init 함수가 없다면 생략 가능
        // 만약 있다면 여기서 else if로 처리하면 됩니다.

        // [추가] 다음 순서를 위해 카운트 증가
        spawnCount++;
    }

    /// <summary>
    /// 외부(Spawner.cs)에서 이 스폰 포인트를 '한 번' 활성화할 때 호출하는 함수
    /// </summary>
    /// <returns>성공적으로 켜졌으면 true, 이미 켜져있거나 꺼진 상태면 false</returns>
    public bool ActivateOnce()
    {
        if (PermanentlyOff || gameObject.activeSelf) return false;

        gameObject.SetActive(true);
        EverActivated = true;
        return true;
    }

    /// <summary>
    /// 게임 재시작 등을 위해 런타임 상태(활성화 기록)를 초기화하는 함수
    /// </summary>
    public void ResetRuntimeFlags()
    {
        EverActivated = false;
        // PermanentlyOff는 리셋하지 않음 (영구적이라는 의미 유지)
        // 필요하다면 이 함수에서 gameObject.SetActive(false)를 할 수도 있음
    }
}