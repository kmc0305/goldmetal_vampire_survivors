using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("Boss Spawn Settings")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;
    public bool spawnBossOnlyOnce = true;
    public float bossScaleMultiplier = 2f;
    public BossSpec bossSpec;

    [Header("Spawn Settings")]
    public int poolId = 0;
    public int rangedEnemyId = 1;
    [Tooltip("적 생성 주기 (초 단위)")]
    public float spawnInterval = 3.0f;

    [Header("Visuals (애니메이션)")]
    public Animator animator;
    [Tooltip("체력이 이 비율 이하로 떨어지면 불타는 애니메이션 재생")]
    public float burningThreshold = 0.4f;

    [Header("HP Bar")]
    public Transform hpBarRoot;
    public Transform hpFill;
    public float barWidth = 1.2f;
    public float barHeight = 0.18f;
    public Vector3 barOffset = new Vector3(0f, 0.9f, 0f);

    public bool PermanentlyOff { get; private set; } = false;
    public bool IsEnabled { get; private set; } = false;
    public bool EverActivated { get; private set; } = false;

    private bool bossSpawned = false;
    private int spawnCount = 0;
    private Coroutine spawnCoroutine;

    // [추가됨] 컴포넌트 제어를 위한 변수
    private Targetable myTargetable;
    private Collider2D myCollider;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();

        myTargetable = GetComponent<Targetable>();
        // [추가됨] 콜라이더 가져오기
        myCollider = GetComponent<Collider2D>();

        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
        if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);

        // 초기 상태: 일단 꺼둠 (Spawner가 ResetRuntimeFlags를 호출하며 확실히 정리함)
        SetTargetableState(false);

        UpdateHPBar();
    }

    void Update()
    {
        if (PermanentlyOff) return;
        if (IsEnabled && !PermanentlyOff)
        {
            UpdateHPBar();
        }
    }

    public bool ActivateOnce()
    {
        if (PermanentlyOff) return false;
        if (IsEnabled && spawnCoroutine != null) return true;

        EverActivated = true;
        IsEnabled = true;

        // [추가됨] 활성화되면 타겟팅 가능하게 변경
        SetTargetableState(true);

        if (animator)
        {
            animator.ResetTrigger("DoReset");
            animator.SetTrigger("DoBuild");
        }

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnRoutine());

        return true;
    }

    IEnumerator SpawnRoutine()
    {
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

        int currentPoolId = poolId;
        SpawnData dataToUse = null;

        if (Spawner.Instance != null && Spawner.Instance.CurrentSpawnData != null)
        {
            currentPoolId = Spawner.Instance.CurrentSpawnData.spriteType;
            dataToUse = Spawner.Instance.CurrentSpawnData;
        }

        if (spawnCount % 2 != 0) currentPoolId = rangedEnemyId;

        if (GameManager.instance == null) return;

        GameObject enemyObj = GameManager.instance.Pool.Get(currentPoolId);
        enemyObj.transform.position = transform.position + new Vector3(0.5f, -1.4f, 0f);
        enemyObj.transform.rotation = Quaternion.identity;

        Enemy enemyScript = enemyObj.GetComponent<Enemy>();

        if (enemyScript != null)
        {
            if (dataToUse != null)
            {
                enemyScript.init(dataToUse);
            }
            else
            {
                SpawnData defaultData = new SpawnData();
                defaultData.speed = enemyScript.speed;
                var t = enemyScript.GetComponent<Targetable>();
                defaultData.health = (int)(t ? t.maxHealth : 10f);
                defaultData.spriteType = currentPoolId;
                enemyScript.init(defaultData);
            }
        }

        spawnCount++;
    }

    public void DeactivatePermanently()
    {
        if (PermanentlyOff) return;
        if (spawnCoroutine != null) { StopCoroutine(spawnCoroutine); spawnCoroutine = null; }

        PermanentlyOff = true;
        IsEnabled = false;

        // [추가됨] 파괴되면 타겟팅 불가능하게 변경
        SetTargetableState(false);

        if (animator) animator.SetTrigger("DoDestroy");
        UpdateHPBar();

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

        // [추가됨] 리셋 시 타겟팅 꺼둠 (Spawner가 게임 시작시 호출함)
        SetTargetableState(false);

        if (animator) animator.SetTrigger("DoReset");
        UpdateHPBar();
    }

    // [추가됨] 타겟 가능 여부(Collider 및 컴포넌트)를 끄고 켜는 헬퍼 함수
    private void SetTargetableState(bool state)
    {
        // 1. Targetable 컴포넌트를 끄면 로직적으로 비활성화됨
        if (myTargetable != null)
            myTargetable.enabled = state;

        // 2. Collider2D를 끄면 물리 감지(AllyAI의 탐색)에 걸리지 않음
        if (myCollider != null)
            myCollider.enabled = state;
    }

    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;

        // [수정됨] PermanentlyOff 일때만 끄는게 아니라, 활성화(IsEnabled)가 안됐을 때도 바를 숨김
        hpBarRoot.gameObject.SetActive(!PermanentlyOff && IsEnabled);

        if (myTargetable == null) return;

        float cur = myTargetable.currentHealth;
        float max = Mathf.Max(0.0001f, myTargetable.maxHealth);
        float ratio = Mathf.Clamp01(cur / max);

        float targetWidth = barWidth * ratio;
        hpFill.localScale = new Vector3(targetWidth, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - targetWidth) * 0.5f, 0f, 0f);

        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);

        if (animator && !PermanentlyOff && IsEnabled)
        {
            bool isBurning = ratio <= burningThreshold;
            animator.SetBool("IsBurning", isBurning);
        }
    }
}