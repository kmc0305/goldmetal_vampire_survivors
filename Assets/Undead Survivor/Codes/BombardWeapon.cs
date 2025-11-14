using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public class BombardWeapon : MonoBehaviour
{
    [Header("무기 능력치")]
    public int level = 0;

    /// <summary>이 무기가 Bombard Bullet에게 전달할 기본 공격력</summary>
    public float damage = 1f;
    /// <summary>한 번의 공격에 Bullet을 몇개 발사할지(레벨업 시 증가!)</summary>
    public int count = 0;
    /// <summary>쿨타임 이후로 경과한 시간</summary>
    private float timer = 0f;
    /// <summary>쿨타임 시간마다 count발 발사</summary>
    public float cooldown = 4f;

    [Header("PoolManager 설정")]
    /// <summary>
    /// [중요] PoolManager의 'prefabs' 배열에 등록된
    /// '실제 무기 프리팹(BombardBullet.cs가 붙어있는)'의 인덱스 번호: 여기서는 5
    /// </summary>
    public int weaponPrefabIndex;

    /// <summary>PoolManager 참조</summary>
    private PoolManager poolManager;

    /// <summary>
    /// [Unity 이벤트] Start() - 게임 시작 시 1회 호출
    /// </summary>
    void Start()
    {
        /// GameManager 인스턴스를 통해 PoolManager 참조를 가져옵니다.
        poolManager = GameManager.instance.Pool;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= cooldown)
        {
            timer = 0f;
            doBombard();    ///쿨타임 찰 때마다 포격을 실행한다.
        }
    }


    IEnumerator BombardRoutine(Vector3[] targetPosList)
    {
        for (int i = 0; i < Mathf.Min(count,targetPosList.Length); i++)
        {
            Transform bullet = poolManager.Get(weaponPrefabIndex).transform;
            bullet.position = targetPosList[i];
            bullet.GetComponent<BombardBullet>().Init(damage, targetPosList[i]);
            yield return new WaitForSeconds(0.2f);//TIME DIFF. for each bullet
        }

    }

    void doBombard()    ///실제 Bullet 포격하는 부분
    {
        int enemycnt = highHPTarget.Length;
        if (enemycnt < 1 || count <= 0 || level <= 0) { timer = 3f; return; }
        Vector3[] targetPosList = new Vector3[enemycnt];
        for (int i = 0; i < enemycnt; i++)  targetPosList[i] = highHPTarget[i].position;
        //for (int i = 0; i < 6 - enemycnt; i++)  targetPosList[i + enemycnt] = targetPosList[i % enemycnt];
        StartCoroutine(BombardRoutine(targetPosList));

        if (count >= 6)
        {
            Transform specialbullet = poolManager.Get(weaponPrefabIndex).transform;
            specialbullet.position = transform.parent.position;
            specialbullet.GetComponent<BombardBullet>().Init(0, specialbullet.position);
            return;
        }
    }



    ///무기 전용 스캐너 부분
    public float scanBRange;    ///인스펙터에서 정했음
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
            if (enemy.faction != Targetable.Faction.Enemy) continue;    ///아군 팩션이면 스캔 대상에서 제외
            if (enemy.maxHealth < 0 || enemy.currentHealth < 0) continue;

            enemies.Add(enemy);
        }

        enemies.Sort(
            (Targetable a, Targetable b) => {
                int maxHpComp = b.maxHealth.CompareTo(a.maxHealth);
                if (maxHpComp != 0) return maxHpComp;
                else return b.currentHealth.CompareTo(a.currentHealth);
            });

        int cnt = Mathf.Min(6, enemies.Count);    ///MAX 6 TIMES
        Transform[] result = new Transform[cnt];
        for (int i = 0; i < cnt; i++) result[i] = enemies[i].transform;

        return result;
    }

    public int[] UpgradeCounts = {0,1,2,3,4,6};
    public void LevelUp(int lvl)
    {
        level = lvl;
        count = UpgradeCounts[lvl];
    }
}
