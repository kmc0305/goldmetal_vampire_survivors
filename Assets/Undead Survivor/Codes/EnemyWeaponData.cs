using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyWeapon", menuName = "Game/Enemy Weapon Data")]
public class EnemyWeaponData : ScriptableObject
{
    [Header("비주얼")]
    public Sprite bulletSprite; // 탄환 이미지 (나중에 추가/변경 가능)
    public Color bulletColor = Color.white; // 탄환 색상

    [Header("전투 속성")]
    public float attackRange = 5f;    // 사거리 (이 거리 안에서 멈춤)
    public float damage = 5f;         // 데미지
    public float cooldown = 2f;       // 공격 속도
    public float bulletSpeed = 6f;    // 탄환 속도
}