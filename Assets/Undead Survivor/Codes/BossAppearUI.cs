using UnityEngine;
using TMPro;

public class BossAppearUI : MonoBehaviour
{
    public static BossAppearUI instance;          // 어디서든 접근 가능
    public TextMeshProUGUI bossText;              // 캔버스에 있는 Text(TMP) 참조

    public float showDuration = 2f;               // 표시 시간(초)
    public float fadeDuration = 1f;               // 페이드 아웃 시간(초)

    void Awake()
    {
        instance = this;

        if (bossText != null)
        {
            bossText.gameObject.SetActive(false); // 처음엔 숨김
            bossText.alpha = 0f;                  // 완전 투명
        }
    }

    public void ShowBossText()
    {
        if (bossText != null)
            StartCoroutine(ShowAndHide());
    }

    private System.Collections.IEnumerator ShowAndHide()
    {
        // 텍스트 내용은 에디터에서 입력한 걸 그대로 사용합니다.
        bossText.gameObject.SetActive(true);
        bossText.alpha = 1f;                      // 즉시 보이게

        yield return new WaitForSeconds(showDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            bossText.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        bossText.gameObject.SetActive(false);     // 다 사라지면 비활성화
    }
}
