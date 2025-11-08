using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BombardBullet : MonoBehaviour      //BombardWeapon으로 발사한 Bombard Bullet
{
    private float currentDamage = 10f;  //기본 데미지
    private float timer = 0f;
    private float duration = 2f;
    private Collider2D coll;
    Rigidbody2D rigid;
    
    //잔류하는 Bullet에 의해 한 번 데미지를 입은 적이 다시 데미지를 입는 것을 방지함
    private HashSet<GameObject> damagedEnemies= new();

    //이 무기의 수명, 비활성화 로직은 Bullet 본인이 담당(Melee와의 차이)
    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        ///rigidbody2d 써야 성 공격 가능
    }
    public void Init(float dmg, Vector3 Coord) //부모인 BombardWeapon에서 데미지, 소환 좌표를 받습니다.
    {
        //Bullet이 활성화(발사) 될때마다 Init을 호출하니, OnEnable 역할도 겸임하는 것입니다.
        damagedEnemies.Clear();
        this.timer = 0f;
        coll = GetComponent<Collider2D>();
        if (coll != null) coll.enabled = true;
        currentDamage = dmg;
        this.transform.position = Coord;
        //부모인 BombardWeapon은 Bullet 활성화와 동시에 Init으로 좌표를 주고,
        //Bullet은 그 좌표로 순간이동합니다(장판이 소환됩니다)
    }

    private void Update()   //duration만큼 시간이 지나면 비활성화
    {
        timer += Time.deltaTime;
        if(timer>0.1f && coll != null) coll.enabled=false;  //Bullet(장판)생성 직후 비활성화하여 더이상 충돌 방지
        if(timer>duration) gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Targetable target = other.GetComponent<Targetable>();
        if (target == null) return;
        if (damagedEnemies.Contains(other.gameObject)) return;  //데미지 중복 방지

        // 3. 부딪힌 대상이 'Enemy' 진영이면 실행
        if (target.faction == Targetable.Faction.Enemy)
        {
            // 4. [핵심] 적('Enemy')의 Targetable 스크립트에 TakeDamage() 함수를 호출합니다.
            //    넉백 방향 계산을 위해 '나(무기)'의 위치(transform)를 넘겨줍니다.
            target.TakeDamage(currentDamage, transform);

            GameObject damagedEnemy = other.gameObject;
            if(!damagedEnemies.Contains(damagedEnemy))
                damagedEnemies.Add(damagedEnemy);
        }
    }

}
