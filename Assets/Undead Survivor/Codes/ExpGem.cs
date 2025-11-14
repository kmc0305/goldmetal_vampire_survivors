using UnityEngine;

public class ExpGem : MonoBehaviour
{
    public int gemexp = 1;

    private Rigidbody2D rigid;
    private Collider2D coll;
    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        Player p=collision.GetComponent<Player>();
        if (p == null) return;

        GameManager.instance.exp += gemexp;
        gameObject.SetActive(false);
        return;
    }
}
