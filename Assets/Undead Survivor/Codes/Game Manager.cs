using UnityEngine;

/// <summary>
/// [싱글톤] 게임 전체의 핵심 설정을 관리하는 싱글톤(Singleton) 매니저입니다.
/// '싱글톤'이란, 게임 내에 '오직 하나만 존재'하는 것을 보장하는 디자인 패턴입니다.
/// 
/// 이 스크립트를 통해 어떤 스크립트에서든 
/// 'GameManager.instance.player' 또는 'GameManager.instance.Pool'처럼
/// 전역 변수(Global variable)처럼 핵심 객체에 쉽게 접근할 수 있습니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>
    /// [static] 키워드: 이 'instance' 변수는 GameManager 클래스 자체에 귀속됩니다.
    /// 따라서 'new GameManager()'를 하지 않아도 'GameManager.instance'로 접근할 수 있습니다.
    /// 모든 스크립트가 공유하는 단 하나의 'GameManager' 참조입니다.
    /// </summary>
    public static GameManager instance;
    public float gameTime;
    public float maxGameTime = 30 * 6 * 10f;
    [Header("핵심 오브젝트 참조")]
    /// <summary>플레이어(Player) 오브젝트 참조 (인스펙터에서 할당)</summary>
    public Player player;
    /// <summary>오브젝트 풀링(Pooling) 매니저 참조 (인스펙터에서 할당)</summary>
    public PoolManager Pool;


    /// <summary>
    /// [Unity 이벤트] Awake() - 스크립트가 로드될 때 1회 호출 (Start()보다 빠름)
    /// 싱글톤 설정은 Awake()에서 처리하는 것이 일반적입니다.
    /// </summary>
    private void Awake()
    {
        // --- 싱글톤 패턴 구현 ---

        // 1. 'instance'가 아직 비어있다면 (null) (즉, 내가 이 씬의 첫 번째 GameManager라면)
        if (instance == null)
        {
            // 'instance' 변수에 '나 자신(this)'을 할당합니다.
            // 이제부터 다른 스크립트들이 'GameManager.instance'를 통해
            // 이 스크립트의 public 변수(player, Pool)에 접근할 수 있습니다.
            instance = this;
            //자기 자신을 모든곳에서 접근 == player를 모든 곳에서 접근 가능 
        }
        else
        {
            // 2. 만약 'instance'에 이미 다른 GameManager가 할당되어 있다면
            //    (예: 씬을 다시 로드했는데 이전 씬의 GameManager가 안 죽고 넘어온 경우)

            // "나는 가짜다!"
            Debug.LogWarning("중복된 GameManager가 생성되어 하나를 파괴합니다. (Destroy)");
            // 새로 생긴 '나 자신(gameObject)'을 파괴합니다.
            Destroy(gameObject);
        }
    }

    void Update()
    {
        gameTime += Time.deltaTime;
        if (gameTime > maxGameTime)
        {
            gameTime = maxGameTime;
        }

    }
}
