using UnityEngine;

public class TowerHPBar : MonoBehaviour
{
    [Header("Target")]
    public Targetable target;          // 성의 Targetable (없으면 GetComponent로 찾음)

    [Header("HP Bar Objects")]
    public Transform hpBarRoot;        // 빈 오브젝트 (HPBarRoot)
    public Transform hpFill;           // SpriteRenderer 달린 바 (HPFill)

    [Header("Style")]
    public float barWidth = 1.6f;      // 가로 길이
    public float barHeight = 0.22f;    // 높이
    public Vector3 barOffset = new Vector3(0f, 1.2f, 0f); // 성 머리 위 위치

    void Awake()
    {
        if (!target) target = GetComponent<Targetable>();
        if (hpBarRoot) hpBarRoot.localPosition = barOffset;
        if (hpFill) hpFill.localScale = new Vector3(barWidth, barHeight, 1f);
        UpdateBar();
    }

    void LateUpdate()
    {
        UpdateBar();
    }

    void UpdateBar()
    {
        if (!target || !hpBarRoot || !hpFill) return;

        // 죽으면 HP바 숨김
        hpBarRoot.gameObject.SetActive(!target.isDead);

        float cur = Mathf.Max(0f, target.currentHealth);
        float max = Mathf.Max(0.0001f, target.maxHealth);
        float ratio = Mathf.Clamp01(cur / max);

        float w = barWidth * ratio;
        hpFill.localScale = new Vector3(w, barHeight, 1f);
        hpFill.localPosition = new Vector3(-(barWidth - w) * 0.5f, 0f, 0f);

        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);
    }
}
