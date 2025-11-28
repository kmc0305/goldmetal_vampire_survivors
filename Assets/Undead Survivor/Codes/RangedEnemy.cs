using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class RangedEnemy : MonoBehaviour
{
    [Header("무기 데이터")]
    public EnemyWeaponData weaponData;
    public int bulletPrefabId;

    [Header("기본 설정")]
    public float speed = 2.0f;
    public LayerMask targetLayer;
    public float detectionRadius = 15f;

    [Header("AI 업데이트")]
    public float aiUpdateFrequency = 0.5f;
    private float aiUpdateRandomDelay = 0.1f;

    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private Targetable currentTarget;
    private Targetable myTargetable;
    private float lastAttackTime;

    private Coroutine aiCoroutine;
    private Animator anim;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        myTargetable = GetComponent<Targetable>();
        anim = GetComponent<Animator>(); // 애니메이션 추가
    }

    void OnEnable()
    {
        currentTarget = null;

        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutineDelayed());
    }

    void OnDisable()
    {
        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }

        currentTarget = null;
        rigid.linearVelocity = Vector2.zero;
    }

    IEnumerator UpdateTargetCoroutineDelayed()
    {
        float initialDelay = Random.Range(0f, aiUpdateFrequency);
        yield return new WaitForSeconds(initialDelay);

        StartCoroutine(UpdateTargetCoroutine());
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            // 타겟이 죽었으면 해제
            if (currentTarget != null && currentTarget.isDead)
                currentTarget = null;

            // 타겟 없으면 새로 탐색
            if (currentTarget == null)
                currentTarget = FindClosestTarget();

            float wait = aiUpdateFrequency + Random.Range(-aiUpdateRandomDelay, aiUpdateRandomDelay);
            if (wait < 0.1f) wait = 0.1f;

            yield return new WaitForSeconds(wait);
        }
    }

    Targetable FindClosestTarget()
    {
        float closest = float.MaxValue;
        Targetable best = null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);

        foreach (Collider2D col in hits)
        {
            Targetable t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < closest)
                {
                    closest = d;
                    best = t;
                }
            }
        }

        return best;
    }

    void FixedUpdate()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack)
            return;

        if (currentTarget == null)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

        // 공격 사거리 안 -> 멈춰서 공격 준비
        if (weaponData != null && dist <= weaponData.attackRange)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }

        // 공격 사거리 밖 -> 이동하며 추적
        Vector2 dir = (currentTarget.transform.position - transform.position).normalized;
        Vector2 step = dir * speed * Time.fixedDeltaTime;

        rigid.MovePosition(rigid.position + step);
    }

    void Update()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack)
            return;
        if (currentTarget == null || weaponData == null)
            return;

        spriter.flipX = currentTarget.transform.position.x < transform.position.x;

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

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
        // 공격 애니메이션
        if (anim != null)
            anim.SetTrigger("Attack");

        GameObject bulletObj = GameManager.instance.Pool.Get(bulletPrefabId);
        if (bulletObj == null) return;

        bulletObj.transform.position = transform.position;

        Vector3 dir = (currentTarget.transform.position - transform.position).normalized;

        EnemyBullet b = bulletObj.GetComponent<EnemyBullet>();
        if (b != null)
        {
            b.Init(weaponData, dir);
        }
    }

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
