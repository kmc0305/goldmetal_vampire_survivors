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

    // 무기 데이터(ScriptableObject)를 받아 초기화
    public void Init(EnemyWeaponData data, Vector3 dir)
    {
        this.damage = data.damage;

        // 스프라이트와 색상 적용
        if (data.bulletSprite != null) spriter.sprite = data.bulletSprite;
        spriter.color = data.bulletColor;

        // 탄환 발사 (Unity 6버전 기준 linearVelocity 사용)
        rigid.linearVelocity = dir * data.bulletSpeed;

        // 발사체 회전 (진행 방향 보기)
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어(Targetable) 확인
        Targetable target = collision.GetComponent<Targetable>();

        // 타겟이 없거나, 같은 적(Enemy) 진영이면 무시
        if (target == null || target.faction == Targetable.Faction.Enemy)
            return;

        // 플레이어 진영이면 데미지 주기
        target.TakeDamage(damage, transform);

        // 탄환 비활성화 (반납)
        gameObject.SetActive(false);
    }

    // 화면 밖으로 나가면 비활성화 (Reposition 스크립트가 없다면 필요)
    void OnBecameInvisible()
    {
        gameObject.SetActive(false);
    }
}