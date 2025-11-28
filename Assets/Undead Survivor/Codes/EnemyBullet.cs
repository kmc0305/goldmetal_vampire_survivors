using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    private float damage;
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
    }

    // 무기 데이터 초기화
    public void Init(EnemyWeaponData data, Vector3 dir)
    {
        this.damage = data.damage;

        // 스프라이트 적용
        if (data.bulletSprite != null)
            spriter.sprite = data.bulletSprite;

        spriter.color = data.bulletColor;

        // 탄환 발사
        rigid.linearVelocity = dir * data.bulletSpeed;

        // 🔥 기본 스프라이트가 "위쪽"을 향하므로 90도 보정
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Targetable target = collision.GetComponent<Targetable>();

        if (target == null || target.faction == Targetable.Faction.Enemy)
            return;

        target.TakeDamage(damage, transform);

        gameObject.SetActive(false);
    }

    void OnBecameInvisible()
    {
        gameObject.SetActive(false);
    }
}
