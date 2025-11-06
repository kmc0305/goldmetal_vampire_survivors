using UnityEngine;

[RequireComponent(typeof(Targetable))]
public class EnemyTowerController : MonoBehaviour
{
    [Header("Spawner (자식)")]
    public Spawner spawner;                 // enemyTower 하위 Spawner drag

    [Header("비주얼")]
    public SpriteRenderer rendererRef;      // 성 SpriteRenderer
    public Sprite idleSprite;               // 비활성 시
    public Sprite activeSprite;             // 활성 시
    public GameObject ruinedPrefab;         // 파괴 후 남길 폐허(선택)

    [Header("보스")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;
    public float bossScaleMultiplier = 2f;

    Targetable targetable;
    bool destroyed = false;

    void Awake()
    {
        targetable = GetComponent<Targetable>();
        if (!rendererRef) rendererRef = GetComponentInChildren<SpriteRenderer>(true);
        if (!spawner) spawner = GetComponentInChildren<Spawner>(true);

        targetable.onDie.AddListener(OnDie);
    }

    void Start()
    {
        // 시작 상태는 'idle 스프라이트 = 스폰 꺼짐'으로 두고,
        // 왼쪽 성만 외부(Sequence)에서 Activate 호출해 active로 바뀌게 함.
        SetIdle();
    }

    // ===== 외부에서 호출 =====
    public void Activate() => SetActive();
    public void Deactivate() => SetIdle();

    void SetActive()
    {
        if (destroyed) return;
        if (rendererRef && activeSprite) rendererRef.sprite = activeSprite;
        if (spawner) spawner.enabled = true;        // ← 스프라이트 활성화 == 스폰 시작
    }

    void SetIdle()
    {
        if (destroyed) return;
        if (rendererRef && idleSprite) rendererRef.sprite = idleSprite;
        if (spawner) spawner.enabled = false;       // ← 스프라이트 비활성 == 스폰 정지
    }

    void OnDie()
    {
        if (destroyed) return;
        destroyed = true;

        // 스폰 정지
        if (spawner) spawner.enabled = false;

        // 폐허 연출
        if (ruinedPrefab) Instantiate(ruinedPrefab, transform.position, Quaternion.identity);

        // 보스 소환
        if (bossPrefab)
        {
            Vector3 pos = bossSpawnPoint ? bossSpawnPoint.position : transform.position;
            var boss = Instantiate(bossPrefab, pos, Quaternion.identity);
            boss.transform.localScale *= bossScaleMultiplier;
        }

        // 본인 시각은 마지막으로 destroyedSprite 쓰고 싶다면 ruinedPrefab 대신
        // rendererRef.sprite = destroyedSprite; 식으로 바꾸면 됨(원하는 방식 선택).
    }
}
