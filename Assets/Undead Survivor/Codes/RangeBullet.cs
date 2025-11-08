using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RangeBullet : MonoBehaviour
{
    //데미지 등의 탄환 속성은 무기를 통해 넣어줄 것임
    private float currentDamage;
    private int per; //더 이상 관통 가능한 적 개수

    Rigidbody2D rigid;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
    }

    public void Init(float damage, int per, Vector3 dir)
    {
        this.currentDamage = damage;
        this.per = per;
        if (per > -1)
        {
            rigid.linearVelocity = dir * 15f;
        }

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Targetable target=collision.GetComponent<Targetable>();
        if (target == null) return;

        if (target.faction != Targetable.Faction.Enemy || per == -1)
            return;


        if (target.faction == Targetable.Faction.Enemy)
        {
            target.TakeDamage(currentDamage, transform);
            per--;
        }

        if (per <= -1)
        {
            rigid.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);    //관통 가능 횟수를 전부 소진 시 비활성화
        }
    }

}