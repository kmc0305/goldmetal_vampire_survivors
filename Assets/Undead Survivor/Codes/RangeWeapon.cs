using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public class RangeWeapon : MonoBehaviour
{
    [Header("무기 능력치")]
    public int level = 0;

    /// <summary>이 무기가 Range Bullet에게 전달할 기본 공격력</summary>
    public float damage;
    /// <summary>하나의 Bullet이 몇 명의 적을 관통할 수 있는지(예시: 1은 적 하나 관통가능)</summary>
    public int per;
    private float timer = 0f;
    private float timer2 = 0f;
    /// <summary>쿨타임 시간마다 발사</summary>
    private float cooldown = 0.4f;

    [Header("PoolManager 설정")]
    /// <summary>
    /// [중요] PoolManager의 'prefabs' 배열에 등록된
    /// '실제 무기 프리팹(BombardBullet.cs가 붙어있는)'의 인덱스 번호: 여기서는 6
    /// </summary>
    public int weaponPrefabIndex;

    /// <summary>PoolManager 참조</summary>
    private PoolManager poolManager;
    void Start()
    {
        /// GameManager 인스턴스를 통해 PoolManager 참조를 가져옵니다.
        poolManager = GameManager.instance.Pool;
    }
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= cooldown)
        {
            timer = 0f;
            doFire();   ///쿨타임 찰 때마다 발사 실행
        }
        timer2+= Time.deltaTime;
        if (level >= 5 && timer2 >= 6.0f) { timer2 = 0f; UltRange();  }
    }

    void doFire()   ///발사를 실행하는 함수
    {
        if (!nearestTarget || level==0) return; ///적 미탐지시 발사 안함

        Vector3 targetPos=nearestTarget.position;
        Vector3 dir=(targetPos - this.transform.position).normalized;
        ///무기 위치(=플레이어 위치)에서 타겟으로 향하는 방향의 단위벡터

        Transform bullet = poolManager.Get(weaponPrefabIndex).transform;

        ///Bullet의 시작 위치, 회전한 각도를 정한다.
        bullet.position = this.transform.position;
        bullet.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        ///Bullet이 활성화 된 직후 데미지, 관통수, 방향의 정보를 Bullet에 Init()으로 제공
        bullet.GetComponent<RangeBullet>().Init(damage, per, dir);

    }



    ///전용 스캐너 부분
    public float scanRange;
    private RaycastHit2D[] targets;
    private Transform nearestTarget;

    void FixedUpdate()
    {
        targets = Physics2D.CircleCastAll(transform.position, scanRange, Vector2.zero, 0);
        nearestTarget = GetNearest();
    }

    Transform GetNearest()
    {
        Transform result = null;
        float diff = scanRange+5f;

        foreach (RaycastHit2D target in targets)
        {
            Targetable enemy = target.collider.GetComponent<Targetable>();
            if (enemy == null) continue;
            if (enemy.faction != Targetable.Faction.Enemy) continue;

            Vector3 myPos = transform.position;
            Vector3 targetPos = target.transform.position;
            float curDiff = Vector3.Distance(myPos, targetPos);

            if (curDiff < diff)
            {
                diff = curDiff;
                result = target.transform;
            }
        }

        return result;
    }

    public Transform AuraMaster;
    void UltRange()
    {
        if (nearestTarget == null) { timer2 = 5f; return; }  
        Vector3 dir= nearestTarget.transform.position - AuraMaster.position;
        float angle=Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg;
        AuraMaster.rotation = Quaternion.Euler(0, 0, angle);
        Aura[] a = GetComponentsInChildren<Aura>();
        a[0].doAura(nearestTarget);
        a[1].doAura(nearestTarget);
        a[2].doAura(nearestTarget);
    }

    public int[] UpgradePer = { 0, 1, 1, 2, 2, 3 };
    public int[] UpgradeDMG = { 0, 1, 1, 2, 2, 3 };
    public void LevelUp(int lvl)
    {
        level = lvl;
        per = UpgradePer[lvl];
        damage = UpgradeDMG[lvl];
    }
}
