using UnityEngine;

/// <summary>
/// [무기 매니저 - 공전 중심축] (플레이어의 자식 오브젝트에 부착)
/// [수정됨] 이 스크립트는 이제 '중심축(Pivot)' 역할을 합니다.
/// 자식으로 실제 무기(MeleeWeapon 프리팹)를 장착하고, 이 중심축 자체를 회전시켜
/// 무기가 플레이어 주위를 '공전(Orbit)'하게 만듭니다.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("무기 능력치")]
    public int level = 0;
    /// <summary>이 무기가 MeleeWeapon에게 전달할 기본 공격력</summary>
    public float damage = 1f;
    /// <summary>중심축의 회전 속도 (이것이 곧 '공전' 속도가 됨)</summary>
    public float rotationSpeed = -200f;
    /// <summary>공전 반경 (중심축으로부터 무기(자식)가 떨어져 있을 거리)</summary>
    public float orbitRadius = 1.6f;

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

        // 게임 시작 시 무기를 즉시 생성하고 '장착'합니다.
        SpawnAndEquipWeapon();
        level = 1;
    }

    /// <summary>
    /// [Unity 이벤트] Update() - 매 프레임 호출
    /// </summary>
    void Update()
    {
        // 이 오브젝트(중심축) 자체를 Z축(Vector3.forward) 기준으로 회전시킵니다.
        // (Time.deltaTime을 곱해 프레임 속도에 관계없이 일정한 속도로 회전)
        // -> 자식으로 붙어있는 무기(MeleeWeapon)도 함께 '공전'하게 됩니다.
        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// PoolManager에서 실제 무기 프리팹을 가져와 '자식'으로 장착합니다.
    /// (Start 함수에서 호출됩니다.)
    /// </summary>
    void SpawnAndEquipWeapon()
    {
        if (poolManager == null)
        {
            Debug.LogError("Weapon.cs: PoolManager를 찾을 수 없습니다!");
            return;
        }

        // 1. 풀(Pool)에서 'weaponPrefabIndex'번의 무기(예: 칼) 오브젝트를 가져옵니다.
        GameObject weaponObj = poolManager.Get(weaponPrefabIndex);
        if (weaponObj == null)
        {
            Debug.LogError("Weapon.cs: PoolManager에서 " + weaponPrefabIndex + "번 프리팹을 가져올 수 없습니다.");
            return;
        }

        // 2. [핵심] 가져온 무기 오브젝트를 '이 중심축(transform)'의 자식(child)으로 설정합니다.
        //    (이제부터 이 중심축이 회전하면, weaponObj도 따라 회전(공전)합니다.)
        weaponObj.transform.parent = this.transform;

        // 3. [핵심] 무기의 '로컬(Local)' 위치를 설정합니다.
        //    (부모(중심축)로부터 'orbitRadius'만큼 떨어진 곳(예: (1.5, 0, 0))에 배치)
        weaponObj.transform.localPosition = new Vector3(orbitRadius, 0, 0);
        // (참고) 무기 프리팹의 로컬 회전값(localPosition)도 0으로 초기화해주는 것이 좋습니다.
        weaponObj.transform.localRotation = Quaternion.Euler(0, 0, -90);
        weaponObj.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);

        // 4. 무기 오브젝트에 붙어있는 MeleeWeapon 스크립트를 찾아, '데미지' 값을 전달합니다.
        MeleeWeapon meleeWeapon = weaponObj.GetComponent<MeleeWeapon>();
        if (meleeWeapon != null)
        {
            // MeleeWeapon 스크립트의 SetDamage 함수를 호출하여,
            // 이 Weapon.cs가 가진 damage 값을 넘겨줍니다.
            meleeWeapon.SetDamage(damage);
        }
        else
        {
            Debug.LogWarning("Weapon.cs: 자식 무기 프리팹에 MeleeWeapon.cs 스크립트가 없습니다!");
        }
    }

    public int[] UpgradeDMG = { 0, 1, 3, 4, 5, 5 };
    public void LevelUp(int lvl)
    {
        level = lvl;
        damage=UpgradeDMG[lvl];
    }
}
