using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RangeBullet : MonoBehaviour
{
    ///데미지 등의 탄환 속성은 무기(RangeWeapon.cs)를 통해 넣어줄 것임
    private float currentDamage;
    private int per; //더 이상 관통 가능한 적 개수

    Rigidbody2D rigid;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
    }

    public void Init(float damage, int per, Vector3 dir)
    {
        ///Bullet이 활성화 되는 순간: 데미지,관통수,방향(속도)가 Bullet에 정해진다.
        this.currentDamage = damage;
        this.per = per;
        if (per > -1)
        {
            rigid.linearVelocity = dir * 25f;
        }

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ///타겟이 범위 내에 없거나, 적 진영이 아니면 충돌하여 데미지를 주지 않음
        Targetable target=collision.GetComponent<Targetable>();
        if (target == null) return;

        if (target.faction != Targetable.Faction.Enemy || per == -1)
            return;

        ///적에게 충돌하면 데미지를 주고, 관통수를 1만큼 감소
        if (target.faction == Targetable.Faction.Enemy)
        {
            target.TakeDamage(currentDamage, transform);
            per--;
        }

        if (per <= -1)///관통수가 0인 경우: 더 이상 관통을 못 하므로 마지막으로 적 때리고 비활성화
        {
            rigid.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);    //관통 가능 횟수를 전부 소진 시 비활성화
        }
    }

}