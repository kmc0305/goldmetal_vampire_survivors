using UnityEngine;
using System.Collections;

public class CircleSector : MonoBehaviour
{
    private float dmg = 6f;
    private float s_scale = 3f;
    private float m_scale = 6f;
    private float l_scale = 9f;
    private float scale = 0f;
    private Animator animC;
    private SpriteRenderer sr;

    private void Awake()
    {
        //transform.localScale = new Vector3(0f,0f,0f);
        animC = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        sr.color = new Color(0, 0, 0, 0);
    }

    public void doExpand()
    {
        StartCoroutine(ExpandSeq());
    }
    private void Update()
    {
        //transform.Rotate(Vector3.up, 90, Space.World);
    }

    IEnumerator ExpandSeq()
    {
        yield return ExpandC(s_scale,1.3f);
        yield return ExpandC(m_scale, 1.3f);
        yield return ExpandC(l_scale, 1.3f);
    }


    IEnumerator ExpandC(float targetscale, float duration)
    {
        transform.localScale = new Vector3(4f,4f,4f);
        float elapsed = 0f;
        animC.SetTrigger("Ice_Trigger");
        sr.color = new Color(255, 255, 255, 190);
        while (elapsed < duration)
        {
            //scale = Mathf.Lerp(0f, targetscale, elapsed / duration);
            //transform.localScale=new Vector3 (scale, scale, scale);
            elapsed += Time.deltaTime;
            yield return null;
        }
        elapsed = 0f;
        sr.color = new Color(0, 0, 0, 0);
        animC.SetTrigger("Ice_Return");
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
