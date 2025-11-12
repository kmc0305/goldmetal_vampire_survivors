using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("Boss Spawn Settings")]
    public GameObject bossPrefab;           // 보스 프리팹
    public Transform bossSpawnPoint;        // 보스 스폰 위치 (없으면 자기 자신 기준)
    public bool spawnBossOnlyOnce = true;   // 보스 1회만 스폰할지 여부
    public float bossScaleMultiplier = 2f;  // 보스 크기 배율
    public BossSpec bossSpec;               // 보스 스탯 설정용 ScriptableObject

    [Header("Spawn Loop (지점별 주기)")]
    public int poolId = 0;                  // 오브젝트 풀 ID
    public bool useSpawnerSpawnTime = true; // Spawner의 spawnTime을 사용할지 여부
    public float fixedInterval = 2f;        // 고정 스폰 간격 (useSpawnerSpawnTime=false일 때 사용)

    [Header("Health (파괴되면 영구 중단)")]
    public int maxHP = 20;                  // 스폰포인트의 체력

    [Header("Visuals (상태별 에셋)")]
    public SpriteRenderer spriteRenderer;   // 현재 상태 표시용 스프라이트 렌더러
    public Sprite idle_Sprite;              // 비활성 상태 이미지
    public Sprite active_Sprite;            // 활성 상태 이미지
    public Sprite Damaged_Sprite;           // 파괴 상태 이미지

    [Header("HP Bar (머리 위 표시)")]
    public Transform hpBarRoot;             // HP바 루트 오브젝트
    public Transform hpFill;                // HP바 채워지는 부분
    public float barWidth = 1.2f;
    public float barHeight = 0.18f;
    public Vector3 barOffset = new Vector3(0f, 0.9f, 0f);

    public bool IsEnabled { get; private set; } = false;   // 현재 활성화 상태
    public bool PermanentlyOff { get; private set; } = false; // 영구 비활성 상태 (파괴 시 true)
    public bool EverActivated { get; private set; } = false;  // 한 번이라도 활성화된 적 있는지

    int hp;                        // 현재 HP
    Coroutine loop;                // 적 스폰 루프 코루틴
    bool bossSpawned = false;      // 보스가 이미 스폰되었는지 여부

    void Awake()
    {
        // SpriteRenderer 자동 참조
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        // HP 초기화
        hp = maxHP;

        // HP바 위치 및 크기 초기 설정
        if (hpBarRoot)
        {
            hpBarRoot.localPosition = barOffset;
            if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);
        }
    }

    // 테스트용: 일정 시간마다 데미지를 주는 루프
    IEnumerator DamageTestLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            if (IsEnabled && !PermanentlyOff) TakeDamage(1);
        }
    }

    void OnEnable()
    {
        // 스폰 루프 시작
        if (loop == null) loop = StartCoroutine(SpawnLoop());
        UpdateVisual();
        UpdateHPBar();

        // 테스트 데미지 루프 시작
        StartCoroutine(DamageTestLoop());
        bossSpawned = false;
    }

    void OnDisable()
    {
        // 코루틴 정리
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

    // 한 번만 활성화되도록 처리
    public bool ActivateOnce()
    {
        if (PermanentlyOff || EverActivated) return false;
        EverActivated = true;
        IsEnabled = true;
        UpdateVisual();
        UpdateHPBar();
        return true;
    }

    // 런타임 플래그 초기화 (재시작용)
    public void ResetRuntimeFlags()
    {
        hp = maxHP;
        IsEnabled = false;
        PermanentlyOff = false;
        EverActivated = false;
        bossSpawned = false;
        UpdateVisual();
        UpdateHPBar();
    }

    // 데미지 처리
    public void TakeDamage(int dmg = 1)
    {
        if (PermanentlyOff) return;

        hp = Mathf.Max(0, hp - dmg);

        // HP 0 → 파괴
        if (hp <= 0)
            DeactivatePermanently();

        UpdateHPBar();
    }

    // 스폰포인트 완전 파괴 시 호출
    public void DeactivatePermanently()
    {
        PermanentlyOff = true;
        IsEnabled = false;
        UpdateVisual();
        UpdateHPBar();

        Debug.Log($"[SpawnPoint] Deactivated. hp={hp}, bossPrefab={(bossPrefab ? bossPrefab.name : "null")}");

        // ⚔️ 보스 스폰 로직
        if (bossPrefab != null && (!spawnBossOnlyOnce || !bossSpawned))
        {
            bossSpawned = true;

            // 보스 스폰 위치 계산
            Vector3 spawnPos = bossSpawnPoint ? bossSpawnPoint.position
                                              : transform.position + Vector3.up * 1.5f;
            spawnPos.z = 0f;

            // 보스 생성
            var boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            boss.transform.localScale *= bossScaleMultiplier;

            // 보스 스탯 적용
            var enemy = boss.GetComponent<Enemy>();
            if (enemy != null && bossSpec != null)
                enemy.ApplyBossSpec(bossSpec);

            // 비활성 상태라면 활성화
            if (!boss.activeSelf) boss.SetActive(true);

            Debug.Log($"[SpawnPoint] Boss spawned at {spawnPos}, active={boss.activeSelf}");

            // 🧾 보스 등장 배너 표시 (BossAppearUI 호출)
            var ui = FindObjectOfType<BossAppearUI>();
            if (ui != null)
                ui.ShowBossText(); // Canvas에 연결된 TextMeshProUGUI를 잠시 표시
            else
                Debug.LogWarning("[SpawnPoint] BossAppearUI not found in scene!");
        }
        else
        {
            if (!bossPrefab)
                Debug.LogWarning("[SpawnPoint] bossPrefab is NULL");
            else
                Debug.Log("[SpawnPoint] Boss already spawned (spawnBossOnlyOnce=true)");
        }
    }

    // 상태별 스프라이트 변경
    void UpdateVisual()
    {
        if (!spriteRenderer) return;

        if (PermanentlyOff && Damaged_Sprite)
            spriteRenderer.sprite = Damaged_Sprite;
        else if (IsEnabled && active_Sprite)
            spriteRenderer.sprite = active_Sprite;
        else if (idle_Sprite)
            spriteRenderer.sprite = idle_Sprite;
    }

    // HP바 갱신
    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;
        hpBarRoot.gameObject.SetActive(!PermanentlyOff);

        float ratio = (maxHP > 0) ? Mathf.Clamp01((float)hp / maxHP) : 0f;
        float targetWidth = barWidth * ratio;
        hpFill.localScale = new Vector3(targetWidth, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - targetWidth) * 0.5f, 0f, 0f);

        // HP바 색상 (초록 → 빨강)
        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr)
            sr.color = Color.Lerp(Color.red, Color.green, ratio);
    }

    // 실제 적 스폰 루프
    IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (!IsEnabled || PermanentlyOff)
            {
                yield return null;
                continue;
            }

            // 오브젝트 풀에서 적 가져오기
            GameObject enemy = GameManager.instance.Pool.Get(poolId);
            enemy.transform.position = transform.position;

            // 현재 스폰 데이터 적용
            var sp = Spawner.Instance;
            if (sp != null && sp.CurrentSpawnData != null)
                enemy.GetComponent<Enemy>().init(sp.CurrentSpawnData);

            // 스폰 간격 계산
            float interval = (useSpawnerSpawnTime && sp != null && sp.CurrentSpawnData != null)
                                ? sp.CurrentSpawnData.spawnTime
                                : fixedInterval;

            yield return (interval > 0f) ? new WaitForSeconds(interval) : null;
        }
    }
}
