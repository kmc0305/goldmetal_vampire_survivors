using System.Collections;
using UnityEngine;

public class RangedEnemy : MonoBehaviour
{
    [Header("무기 데이터")]
    public EnemyWeaponData weaponData;
    public int bulletPrefabId;

    [Header("기본 설정")]
    public float speed = 2.0f;
    public LayerMask targetLayer;
    public float detectionRadius = 15f;

    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private Targetable currentTarget;
    private float lastAttackTime;
    private Targetable myTargetable; // 내 자신의 Targetable

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        myTargetable = GetComponent<Targetable>();
    }

    void OnEnable()
    {
        currentTarget = null;
        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            // 넉백 중이 아닐 때만 타겟 갱신 (선택사항)
            currentTarget = FindClosestTarget();
            yield return new WaitForSeconds(0.5f);
        }
    }

    // ... FindClosestTarget 함수는 기존과 동일 ...
    Targetable FindClosestTarget()
    {
        float closestDist = float.MaxValue;
        Targetable bestTarget = null;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);

        foreach (Collider2D col in hits)
        {
            Targetable t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                float dist = UnityEngine.Vector3.Distance(transform.position, t.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = t;
                }
            }
        }
        return bestTarget;
    }

    void FixedUpdate()
    {
        // [중요] 넉백 중이면 이동 로직 건너뜀
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

        if (currentTarget == null)
        {
            rigid.linearVelocity = UnityEngine.Vector2.zero;
            return;
        }

        float distance = UnityEngine.Vector3.Distance(transform.position, currentTarget.transform.position);
        UnityEngine.Vector2 dir = (currentTarget.transform.position - transform.position).normalized;

        if (distance > weaponData.attackRange)
        {
            rigid.MovePosition(rigid.position + dir * speed * Time.fixedDeltaTime);
        }
        else
        {
            rigid.linearVelocity = UnityEngine.Vector2.zero;
        }
    }

    void Update()
    {
        // 넉백 중이면 회전/발사 안 함
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null) return;
        if (weaponData == null) return;

        spriter.flipX = currentTarget.transform.position.x < transform.position.x;

        float dist = UnityEngine.Vector3.Distance(transform.position, currentTarget.transform.position);

        if (dist <= weaponData.attackRange)
        {
            if (Time.time >= lastAttackTime + weaponData.cooldown)
            {
                Fire();
                lastAttackTime = Time.time;
            }
        }
    }

    void Fire()
    {
        GameObject bulletObj = GameManager.instance.Pool.Get(bulletPrefabId);
        if (bulletObj == null) return;

        bulletObj.transform.position = transform.position;

        UnityEngine.Vector3 dir = (currentTarget.transform.position - transform.position).normalized;

        EnemyBullet bulletScript = bulletObj.GetComponent<EnemyBullet>();
        if (bulletScript != null)
        {
            bulletScript.Init(weaponData, dir);
        }
    }

    // [디버그용]
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        if (weaponData != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, weaponData.attackRange);
        }
    }

}