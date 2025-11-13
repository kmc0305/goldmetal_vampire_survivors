using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    // ------------------------------
    // Boss Spawn Settings
    // ------------------------------
    [Header("Boss Spawn Settings")]
    public GameObject bossPrefab;              // 타워 파괴 시 소환할 보스 프리팹
    public Transform bossSpawnPoint;           // 보스 소환 위치(없으면 타워 위 약간 위)
    public bool spawnBossOnlyOnce = true;      // true면 한 번만 보스 소환
    public float bossScaleMultiplier = 2f;     // 보스 스케일 배수
    public BossSpec bossSpec;                  // 보스 능력치(체력/공격 등)

    // ------------------------------
    // Spawn Loop (지점별 주기)
    // ------------------------------
    [Header("Spawn Loop (지점별 주기)")]
    public int poolId = 0;                     // PoolManager에서 꺼낼 적 인덱스
    public bool useSpawnerSpawnTime = true;    // Spawner의 spawnData 간격을 따를지
    public float fixedInterval = 2f;           // 개별 지점 고정 간격(위가 false일 때 사용)

    // ------------------------------
    // Visuals
    // ------------------------------
    [Header("Visuals (상태별 에셋)")]
    public SpriteRenderer spriteRenderer;      // 연결할 SpriteRenderer
    public Sprite idle_Sprite;                 // 비활성 시 스프라이트
    public Sprite active_Sprite;               // 활성 시 스프라이트
    public Sprite Damaged_Sprite;              // 완전 파괴 시 스프라이트

    // ------------------------------
    // HP Bar (Targetable 기준)
    // ------------------------------
    [Header("HP Bar (Targetable 기준)")]
    public Transform hpBarRoot;                // HP 바 부모
    public Transform hpFill;                   // HP 채우기 (SpriteRenderer 달린 오브젝트)
    public float barWidth = 1.2f;              // 바 전체 가로 길이
    public float barHeight = 0.18f;            // 바 높이
    public Vector3 barOffset = new Vector3(0f, 0.9f, 0f);

    // ------------------------------
    // 런타임 상태 플래그
    // ------------------------------
    public bool IsEnabled { get; private set; } = false;      // 현재 스폰 중인지
    public bool PermanentlyOff { get; private set; } = false;  // 파괴되어 영구 중단 여부
    public bool EverActivated { get; private set; } = false;   // 한 번이라도 켜진 적 있는지

    // 내부
    Coroutine loop;
    bool bossSpawned = false;

    void Awake()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        // HP바 초기 위치/스케일
        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
        if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);

        UpdateVisual();
        UpdateHPBar(); // Targetable 값을 읽어 표시
    }

    void OnEnable()
    {
        if (loop == null) loop = StartCoroutine(SpawnLoop());
        UpdateVisual();
        UpdateHPBar();
        bossSpawned = false;
    }

    void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

    // -------------------------------------------------
    // 외부에서 켜줄 때(Spawner가 주기적으로 한 개씩 활성화)
    // -------------------------------------------------
    public bool ActivateOnce()
    {
        if (PermanentlyOff || EverActivated) return false;
        EverActivated = true;
        IsEnabled = true;
        UpdateVisual();
        UpdateHPBar();
        return true;
    }

    // 씬 리셋/재시작 시 호출(Spawner.Awake에서 호출)
    public void ResetRuntimeFlags()
    {
        IsEnabled = false;
        PermanentlyOff = false;
        EverActivated = false;
        bossSpawned = false;
        UpdateVisual();
        UpdateHPBar();
    }

    // -------------------------------------------------
    // 타워 파괴 처리(※ Targetable.onDie 이벤트로 연결해서 호출 권장)
    // -------------------------------------------------
    public void DeactivatePermanently()
    {
        if (PermanentlyOff) return;

        PermanentlyOff = true;
        IsEnabled = false;
        UpdateVisual();
        UpdateHPBar();

        Debug.Log($"[SpawnPoint] Deactivated. bossPrefab={(bossPrefab ? bossPrefab.name : "null")}");

        // 보스 소환
        if (bossPrefab != null && (!spawnBossOnlyOnce || !bossSpawned))
        {
            bossSpawned = true;

            Vector3 spawnPos = bossSpawnPoint ? bossSpawnPoint.position
                                              : transform.position + Vector3.up * 1.5f;
            spawnPos.z = 0f;
         
            var boss = Object.Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            boss.transform.localScale *= bossScaleMultiplier;

            var enemy = boss.GetComponent<Enemy>();
            if (enemy != null && bossSpec != null)
                enemy.ApplyBossSpec(bossSpec);

            if (!boss.activeSelf) boss.SetActive(true);

            Debug.Log($"[SpawnPoint] Boss spawned at {spawnPos}, active={boss.activeSelf}");
        }
        else
        {
            if (!bossPrefab) Debug.LogWarning("[SpawnPoint] bossPrefab is NULL");
            else Debug.Log("[SpawnPoint] Boss already spawned (spawnBossOnlyOnce=true)");
        }
    }

    // -------------------------------------------------
    // 비주얼/HP바 업데이트 (체력은 Targetable을 단일 출처로)
    // -------------------------------------------------
    void UpdateVisual()
    {
        if (!spriteRenderer) return;

        if (PermanentlyOff && Damaged_Sprite) spriteRenderer.sprite = Damaged_Sprite;
        else if (IsEnabled && active_Sprite) spriteRenderer.sprite = active_Sprite;
        else if (idle_Sprite) spriteRenderer.sprite = idle_Sprite;
    }

    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;

        // 파괴되면 HP바 숨김
        hpBarRoot.gameObject.SetActive(!PermanentlyOff);

        var tar = GetComponent<Targetable>();
        if (tar == null)
            return;

        float cur = tar.currentHealth;
        float max = Mathf.Max(0.0001f, tar.maxHealth); // 0 나눗셈 방지
        float ratio = Mathf.Clamp01(cur / max);

        float targetWidth = barWidth * ratio;
        hpFill.localScale = new Vector3(targetWidth, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - targetWidth) * 0.5f, 0f, 0f);

        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);
    }

    // -------------------------------------------------
    // 스폰 루프
    // -------------------------------------------------
    IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (!IsEnabled || PermanentlyOff)
            {
                yield return null;
                continue;
            }

            GameObject enemy = GameManager.instance.Pool.Get(poolId);

            // 🔥 여기서 y축 -5.4 지점에 스폰되도록 오프셋 추가
            enemy.transform.position = transform.position + new Vector3(0f, -5.4f, 0f);

            var sp = Spawner.Instance;
            if (sp != null && sp.CurrentSpawnData != null)
                enemy.GetComponent<Enemy>().init(sp.CurrentSpawnData);

            float interval = useSpawnerSpawnTime && sp != null && sp.CurrentSpawnData != null
                ? sp.CurrentSpawnData.spawnTime
                : fixedInterval;

            if (interval > 0f) yield return new WaitForSeconds(interval);
            else yield return null;
        }
    }


    // 에디터에서 참조 자동 세팅 보조
    void OnValidate()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
    }
}
