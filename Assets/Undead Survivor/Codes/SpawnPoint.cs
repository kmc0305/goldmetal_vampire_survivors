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

    [Header("Spawn Loop (지점별 주기)")]
    public int poolId = 0;
    public bool useSpawnerSpawnTime = true;
    public float fixedInterval = 2f;

    [Header("Health (파괴되면 영구 중단)")]
    public int maxHP = 20;

    [Header("Visuals (상태별 에셋)")]
    public SpriteRenderer spriteRenderer;
    public Sprite idle_Sprite;
    public Sprite active_Sprite;
    public Sprite Damaged_Sprite;

    [Header("HP Bar (머리 위 표시)")]
    public Transform hpBarRoot;
    public Transform hpFill;
    public float barWidth = 1.2f;
    public float barHeight = 0.18f;
    public Vector3 barOffset = new Vector3(0f, 0.9f, 0f);

    public bool IsEnabled { get; private set; } = false;
    public bool PermanentlyOff { get; private set; } = false;
    public bool EverActivated { get; private set; } = false;

    int hp;
    Coroutine loop;
    bool bossSpawned = false;

    void Awake()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        hp = maxHP;
        if (hpBarRoot)
        {
            hpBarRoot.localPosition = barOffset;
            if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);
        }
    }

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
        if (loop == null) loop = StartCoroutine(SpawnLoop());
        UpdateVisual();
        UpdateHPBar();
        StartCoroutine(DamageTestLoop()); // 테스트용
        bossSpawned = false;
    }

    void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
        loop = null;
    }

    public bool ActivateOnce()
    {
        if (PermanentlyOff || EverActivated) return false;
        EverActivated = true;
        IsEnabled = true;
        UpdateVisual();
        UpdateHPBar();
        return true;
    }

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

    public void TakeDamage(int dmg = 1)
    {
        if (PermanentlyOff) return;
        hp = Mathf.Max(0, hp - dmg);
        if (hp <= 0) DeactivatePermanently();
        UpdateHPBar();
    }

    public void DeactivatePermanently()
    {
        PermanentlyOff = true;
        IsEnabled = false;
        UpdateVisual();
        UpdateHPBar();

        Debug.Log($"[SpawnPoint] Deactivated. hp={hp}, bossPrefab={(bossPrefab ? bossPrefab.name : "null")}");

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

            Debug.Log($"[SpawnPoint] Boss spawned at {spawnPos}, active={boss.activeSelf}");
        }
        else
        {
            if (!bossPrefab) Debug.LogWarning("[SpawnPoint] bossPrefab is NULL");
            else Debug.Log("[SpawnPoint] Boss already spawned (spawnBossOnlyOnce=true)");
        }
    }

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
        hpBarRoot.gameObject.SetActive(!PermanentlyOff);

        float ratio = (maxHP > 0) ? Mathf.Clamp01((float)hp / maxHP) : 0f;
        float targetWidth = barWidth * ratio;
        hpFill.localScale = new Vector3(targetWidth, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - targetWidth) * 0.5f, 0f, 0f);

        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (!IsEnabled || PermanentlyOff) { yield return null; continue; }

            GameObject enemy = GameManager.instance.Pool.Get(poolId);
            enemy.transform.position = transform.position;

            var sp = Spawner.Instance;
            if (sp != null && sp.CurrentSpawnData != null)
                enemy.GetComponent<Enemy>().init(sp.CurrentSpawnData);

            float interval = (useSpawnerSpawnTime && sp != null && sp.CurrentSpawnData != null)
                                ? sp.CurrentSpawnData.spawnTime
                                : fixedInterval;

            yield return (interval > 0f) ? new WaitForSeconds(interval) : null;
        }
    }
}
