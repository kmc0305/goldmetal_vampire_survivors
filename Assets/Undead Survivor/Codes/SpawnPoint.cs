using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("Boss Spawn Settings")]
    public GameObject bossPrefab;
    public bool spawnBossOnlyOnce = true;
    public float bossScaleMultiplier = 2f;

    [Header("Spawn Settings")]
    public int poolId = 0;
    public float spawnInterval = 3.0f;
    public Transform customSpawnPoint;

    [Header("Visuals (애니메이션)")]
    public Animator animator;
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

    private readonly Vector3 spawnOffset = new Vector3(0.5f, -1.4f, 0f);

    private Targetable myTargetable;
    private Collider2D myCollider;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();

        myTargetable = GetComponent<Targetable>();
        myCollider = GetComponent<Collider2D>();

        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
        if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);

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
        // ★ 추가됨 : 게임 시작 전엔 스폰 금지
        while (!GameManager.instance.isGameLive)
            yield return null;

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
        // ★ 추가됨 : 게임 시작 전에는 리턴
        if (!GameManager.instance.isGameLive) return;

        if (PermanentlyOff) return;

        int currentPoolId = poolId;
        SpawnData dataToUse = null;

        if (Spawner.Instance != null && Spawner.Instance.CurrentSpawnData != null)
        {
            dataToUse = Spawner.Instance.CurrentSpawnData;
        }

        GameObject enemyObj = GameManager.instance.Pool.Get(currentPoolId);

        Vector3 finalPos = customSpawnPoint != null ?
            customSpawnPoint.position :
            transform.position + spawnOffset;

        enemyObj.transform.position = finalPos;
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

        if (myTargetable != null)
            myTargetable.enabled = false;

        if (animator) animator.SetTrigger("DoDestroy");
        UpdateHPBar();

        if (bossPrefab != null && (!spawnBossOnlyOnce || !bossSpawned))
        {
            bossSpawned = true;

            Vector3 spawnPos = customSpawnPoint != null ?
                customSpawnPoint.position :
                transform.position + spawnOffset;

            spawnPos.z = 0f;

            var boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            boss.transform.localScale *= bossScaleMultiplier;

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

        SetTargetableState(false);

        if (animator) animator.SetTrigger("DoReset");
        UpdateHPBar();
    }

    private void SetTargetableState(bool state)
    {
        if (myTargetable != null)
            myTargetable.enabled = state;

        if (myCollider != null)
            myCollider.enabled = state;
    }

    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;

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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        if (customSpawnPoint != null)
        {
            Gizmos.DrawWireSphere(customSpawnPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, customSpawnPoint.position);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position + spawnOffset, 0.5f);
        }
    }
}
