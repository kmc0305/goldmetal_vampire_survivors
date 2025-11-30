using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 입력을 받고, 이동 및 애니메이션을 처리합니다.
/// [기능 추가]: 스페이스바 입력 시 아군 집결(CallAllies) - 반경 50m 이내, 타워 회피 적용
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

    private void Update()
    {
        // 키보드가 연결되어 있고 스페이스바를 눌렀을 때
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CallAllies();
        }
    }

    // ✅ [수정] 반경 50 이내 아군에게 UnitMover2D를 통해 이동 명령 (장애물 회피 포함)
    void CallAllies()
    {
        if (AllyAI.ActiveAllies != null)
        {
            float range = 50f;
            float rangeSq = range * range; // 최적화를 위해 거리 제곱값 미리 계산
            Vector2 playerPos = transform.position;

            int count = 0;

            foreach (var ally in AllyAI.ActiveAllies)
            {
                if (ally != null && ally.gameObject.activeSelf)
                {
                    // 1. 거리 체크 (현재 위치와 아군 위치 사이의 거리 제곱 비교)
                    // Vector2로 캐스팅하여 Z축 높이 차이로 인한 오차 방지
                    float distSq = (playerPos - (Vector2)ally.transform.position).sqrMagnitude;

                    if (distSq <= rangeSq)
                    {
                        // 2. UnitMover2D 컴포넌트 가져오기
                        // (AllyAI와 같은 오브젝트에 붙어있다고 가정)
                        UnitMover2D mover = ally.GetComponent<UnitMover2D>();

                        if (mover != null)
                        {
                            // 3. 타워 회피 알고리즘이 포함된 SetMoveTarget 호출
                            mover.SetMoveTarget(playerPos);
                            count++;
                        }
                    }
                }
            }
            UnityEngine.Debug.Log($"🚩 아군 집결 명령! (반경 50 내 {count}기 호출)");
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