using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public class RangeWeapon : MonoBehaviour
{
    [Header("무기 능력치")]
    public float damage;
    public int per;

    private float timer = 0f;
    private float cooldown = 0.4f;

    [Header("PoolManager 설정")]
    public int weaponPrefabIndex;   //Range: 6

    private PoolManager poolManager;
    void Start()
    {
        poolManager = GameManager.instance.Pool;
    }
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= cooldown)
        {
            timer = 0f;
            doFire();
        }
    }

    void doFire()
    {
        if (!nearestTarget) return;

        Vector3 targetPos=nearestTarget.position;
        Vector3 dir=(targetPos - this.transform.position).normalized;
        //무기 위치(=플레이어 위치)에서 타겟으로 향하는 방향의 단위벡터

        Transform bullet = poolManager.Get(weaponPrefabIndex).transform;
        bullet.position = this.transform.position;
        bullet.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        bullet.GetComponent<RangeBullet>().Init(damage, per, dir);
    }



    //전용 스캐너 부분
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
}
