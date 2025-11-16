using UnityEngine;
using System.Collections;

public class CircleSector : MonoBehaviour
{
    private float dmg = 6f;
    private float s_scale = 3f;
    private float m_scale = 6f;
    private float l_scale = 9f;
    private float scale = 0f;

    private void Awake()
    {
        transform.localScale = new Vector3(0f,0f,0f);
    }

    public void doExpand()
    {
        StartCoroutine(ExpandSeq());
    }

    IEnumerator ExpandSeq()
    {
        yield return ExpandC(s_scale,0.3f);
        yield return ExpandC(m_scale, 0.25f);
        yield return ExpandC(l_scale, 0.2f);
    }

    IEnumerator ExpandC(float targetscale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            scale = Mathf.Lerp(0f, targetscale, elapsed / duration);
            transform.localScale=new Vector3 (scale, scale, scale);
            elapsed += Time.deltaTime;
            yield return null;
        }
        elapsed = 0f;
        while(elapsed < duration)
        {
            scale = Mathf.Lerp(targetscale, 0f, elapsed / duration);
            transform.localScale = new Vector3(scale, scale, scale);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Targetable target = collision.GetComponent<Targetable>();
        if (target == null) return;

        if (target.faction == Targetable.Faction.Enemy)
        {
            target.TakeDamage(dmg, transform);
        }
    }
}
