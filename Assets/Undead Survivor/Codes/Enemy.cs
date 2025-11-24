using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    // ... (기존 변수들 유지) ...
    [Header("Friendly Tower Avoidance")]
    public float avoidDuration = 0.6f;
    public float avoidSpeedMul = 1.2f;
    [Header("Avoidance Filters")]
    public float avoidanceGrace = 0.35f;
    public float minSpeedToAvoid = 0.1f;
    public float minDotBlock = 0.25f;

    private float avoidUntil = 0f;
    private UnityEngine.Vector2 avoidDir = UnityEngine.Vector2.zero;
    private float spawnGraceUntil = 0f;

    [Header("Boss HP Bar")]
    public Transform hpBarRoot;
    public Transform hpFill;
    public float barWidth = 2.0f;
    public float barHeight = 0.25f;
    public UnityEngine.Vector3 barOffset = new UnityEngine.Vector3(0f, 1.5f, 0f);

    [Header("범위공격 옵션")]
    public bool isAreaAttack = false;
    public float areaAttackRadius = 3.0f;

    // ★★★ [추가] 광역 공격 시 생성할 이펙트 프리팹 ★★★
    public GameObject areaAttackEffectPrefab;

    [Header("기본 능력치")]
    public float speed = 2.5f;
    public float health;
    public float maxHealth;
    public RuntimeAnimatorController[] animCon;

    [Header("AI 설정")]
    public LayerMask targetLayer;
    public float detectionRadius = 15f;
    private float aiUpdateFrequency = 0.5f;

    [Header("공격 설정")]
    public float attackDamage = 5f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    // 컴포넌트
    private Rigidbody2D rigid;
    private SpriteRenderer spriter;
    private Coroutine aiCoroutine;
    private Animator anim;
    private Targetable currentTarget;
    private Targetable myTargetable; // 내 자신의 Targetable 참조

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        myTargetable = GetComponent<Targetable>(); // Targetable 컴포넌트 가져오기
    }

    void OnEnable()
    {
        spawnGraceUntil = Time.time + avoidanceGrace;
        avoidDir = UnityEngine.Vector2.zero;
        avoidUntil = 0f;

        if (aiCoroutine == null)
            aiCoroutine = StartCoroutine(UpdateTargetCoroutine());

        UpdateHPBar();
    }

    // ... (OnDisable, UpdateTargetCoroutine, FindClosestTarget, UpdateHPBar 등은 기존 유지) ...
    void OnDisable()
    {
        if (aiCoroutine != null)
        {
            StopCoroutine(aiCoroutine);
            aiCoroutine = null;
        }
        // 킬 수 증가는 Targetable에서 처리하므로 여기서는 제거해도 됨 (중복 방지)

        currentTarget = null;
        if (rigid != null)
            rigid.linearVelocity = UnityEngine.Vector2.zero;
        avoidDir = UnityEngine.Vector2.zero;
        avoidUntil = 0f;
    }

    IEnumerator UpdateTargetCoroutine()
    {
        while (gameObject.activeSelf)
        {
            // 넉백 중에는 타겟 탐색도 잠시 쉴 수 있음 (선택사항)
            currentTarget = FindClosestTarget();
            yield return new WaitForSeconds(aiUpdateFrequency);
        }
    }

    Targetable FindClosestTarget()
    {
        float closestDistance = float.MaxValue;
        Targetable bestTarget = null;
        Collider2D[] targetsInView = Physics2D.OverlapCircleAll(transform.position, detectionRadius, targetLayer);

        foreach (Collider2D col in targetsInView)
        {
            var t = col.GetComponent<Targetable>();
            if (t != null && !t.isDead)
            {
                float d = UnityEngine.Vector3.Distance(transform.position, t.transform.position);
                if (d < closestDistance)
                {
                    closestDistance = d;
                    bestTarget = t;
                }
            }
        }
        return bestTarget;
    }

    void UpdateHPBar()
    {
        if (!hpBarRoot || !hpFill) return;
        float cur = health;
        float max = maxHealth;
        if (myTargetable != null)
        {
            cur = myTargetable.currentHealth;
            max = myTargetable.maxHealth;
        }
        float ratio = (max > 0f) ? Mathf.Clamp01(cur / max) : 0f;
        hpBarRoot.localPosition = barOffset;
        float w = barWidth * ratio;
        hpFill.localScale = new UnityEngine.Vector3(w, barHeight, 1f);
        hpFill.localPosition = new UnityEngine.Vector3(-(barWidth - w) * 0.5f, 0f, 0f);
        var sr = hpFill.GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.Lerp(Color.red, Color.green, ratio);
        if (myTargetable != null) hpBarRoot.gameObject.SetActive(!myTargetable.isDead);
    }

    bool IsFriendlyTower(GameObject other)
    {
        if (other.layer != gameObject.layer) return false;
        return other.GetComponent<SpawnPoint>() != null
            || other.GetComponentInParent<SpawnPoint>() != null
            || other.GetComponent<AllySpawner>() != null
            || other.GetComponentInParent<AllySpawner>() != null;
    }

    void FixedUpdate()
    {
        // [중요] Targetable이 넉백 중이라면 이동 로직을 건너뜀
        if (myTargetable != null && myTargetable.IsKnockedBack) return;

        if (Time.time < avoidUntil && avoidDir.sqrMagnitude > 0.0001f)
        {
            UnityEngine.Vector2 step = avoidDir.normalized * speed * avoidSpeedMul * Time.fixedDeltaTime;
            rigid.MovePosition(rigid.position + step);
            rigid.linearVelocity = UnityEngine.Vector2.zero;
            return;
        }

        if (currentTarget == null)
        {
            rigid.linearVelocity = UnityEngine.Vector2.zero;
            return;
        }

        UnityEngine.Vector2 dirVec = currentTarget.transform.position - transform.position;
        UnityEngine.Vector2 nextVec = dirVec.normalized * speed * Time.fixedDeltaTime;
        rigid.MovePosition(rigid.position + nextVec);
        rigid.linearVelocity = UnityEngine.Vector2.zero;
    }

    // ... (LateUpdate, OnCollisionEnter2D, OnCollisionStay2D 등은 기존 유지) ...
    void LateUpdate()
    {
        if (myTargetable != null && myTargetable.IsKnockedBack) return;
        if (currentTarget == null) return;
        spriter.flipX = currentTarget.transform.position.x < rigid.position.x;
        UpdateHPBar();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartAvoidance(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryStartAvoidance(collision);
        if (Time.time < lastAttackTime + attackCooldown) return;
        if (myTargetable != null && myTargetable.IsKnockedBack) return; // 넉백 중 공격 불가
        if (currentTarget == null) return;
        if (collision.gameObject != currentTarget.gameObject) return;

        if (isAreaAttack) DoAreaAttack();
        else currentTarget.TakeDamage(attackDamage, transform);

        lastAttackTime = Time.time;
    }

    // ... (TryStartAvoidance, DoAreaAttack 등 유지) ...
    void TryStartAvoidance(Collision2D collision)
    {
        if (Time.time < spawnGraceUntil) return;
        if (!IsFriendlyTower(collision.gameObject) || collision.contactCount == 0) return;
        if (rigid.linearVelocity.sqrMagnitude < minSpeedToAvoid * minSpeedToAvoid && currentTarget == null) return;

        UnityEngine.Vector2 n = collision.GetContact(0).normal;
        UnityEngine.Vector2 desired;
        if (currentTarget != null)
        {
            UnityEngine.Vector2 toTarget = (UnityEngine.Vector2)(currentTarget.transform.position - transform.position);
            desired = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : rigid.linearVelocity.normalized;
        }
        else
        {
            desired = rigid.linearVelocity.sqrMagnitude > 0.0001f ? rigid.linearVelocity.normalized : UnityEngine.Vector2.zero;
        }
        if (desired == UnityEngine.Vector2.zero) return;

        float intoWall = UnityEngine.Vector2.Dot(desired, -n);
        if (intoWall < minDotBlock) return;

        UnityEngine.Vector2 t1 = new UnityEngine.Vector2(-n.y, n.x);
        UnityEngine.Vector2 t2 = new UnityEngine.Vector2(n.y, -n.x);
        UnityEngine.Vector2 chosen = (UnityEngine.Vector2.Dot(t1, desired) >= UnityEngine.Vector2.Dot(t2, desired)) ? t1 : t2;

        avoidDir = chosen.normalized;
        avoidUntil = Time.time + avoidDuration;
    }

    void DoAreaAttack()
    {

        // ★★★ [추가] 공격 이펙트 생성 ★★★
        if (areaAttackEffectPrefab != null)
        {
            // 보스 위치(transform.position)에 이펙트 생성
            Instantiate(areaAttackEffectPrefab, transform.position, Quaternion.identity);
        }

        // 반경 내 '타겟 레이어'에 해당하는 모든 대상 탐색
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, areaAttackRadius, targetLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            var t = hits[i].GetComponent<Targetable>();
            if (t == null || t.isDead) continue;
            t.TakeDamage(attackDamage, transform);
        }
    }

    // [삭제] 기존 ApplyKnockback, FlashRoutine 등은 Targetable이 대신하므로 삭제해도 됨

    public void slowDown(float x, float dur)
    {
        StartCoroutine(SlowDownFor(x, dur));
    }

    private IEnumerator SlowDownFor(float x, float dur)
    {
        float og = speed;
        speed = speed * x;
        yield return new WaitForSeconds(dur);
        speed = og;
    }

    public void init(SpawnData data)
    {
        if (data == null) return;
        if (animCon != null && data.spriteType >= 0 && data.spriteType < animCon.Length)
            anim.runtimeAnimatorController = animCon[data.spriteType];
        speed = data.speed;
        maxHealth = data.health;
        health = data.health;
        // Targetable 데이터도 동기화
        if (myTargetable != null)
        {
            myTargetable.maxHealth = data.health;
            myTargetable.currentHealth = data.health;
        }
    }

    public void ApplyBossSpec(BossSpec spec)
    {
        if (spec == null) return;
        attackDamage = spec.attackDamage;
        attackCooldown = spec.attackCooldown;
        detectionRadius = spec.detectionRadius;
        speed = spec.moveSpeed;
        maxHealth = spec.maxHP;
        health = spec.maxHP;
        // Targetable 동기화
        if (myTargetable != null)
        {
            myTargetable.maxHealth = spec.maxHP;
            myTargetable.currentHealth = spec.maxHP;
        }
        isAreaAttack = spec.isAreaAttack;
        areaAttackRadius = spec.areaRadius;
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr) sr.color = spec.tint;
        UpdateHPBar();
    }

    void OnDrawGizmosSelected()
    {
        if (isAreaAttack)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, areaAttackRadius);
        }
    }
}