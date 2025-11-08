using UnityEngine;

[CreateAssetMenu(fileName = "BossSpec", menuName = "Game/Boss Spec")]
public class BossSpec : ScriptableObject
{
    [Header("공격")]
    public bool isAreaAttack = false; // 범위공격 여부
    public float areaRadius = 3.0f;  // 범위공격 반경
    public float attackDamage = 12f;
    public float attackCooldown = 1.0f;

    [Header("탐지/이동")]
    public float detectionRadius = 15f;
    public float moveSpeed = 2.0f;

    [Header("체력")]
    public float maxHP = 300f;

    [Header("비주얼(선택)")]
    public Color tint = Color.white;
}
