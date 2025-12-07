using UnityEngine;

public class Critical : MonoBehaviour
{

    private PoolManager pool;

    private void Start()
    {
        pool = GameManager.instance.Pool;
    }

    public void onCrit(Vector3 pos)
    {
        Transform crit = pool.Get(9).transform;
        crit.GetComponent<CritElem>().initCrit(pos);
        //Debug.Log("oncrit done on critical.cs");
    }
}
