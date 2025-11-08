using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public class BombardWeapon : MonoBehaviour
{
    [Header("무기 능력치")]
    /// <summary>이 무기가 Bombard Bullet에게 전달할 기본 공격력</summary>
    public float damage = 1f;
    /// <summary>한 번의 공격에 Bullet을 몇개 발사할지(레벨업 시 증가!)</summary>
    public int count = 1;
    /// <summary>쿨타임 이후로 경과한 시간</summary>
    private float timer = 0f;
    public float cooldown = 4f;

    [Header("PoolManager 설정")]
    /// <summary>
    /// [중요] PoolManager의 'prefabs' 배열에 등록된
    /// '실제 무기 프리팹(MeleeWeapon.cs가 붙어있는)'의 인덱스 번호
    /// </summary>
    public int weaponPrefabIndex;

    /// <summary>PoolManager 참조</summary>
    private PoolManager poolManager;

    /// <summary>
    /// [Unity 이벤트] Start() - 게임 시작 시 1회 호출
    /// </summary>
    void Start()
    {
        // GameManager 인스턴스를 통해 PoolManager 참조를 가져옵니다.
        poolManager = GameManager.instance.Pool;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= cooldown)
        {
            timer = 0f;
            doBombard();
        }
    }

    void doBombard()    //실제 Bullet 발사
    {
        if (highHPTarget.Length == 0) return;      //스캐너 적 미탐지시 발사 안함
        Vector3 targetPos = highHPTarget[0].position;
        GameObject bullet=poolManager.Get(weaponPrefabIndex);
        bullet.GetComponent<BombardBullet>().Init(damage,targetPos);

    }


    //무기 전용 스캐너 부분
    public float scanBRange;    //인스펙터에서 정했음
    private RaycastHit2D[] targets;
    private Transform[] highHPTarget;

    void FixedUpdate()
    {
        targets = Physics2D.CircleCastAll(transform.position, scanBRange, Vector2.zero, 0);
        highHPTarget = GetHighestHP();
    }

    Transform[] GetHighestHP()
    {
        if (targets == null || targets.Length == 0) return null;

        List<Targetable> enemies = new List<Targetable>();
        

        foreach (RaycastHit2D target in targets)
        {
            Targetable enemy = target.collider.GetComponent<Targetable>();
            if (enemy == null) continue;
            if (enemy.faction != Targetable.Faction.Enemy) continue;    //아군 팩션이면 스캔 대상에서 제외
            if (enemy.maxHealth < 0 || enemy.currentHealth < 0) continue;

            enemies.Add(enemy);
        }

        enemies.Sort(
            (Targetable a, Targetable b) => {
                int maxHpComp = b.maxHealth.CompareTo(a.maxHealth);
                if (maxHpComp != 0) return maxHpComp;
                else return b.currentHealth.CompareTo(a.currentHealth);
            });

        int cnt = Mathf.Min(6, enemies.Count);    //MAX 6 TIMES
        Transform[] result = new Transform[cnt];
        for (int i = 0; i < cnt; i++) result[i] = enemies[i].transform;

        return result;
    }

}
