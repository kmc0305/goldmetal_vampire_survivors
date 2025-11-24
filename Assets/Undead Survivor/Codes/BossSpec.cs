using UnityEngine;

[CreateAssetMenu(fileName = "BossSpec", menuName = "Game/Boss Spec")]
public class BossSpec : ScriptableObject
{
    [Header("공격 설정")]
    public float attackDamage = 12f;
    public float attackCooldown = 1.0f;
    public bool isAreaAttack = false;
    public float areaRadius = 3.0f;

    // ★ 여기에 이펙트를 넣으면 적용되고, 비워두면 Enemy 기본값을 씁니다.
    public GameObject areaAttackEffect;

    [Header("탐지/이동")]
    public float detectionRadius = 15f;
    public float moveSpeed = 2.0f;

    [Header("체력")]
    public float maxHP = 300f;

    [Header("비주얼(선택)")]
    public Color tint = Color.white;
    public float scaleMultiplier = 1.5f; // 보스 크기 조절용
}