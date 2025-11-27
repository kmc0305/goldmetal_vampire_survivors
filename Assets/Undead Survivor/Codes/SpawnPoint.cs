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
    public int poolId = 0;
    public int rangedEnemyId = 1;
    [Tooltip("적 생성 주기 (초 단위)")]
    public float spawnInterval = 3.0f;

    // ------------------------------
    // Visuals & Animation
    // ------------------------------
    [Header("Visuals (애니메이션)")]
    public Animator animator;

    [Tooltip("체력이 이 비율 이하로 떨어지면 불타는 애니메이션 재생 (0.4 = 40%)")]
    public float burningThreshold = 0.4f;

    [Header("HP Bar")]
    public Transform hpBarRoot;
    public Transform hpFill;
    public float barWidth = 1.2f;
    public float barHeight = 0.18f;
    public Vector3 barOffset = new Vector3(0f, 0.9f, 0f);

    // ------------------------------
    // Runtime Flags
    // ------------------------------
    public bool PermanentlyOff { get; private set; } = false;
    public bool IsEnabled { get; private set; } = false; // 현재 활성 상태 (건설 완료 후)
    public bool EverActivated { get; private set; } = false;

    private bool bossSpawned = false;
    private int spawnCount = 0;
    private Coroutine spawnCoroutine;
    private Targetable myTargetable; // HP 확인용 캐싱

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        myTargetable = GetComponent<Targetable>(); // HP 컴포넌트 미리 가져오기

        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
        if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);

        UpdateHPBar();
    }
    // SpawnPoint.cs 안에 이 함수를 추가해줘 (void Start 밑이나 아무데나)

    void Update()
    {
        if (PermanentlyOff) return;
        // 매 순간마다 HP바와 애니메이션 상태를 갱신
        if (IsEnabled && !PermanentlyOff)
        {
            UpdateHPBar();
        }
    }
    // Spawner(매니저)가 10초 간격으로 이 함수를 호출할 거야
    // SpawnPoint.cs 안의 ActivateOnce 함수

    public bool ActivateOnce()
    {
        if (PermanentlyOff) return false;
        if (IsEnabled && spawnCoroutine != null) return true;

        EverActivated = true;
        IsEnabled = true;

        // ★ [수정] 여기가 핵심! ★
        if (animator)
        {
            // "야, 아까 든 리셋 깃발 있으면 얼른 내려!" (초기화)
            animator.ResetTrigger("DoReset");

            // "자, 이제 건설 시작!"
            animator.SetTrigger("DoBuild");
        }

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnRoutine());

        return true;
    }

    IEnumerator SpawnRoutine()
    {
        // 건설되는 동안(약 1~2초?) 적이 바로 나오면 어색하니까 
        // 애니메이션 길이만큼 잠깐 기다려줄 수도 있어. 일단은 즉시 생성.
        SpawnEnemy();
        yield return new WaitForSeconds(spawnInterval);

        while (IsEnabled && !PermanentlyOff)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }
        spawnCoroutine = null;
    }

    void SpawnEnemy()
    {
        if (PermanentlyOff) return;
        // ... (기존 로직 동일) ...
        int currentPoolId = poolId;
        if (Spawner.Instance?.CurrentSpawnData != null) currentPoolId = Spawner.Instance.CurrentSpawnData.spriteType;
        if (spawnCount % 2 != 0) currentPoolId = rangedEnemyId;
        if (GameManager.instance == null) return;

        GameObject enemyObj = GameManager.instance.Pool.Get(currentPoolId);
        enemyObj.transform.position = transform.position + new Vector3(0.5f, -1.4f, 0f);
        enemyObj.transform.rotation = Quaternion.identity;

        Enemy enemyScript = enemyObj.GetComponent<Enemy>();
        if (enemyScript != null && Spawner.Instance?.CurrentSpawnData != null)
            enemyScript.init(Spawner.Instance.CurrentSpawnData);

        spawnCount++;
    }

    public void DeactivatePermanently()
    {
        if (PermanentlyOff) return;

        if (spawnCoroutine != null) { StopCoroutine(spawnCoroutine); spawnCoroutine = null; }

        PermanentlyOff = true;
        IsEnabled = false;

        // ★ 파괴 애니메이션 (Trigger)
        if (animator) animator.SetTrigger("DoDestroy");

        UpdateHPBar();
        Debug.Log($"[SpawnPoint] Deactivated. Boss Logic Starting...");

        // 보스 생성 로직 (기존 동일)
        if (bossPrefab != null && (!spawnBossOnlyOnce || !bossSpawned))
        {
            bossSpawned = true;
            Vector3 spawnPos = bossSpawnPoint ? bossSpawnPoint.position : transform.position + Vector3.up * 1.5f;
            spawnPos.z = 0f;
            var boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            boss.transform.localScale *= bossScaleMultiplier;
            var enemy = boss.GetComponent<Enemy>();
            if (enemy != null && bossSpec != null) enemy.ApplyBossSpec(bossSpec);
            if (!boss.activeSelf) boss.SetActive(true);
        }
    }

    public void ResetRuntimeFlags()
    {
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = null;

        PermanentlyOff = false;
        EverActivated = false;
        IsEnabled = false;
        bossSpawned = false;
        spawnCount = 0;

        // ★ 초기화 (Trigger)
        if (animator) animator.SetTrigger("DoReset");

        UpdateHPBar();
    }

    // 여기가 핵심! HP바 갱신하면서 불타는 상태 체크
    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;

        // 파괴되었으면 HP바 숨김
        hpBarRoot.gameObject.SetActive(!PermanentlyOff && IsEnabled);

        if (myTargetable == null) return;

        float cur = myTargetable.currentHealth;
        float max = Mathf.Max(0.0001f, myTargetable.maxHealth);
        float ratio = Mathf.Clamp01(cur / max);

        // HP바 길이 조절
        float targetWidth = barWidth * ratio;
        hpFill.localScale = new Vector3(targetWidth, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - targetWidth) * 0.5f, 0f, 0f);

        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);

        // ★ 불타는 애니메이션 상태 제어 (파괴되지 않았을 때만)
        if (animator && !PermanentlyOff && IsEnabled)
        {
            bool isBurning = ratio <= burningThreshold;
            animator.SetBool("IsBurning", isBurning);
        }
    }
}