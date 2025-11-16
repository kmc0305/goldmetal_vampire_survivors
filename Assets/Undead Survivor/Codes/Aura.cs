using System.Collections;
using UnityEngine;

public class Aura : MonoBehaviour
{
    Transform pivot;
    public float AuraDMG = 10f;
    private float AuraScale = 20f;      //20f in 0.3s 
    private float scale = 0f;


    private void Awake()
    {
        pivot = transform.parent;
        pivot.localScale = new Vector3(0,0,0);
    }

    public void doAura(Transform Nearest)
    {
        StartCoroutine(ExpandAura());
    }
    
    IEnumerator ExpandAura()
    {
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            scale =Mathf.Lerp(0f,AuraScale,elapsed/0.3f);
            pivot.localScale = new Vector3(scale, 1, 0);
            elapsed+= Time.deltaTime;
            yield return null;
        }
        scale = 0f;
        pivot.localScale = new Vector3(0, 0, 0);
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        Targetable target = collision.GetComponent<Targetable>();
        if (target == null) return;

        if (target.faction != Targetable.Faction.Enemy)
            return;

        if (target.faction == Targetable.Faction.Enemy)
        {
            target.TakeDamage(AuraDMG, transform);
        }
    }
}
