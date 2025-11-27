using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 입력을 받고, 이동 및 애니메이션을 처리합니다.
/// [기능 추가]: 스페이스바 입력 시 아군 집결(CallAllies)
/// [수정]: Debug 모호함 해결
/// </summary>
public class Player : MonoBehaviour
{
    [Header("입력 및 속도")]
    public UnityEngine.Vector2 inputVec;
    public float speed;

    [Header("달리기 설정")]
    public float runSpeedMultiplier = 1.5f;

    Rigidbody2D rigid;
    SpriteRenderer spriter;
    Animator anim;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        if (speed == 0) speed = 8;
    }

    // ✅ [추가] 스페이스바 입력을 감지하여 아군 호출
    private void Update()
    {
        // 키보드가 연결되어 있고 스페이스바를 눌렀을 때
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CallAllies();
        }
    }

    // ✅ [추가] 모든 아군에게 현재 내 위치로 오라고 명령
    void CallAllies()
    {
        // AllyAI의 정적 리스트를 순회하며 명령 전달 (매우 빠름)
        if (AllyAI.ActiveAllies != null)
        {
            foreach (var ally in AllyAI.ActiveAllies)
            {
                if (ally != null && ally.gameObject.activeSelf)
                {
                    ally.CommandMoveTo(transform.position);
                }
            }
            // System.Diagnostics와의 충돌 방지를 위해 UnityEngine.Debug 명시
            UnityEngine.Debug.Log("🚩 아군 집결 명령 (Recall)!");
        }
    }

    void OnMove(InputValue value)
    {
        inputVec = value.Get<UnityEngine.Vector2>();
    }

    private void FixedUpdate()
    {
        float currentSpeed = speed;

        if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
        {
            currentSpeed *= runSpeedMultiplier;
        }

        UnityEngine.Vector2 nextVec = inputVec * currentSpeed * Time.fixedDeltaTime;
        rigid.MovePosition(rigid.position + nextVec);
    }

    private void LateUpdate()
    {
        anim.SetFloat("Speed", inputVec.magnitude);

        if (inputVec.x != 0)
        {
            spriter.flipX = inputVec.x < 0;
        }
    }
}