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
        // 플레이어 태그를 확인하거나 Player 컴포넌트 존재 여부 확인
        Player p = collision.GetComponent<Player>();

        // 플레이어가 아니면 무시
        if (p == null) return;

        // GameManager 인스턴스가 존재하는지 확인 후 경험치 획득 함수 호출
        if (GameManager.instance != null)
        {
            // [수정] getExp()를 호출해야 레벨업 체크가 됨
            GameManager.instance.getExp();
        }

        // 아이템 비활성화 (풀링으로 반환)
        gameObject.SetActive(false);
    }
}