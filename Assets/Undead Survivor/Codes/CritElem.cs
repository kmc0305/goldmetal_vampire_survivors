using UnityEngine;
using System.Collections;

public class CritElem : MonoBehaviour 
{
    private SpriteRenderer sr;
    private Animator anim;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }
    public void initCrit(Vector3 pos)
    {
        this.transform.position = pos;
        StartCoroutine(crit_routine());
        
    }
    IEnumerator crit_routine()
    {
        // 첫 번째 트리거
        anim.SetTrigger("doCrit");
        yield return new WaitForSeconds(0.2f);

        // 두 번째 트리거
        //anim.SetTrigger("exitCrit");
        yield return new WaitForSeconds(0.4f);

        // 오브젝트 비활성화
        gameObject.SetActive(false);

    }

}
