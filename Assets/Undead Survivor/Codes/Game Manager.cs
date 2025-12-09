using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public float gameTime;
    public float maxGameTime = 30 * 6 * 10f;

    public int health;
    public int maxHealth = 100;

    [Header("핵심 오브젝트 참조")]
    public Player player;
    public PoolManager Pool;
    private Critical crit_scr;

    public int exp = 0;
    public int[] nextExp = { 0, 12, 24, 36, 48, 60, 72, 84, 96, 108, 120, 132, 144, 156, 168, 180 };
    public int level = 1;
    public int points = 0;

    public int kill = 0;

    // ★ 추가됨 : 게임이 시작되었는지 여부
    public bool isGameLive = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            UnityEngine.Debug.LogWarning("중복된 GameManager 감지. 삭제됨.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        health = maxHealth;
    }

    void Update()
    {
        if (!isGameLive) return; // ★ 추가됨 : 게임 시작 전에는 시간 증가 금지

        gameTime += Time.deltaTime;
        if (gameTime > maxGameTime)
        {
            gameTime = maxGameTime;
        }
    }

    // ★ 추가됨 : 버튼이 호출하는 게임 시작 함수
    public void StartGame()
    {
        isGameLive = true;
        UnityEngine.Debug.Log("게임 시작됨");

        // 플레이어 활성화 (원한다면 Player 내부에도 isGameLive 체크 추가 가능)
        if (player != null)
            player.enabled = true;
    }

    public void getExp()
    {
        if (!isGameLive) return; // ★ 게임 시작 전엔 경험치도 금지
        exp++;

        int idx = Mathf.Min(level, nextExp.Length - 1);

        if (exp >= nextExp[idx])
        {
            exp -= nextExp[idx];
            level++;
            points++;
        }
    }

    public void AddKill()
    {
        if (!isGameLive) return; // ★ 게임 시작 전엔 무효
        kill++;
    }

    public void launchCrit(Vector3 pos)
    {
        if (crit_scr == null || crit_scr.enabled == false)
        {
            crit_scr = GetComponent<Critical>();
            crit_scr.enabled = true;
        }
        crit_scr.onCrit(pos);
    }
}
