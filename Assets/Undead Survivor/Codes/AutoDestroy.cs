using UnityEngine;
using System.Collections;

public class AutoDestroy : MonoBehaviour
{
    [Header("설정")]
    public float lifeTime = 0.5f;     // 1. 몇 초 뒤에 사라지게 할지 (애니메이션 길이보다 조금 길게)
    public bool useFadeOut = true;    // 2. 투명해지면서 사라질지 여부
    public float fadeDuration = 0.5f; // 3. 투명해지는 데 걸리는 시간

    private SpriteRenderer sprite;

    void Start()
    {
        sprite = GetComponent<SpriteRenderer>();
        // 코루틴 시작: 대기 -> (페이드아웃) -> 삭제
        StartCoroutine(DestroyRoutine());
    }

    IEnumerator DestroyRoutine()
    {
        // 1단계: 지정된 시간만큼 대기 (이때 땅 갈라진 모습 유지)
        yield return new WaitForSeconds(lifeTime);

        // 2단계: 서서히 투명해지기
        if (useFadeOut && sprite != null)
        {
            Color startColor = sprite.color;
            float timer = 0f;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);

                // 알파값(투명도) 조절
                sprite.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }

        // 3단계: 오브젝트 완전 삭제
        Destroy(gameObject);
    }
}