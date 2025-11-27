using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("입력 및 속도")]
    // ★ 수정: 모호함 방지를 위해 UnityEngine.Vector2로 명시
    public UnityEngine.Vector2 inputVec;
    public float speed;

    [Header("달리기 설정")]
    [Tooltip("Shift 키를 누를 때 적용될 속도 배수 (예: 1.5배)")]
    public float runSpeedMultiplier = 1.5f;

    Rigidbody2D rigid;
    SpriteRenderer spriter;
    Animator anim;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        speed = 8;
    }

    void OnMove(InputValue value)
    {
        // ★ 수정: 여기도 UnityEngine.Vector2로 명시
        inputVec = value.Get<UnityEngine.Vector2>();
    }

    private void FixedUpdate()
    {
        float currentSpeed = speed;

        // Shift 키 입력 감지 (Keyboard.current가 null이 아닐 때만 체크)
        if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
        {
            currentSpeed *= runSpeedMultiplier;
        }

        // ★ 수정: 여기도 UnityEngine.Vector2로 명시
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