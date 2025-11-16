using UnityEngine;
using System.Collections;

/// <summary>
/// [근접 무기 - 히트박스] (실제 무기 프리팹에 부착)
/// [수정됨] 이 스크립트는 부모(Weapon.cs - 중심축)에게 붙어 '공전(Orbit)'하면서,
/// 적과 부딪혔을 때 '데미지 판정'만 담당합니다.
/// 
/// [중요 설정]
/// 이 오브젝트의 Collider2D는 반드시 'Is Trigger'가 체크(✔)되어 있어야 합니다.
/// (물리적으로 부딪히는 게 아니라, '통과'하면서 감지해야 하므로)
/// </summary>
public class MeleeWeapon : MonoBehaviour
{
    /// <summary>Weapon.cs(부모)로부터 실시간으로 받아올 현재 데미지 값</summary>
    private float currentDamage = 2f;

    /// <summary>
    /// 부모인 Weapon.cs가 이 함수를 호출하여 무기의 데미지를 설정해 줍니다.
    /// (예: 플레이어가 레벨업하면 Weapon.cs가 이 값을 더 높게 설정할 수 있습니다.)
    /// </summary>
    public void SetDamage(float damage)
    {
        currentDamage = damage;
    }

    /// <summary>
    /// [핵심] 공격 판정 (Trigger)
    /// 이 무기의 Collider2D (IsTrigger=true)에 다른 Collider가 '들어왔을(Enter)' 때 1회 호출됩니다.
    /// </summary>
    /// <param name="other">나(무기)와 부딪힌 대상의 Collider2D 정보</param>
    void OnTriggerEnter2D(Collider2D other)
    {
        // 1. 부딪힌 대상이 Targetable 스크립트를 가지고 있는지 확인합니다.
        Targetable target = other.GetComponent<Targetable>();

        // 2. Targetable이 없으면 (예: 벽, 아이템 등) 무시하고 즉시 함수 종료
        if (target == null) return;

        // 3. 부딪힌 대상이 'Enemy' 진영인지 확인합니다.
        //    (아군(Ally)이나 플레이어를 때리면 안 되므로)
        if (target.faction == Targetable.Faction.Enemy)
        {
            // 4. [핵심] 적('Enemy')의 Targetable 스크립트에 TakeDamage() 함수를 호출합니다.
            //    넉백 방향 계산을 위해 '나(무기)'의 위치(transform)를 넘겨줍니다.
            target.TakeDamage(currentDamage, transform);
            Weapon wpn = GetComponentInParent<Weapon>();
        }
    }
}
