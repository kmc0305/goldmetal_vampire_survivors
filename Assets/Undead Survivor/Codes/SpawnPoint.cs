using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("Spawn Loop (지점별 주기)")]
    public int poolId = 0;
    public bool useSpawnerSpawnTime = true;
    public float fixedInterval = 2f;

    [Header("Health (파괴되면 영구 중단)")]
    public int maxHP = 20;

    [Header("Visuals (상태별 에셋)")]
    public SpriteRenderer spriteRenderer; // 연결할 SpriteRenderer
    public Sprite idle_Sprite;            // 비활성 시 스프라이트
    public Sprite active_Sprite;          // 활성 시 스프라이트
    public Sprite Damaged_Sprite;         // 완전 파괴 시 스프라이트(선택)

    [Header("HP Bar (머리 위 표시)")]
    public Transform hpBarRoot;   // HP 바 부모 (빈 오브젝트)
    public Transform hpFill;      // 채워지는 부분(스프라이트가 붙은 Transform)
    public float barWidth = 1.2f; // 바 전체 가로 길이(유닛)
    public float barHeight = 0.18f;// 바 높이(유닛)
    public Vector3 barOffset = new Vector3(0f, 0.9f, 0f); // 머리 위 위치

    [Header("Boss Spawn Settings")]
    public GameObject bossPrefab;      // Inspector에 드래그해서 연결
    public Transform bossSpawnPoint;   // (선택) 보스를 소환할 위치, 없으면 자기 자리 사용
    public bool spawnBossOnlyOnce = true; // ✅ 중복 소환 방지 옵션

    public bool IsEnabled { get; private set; } = false;
    public bool PermanentlyOff { get; private set; } = false;
    public bool EverActivated { get; private set; } = false;

    int hp;
    Coroutine loop;
    bool bossSpawned = false; // ✅ 실제로 보스를 한 번 소환했는지

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // 최대 체력을 기준으로 현재 체력 초기화
        hp = maxHP;

        // HP 바 초기 위치/크기 세팅
        if (hpBarRoot != null)
        {
            hpBarRoot.localPosition = barOffset;
            if (hpFill != null)
                hpFill.localScale = new Vector3(barWidth, barHeight, 1f);
        }
    }

    IEnumerator DamageTestLoop() // ---------------- 데미지 테스트용
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // 0.5초마다 데미지 1
            if (IsEnabled && !PermanentlyOff)
            {
                TakeDamage(1);
            }
        }
    }

    void OnEnable()
    {
        if (loop == null) loop = StartCoroutine(SpawnLoop());
        UpdateVisual();
        UpdateHPBar(); // 시작 시 한 번 반영
        StartCoroutine(DamageTestLoop()); // --------- 데미지 테스트
        bossSpawned = false; // ✅ 다시 켜지면 보스 스폰 플래그 초기화
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
        hp = maxHP; // 웨이브 초기화 시 체력도 최대치로
        IsEnabled = false;
        PermanentlyOff = false;
        EverActivated = false;
        bossSpawned = false; // ✅ 웨이브 리셋 시 보스 다시 소환 가능
        UpdateVisual();
        UpdateHPBar();
    }

    public void TakeDamage(int dmg = 1)
    {
        if (PermanentlyOff) return;

        hp -= dmg;
        if (hp < 0) hp = 0;

        if (hp <= 0)
            DeactivatePermanently();

        UpdateHPBar(); // 체력 변동 즉시 반영
    }

    public void DeactivatePermanently()
    {
        PermanentlyOff = true;
        IsEnabled = false;
        UpdateVisual();
        UpdateHPBar(); // 숨김 처리 포함

        // ✅ 흐름/상태 확인용 로그
        Debug.Log($"[SpawnPoint] Deactivated. hp={hp}, bossPrefab={(bossPrefab ? bossPrefab.name : "null")}");

        // ✅ 보스 소환 (중복 방지 + 널 체크)
        if (bossPrefab != null && (!spawnBossOnlyOnce || !bossSpawned))
        {
            bossSpawned = true;

            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position
                                                       : transform.position + Vector3.up * 1.5f;
            spawnPos.z = 0f; // ✅ 2D 카메라에 보이도록 z=0 고정

            var boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            // ✅ 강제로 크기 2배 설정
            boss.transform.localScale *= 2f;


            // ✅ 프리팹이 비활성 상태로 저장되어 있던 경우 강제 활성화
            if (!boss.activeSelf) boss.SetActive(true);

            Debug.Log($"[SpawnPoint] Boss spawned at {spawnPos}, active={boss.activeSelf}");
        }
        else
        {
            if (bossPrefab == null)
                Debug.LogWarning("[SpawnPoint] bossPrefab is NULL (Inspector에 프리팹 등록 필요)");
            else
                Debug.Log("[SpawnPoint] Boss already spawned (spawnBossOnlyOnce=true)");
        }
    }

    void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        if (PermanentlyOff && Damaged_Sprite != null)
            spriteRenderer.sprite = Damaged_Sprite;
        else if (IsEnabled && active_Sprite != null)
            spriteRenderer.sprite = active_Sprite;
        else if (idle_Sprite != null)
            spriteRenderer.sprite = idle_Sprite;
    }

    /// <summary>
    /// hp / maxHP 비율로 머리 위 HP 바를 갱신
    /// </summary>
    void UpdateHPBar()
    {
        if (hpBarRoot == null || hpFill == null) return;
        // 파괴되면 HP 바 숨김, 아니면 항상 표시
        hpBarRoot.gameObject.SetActive(!PermanentlyOff);

        float ratio = (maxHP > 0) ? Mathf.Clamp01((float)hp / maxHP) : 0f;

        // 왼쪽 기준으로 줄어들게 보이도록: 스케일 + 위치 보정
        float targetWidth = barWidth * ratio;
        hpFill.localScale = new Vector3(targetWidth, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - targetWidth) * 0.5f, 0f, 0f);

        // 색상: 초록(체력 많음) → 빨강(체력 적음)
        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = Color.Lerp(Color.red, Color.green, ratio);
    }

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
            enemy.transform.position = transform.position;

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
}
