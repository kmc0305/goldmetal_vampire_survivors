using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("Boss Spawn Settings")]
    public GameObject bossPrefab;
    public bool spawnBossOnlyOnce = true;
    public float bossScaleMultiplier = 2f;

    [Header("Spawn Settings")]
    [Tooltip("PoolManager에서 가져올 적군 유닛의 고정 ID 또는 인덱스")]
    public int poolId = 0;

    [Tooltip("적 생성 주기 (초 단위)")]
    public float spawnInterval = 3.0f;

    // ★ [추가됨] 커스텀 스폰 위치 지정용 변수
    [Tooltip("여기에 빈 오브젝트를 넣으면 적들이 그 위치에서 생성됩니다. (비워두면 기본 위치 사용)")]
    public Transform customSpawnPoint;

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

    // 공통 스폰 위치 오프셋 (커스텀 포인트 없을 때 사용)
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
            dataToUse = Spawner.Instance.CurrentSpawnData;
        }

        if (GameManager.instance == null) return;

        GameObject enemyObj = GameManager.instance.Pool.Get(currentPoolId);

        // ★ [수정됨] 커스텀 포인트가 있으면 거기서, 없으면 원래대로
        Vector3 finalPos;
        if (customSpawnPoint != null)
        {
            finalPos = customSpawnPoint.position;
        }
        else
        {
            finalPos = transform.position + spawnOffset;
        }

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

            // ★ [수정됨] 보스도 커스텀 포인트 위치 적용
            Vector3 spawnPos;
            if (customSpawnPoint != null)
            {
                spawnPos = customSpawnPoint.position;
            }
            else
            {
                spawnPos = transform.position + spawnOffset;
            }

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

    // ★ [추가됨] 에디터에서 어디서 스폰되는지 눈으로 보기 쉽게 선 그려줌
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        if (customSpawnPoint != null)
        {
            // 커스텀 포인트가 있으면 그 위치에 원을 그림
            Gizmos.DrawWireSphere(customSpawnPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, customSpawnPoint.position); // 본체랑 연결선
        }
        else
        {
            // 없으면 기본 오프셋 위치에 원을 그림
            Gizmos.DrawWireSphere(transform.position + spawnOffset, 0.5f);
        }
    }
}