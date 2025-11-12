using UnityEngine;
using TMPro;
using System.Collections;

public class BossAppearUI : MonoBehaviour
{
    public TextMeshProUGUI bossText;
    public float showDuration = 2.5f;

    void Start()
    {
        // 시작 시 텍스트는 꺼둠
        if (bossText != null)
            bossText.gameObject.SetActive(false);
    }

    public void ShowBossText()
    {
        // 코루틴 실행은 "이 스크립트가 붙은 오브젝트(Canvas)"에서
        if (bossText != null && gameObject.activeInHierarchy)
            StartCoroutine(ShowTextRoutine());
        else
            Debug.LogWarning("BossAppearUI: bossText not assigned or object inactive.");
    }

    IEnumerator ShowTextRoutine()
    {
        bossText.gameObject.SetActive(true);
        yield return new WaitForSeconds(showDuration);
        bossText.gameObject.SetActive(false);
    }
}
