using UnityEngine;

public class GameStartGate : MonoBehaviour
{
    [Tooltip("시작 화면 UI 루트(Canvas 또는 Panel)")]
    public GameObject startUIRoot;

    private bool started = false;

    void Awake()
    {
        // 씬 로드 즉시 게임 전체 정지
        Time.timeScale = 0f;
    }

    public void StartGame()
    {
        if (started) return;
        started = true;

        // 시작 UI 숨김
        if (startUIRoot != null)
            startUIRoot.SetActive(false);

        // 게임 시작
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        // 예외 상황 대비 (에디터 정지, 씬 전환 등)
        Time.timeScale = 1f;
    }
}
