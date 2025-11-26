using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    // ------------------------------
    // Boss Spawn Settings
    // ------------------------------
    [Header("Boss Spawn Settings")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;
    public bool spawnBossOnlyOnce = true;
    public float bossScaleMultiplier = 2f;
    public BossSpec bossSpec;

    // ------------------------------
    // Spawn Settings
    // ------------------------------
    [Header("Spawn Settings")]
    public int poolId = 0;                     // 기본 적 인덱스 (근거리)
    public int rangedEnemyId = 1;              // 원거리 적 인덱스

    [Tooltip("적 생성 주기 (초 단위)")]
    public float spawnInterval = 3.0f;         // ★ 추가: 스폰 인터벌

    // ------------------------------
    // Visuals & HP Bar
    // ------------------------------
    [Header("Visuals (스프라이트 세팅)")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("활성화된 상태 (멀쩡한 성)")]
    public Sprite active_Sprite;

    [Tooltip("파괴된 상태 (잔해)")]
    public Sprite Damaged_Sprite;

    [Tooltip("아직 활성화 전 대기 상태 (보통 잔해나 빈터)")]
    public Sprite idle_Sprite;

    [Header("HP Bar")]
    public Transform hpBarRoot;
    public Transform hpFill;
    public float barWidth = 1.2f;
    public float barHeight = 0.18f;
    public Vector3 barOffset = new Vector3(0f, 0.9f, 0f);

    // ------------------------------
    // Runtime Flags
    // ------------------------------
    public bool PermanentlyOff { get; private set; } = false; // 파괴 여부
    public bool IsEnabled { get; private set; } = false;      // 활성화 여부 (성 모습 유지용)
    public bool EverActivated { get; private set; } = false;  // Spawner 참조용

    // 내부 변수
    private bool bossSpawned = false;
    private int spawnCount = 0;
    private Coroutine spawnCoroutine; // ★ 추가: 실행 중인 스폰 코루틴 저장용

    void Awake()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
        if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);

        UpdateVisual();
        UpdateHPBar();
    }

    void OnEnable()
    {
        UpdateVisual();
        UpdateHPBar();
    }

    // -------------------------------------------------
    // [핵심] Spawner가 호출하는 함수
    // -------------------------------------------------
    public bool ActivateOnce()
    {
        // 이미 파괴되었으면 무시
        if (PermanentlyOff) return false;

        // 이미 활성화되어 코루틴이 돌고 있다면 중복 실행 방지
        if (IsEnabled && spawnCoroutine != null) return true;

        // 1. 상태 변경 (활성화 됨)
        EverActivated = true;
        IsEnabled = true; // ★ 이제 이 플래그가 true면 계속 성 모습입니다.

        // 2. 비주얼 업데이트 (즉시 성으로 변신)
        UpdateVisual();

        // 3. 적 생성 루틴 시작 (★ 수정: 단발성 호출 -> 코루틴 시작)
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnRoutine());

        return true;
    }

    // ★ 추가: 인터벌을 두고 적을 계속 생성하는 코루틴
    IEnumerator SpawnRoutine()
    {
        // 즉시 첫 번째 적 생성 (원치 않으면 이 줄을 지우고 yield return을 먼저 두면 됨)
        SpawnEnemy();
        yield return new WaitForSeconds(spawnInterval);

        // 파괴되지 않았고, 활성화 상태라면 계속 반복
        while (IsEnabled && !PermanentlyOff)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }

        spawnCoroutine = null;
    }

    void SpawnEnemy()
    {
        // 파괴된 상태에서는 생성 안 함 (안전장치)
        if (PermanentlyOff) return;

        int currentPoolId = poolId;

        if (Spawner.Instance != null && Spawner.Instance.CurrentSpawnData != null)
        {
            currentPoolId = Spawner.Instance.CurrentSpawnData.spriteType;
        }

        // 홀수 번째일 때 원거리 유닛
        if (spawnCount % 2 != 0)
        {
            currentPoolId = rangedEnemyId;
        }

        // GameManager 인스턴스 체크 (에러 방지)
        if (GameManager.instance == null) return;

        GameObject enemyObj = GameManager.instance.Pool.Get(currentPoolId);

        // 위치 설정 (y -5.4 오프셋)
        enemyObj.transform.position = transform.position + new Vector3(0f, -5.4f, 0f);
        enemyObj.transform.rotation = Quaternion.identity;

        Enemy enemyScript = enemyObj.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            if (Spawner.Instance != null && Spawner.Instance.CurrentSpawnData != null)
                enemyScript.init(Spawner.Instance.CurrentSpawnData);
        }

        spawnCount++;
    }

    public void ResetRuntimeFlags()
    {
        // ★ 리셋 시 코루틴 정지
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = null;

        PermanentlyOff = false;
        EverActivated = false;
        IsEnabled = false; // 리셋 시 다시 대기 상태로
        bossSpawned = false;
        spawnCount = 0;

        UpdateVisual();
        UpdateHPBar();
    }

    // -------------------------------------------------
    // 타워 파괴 처리
    // -------------------------------------------------
    public void DeactivatePermanently()
    {
        if (PermanentlyOff) return;

        // ★ 파괴 시 코루틴 정지
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        // 상태 변경
        PermanentlyOff = true;
        IsEnabled = false; // 파괴되었으니 활성 상태 해제

        UpdateVisual(); // 즉시 잔해(Damaged)로 변경
        UpdateHPBar();

        Debug.Log($"[SpawnPoint] Deactivated. Boss Logic Starting...");

        if (bossPrefab != null && (!spawnBossOnlyOnce || !bossSpawned))
        {
            bossSpawned = true;
            Vector3 spawnPos = bossSpawnPoint ? bossSpawnPoint.position
                                              : transform.position + Vector3.up * 1.5f;
            spawnPos.z = 0f;

            var boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            boss.transform.localScale *= bossScaleMultiplier;

            var enemy = boss.GetComponent<Enemy>();
            if (enemy != null && bossSpec != null)
                enemy.ApplyBossSpec(bossSpec);

            if (!boss.activeSelf) boss.SetActive(true);
        }
    }

    // -------------------------------------------------
    // Visual Helper (스프라이트 결정 로직)
    // -------------------------------------------------
    void UpdateVisual()
    {
        if (!spriteRenderer)
        {
            Debug.LogError($"[{gameObject.name}] 오류: SpriteRenderer가 연결되지 않았습니다!");
            return;
        }

        // 우선순위 1: 파괴됨 (Damaged)
        if (PermanentlyOff)
        {
            if (Damaged_Sprite) spriteRenderer.sprite = Damaged_Sprite;
        }
        // 우선순위 2: 활성화됨 (Active - 성 모습 유지)
        else if (IsEnabled)
        {
            if (active_Sprite)
            {
                spriteRenderer.sprite = active_Sprite;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 경고: Active Sprite가 비어있어서 이미지를 못 바꿉니다!");
            }
        }
        // 우선순위 3: 아직 활성화 안 됨 (Idle)
        else
        {
            if (idle_Sprite) spriteRenderer.sprite = idle_Sprite;
            else if (Damaged_Sprite) spriteRenderer.sprite = Damaged_Sprite;
        }
    }

    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;

        hpBarRoot.gameObject.SetActive(!PermanentlyOff);

        var tar = GetComponent<Targetable>();
        if (tar == null) return;

        float cur = tar.currentHealth;
        float max = Mathf.Max(0.0001f, tar.maxHealth);
        float ratio = Mathf.Clamp01(cur / max);

        float targetWidth = barWidth * ratio;
        hpFill.localScale = new Vector3(targetWidth, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - targetWidth) * 0.5f, 0f, 0f);

        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);
    }

    void OnValidate()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
    }
}