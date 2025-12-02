using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections; // [추가] 코루틴 사용을 위해 추가

/// <summary>
/// 플레이어의 입력을 받고, 이동 및 애니메이션을 처리합니다.
/// [기능 추가]: 스페이스바 입력 시 아군 집결(CallAllies) - 반경 50m 이내, 타워 회피 적용
/// </summary>
public class Player : MonoBehaviour
{
    [Header("입력 및 속도")]
    public UnityEngine.Vector2 inputVec;
    public float speed;

    // [추가 시작] 늪지대 기능을 위한 변수
    [Header("속도 제어")]
    // 이 배율은 늪지대와 같은 외부 환경 요소에 의해 조절됩니다.
    public float speedMultiplier = 1f;
    // [추가 끝]

    [Header("달리기 설정")]
    public float runSpeedMultiplier = 1.5f;

    UnityEngine.Rigidbody2D rigid;
    UnityEngine.SpriteRenderer spriter;
    UnityEngine.Animator anim;

    // [추가] 유닛 소집 코루틴 참조
    private Coroutine callAlliesCoroutine;

    private void Awake()
    {
        rigid = GetComponent<UnityEngine.Rigidbody2D>();
        spriter = GetComponent<UnityEngine.SpriteRenderer>();
        anim = GetComponent<UnityEngine.Animator>();

        if (speed == 0) speed = 8;
    }

    private void Update()
    {
        // 키보드가 연결되어 있고 스페이스바를 눌렀을 때
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // [수정] 코루틴을 통해 명령을 분산 실행
            if (callAlliesCoroutine != null)
            {
                StopCoroutine(callAlliesCoroutine);
            }
            callAlliesCoroutine = StartCoroutine(CallAlliesRoutine());
        }
    }

    /// <summary>
    /// [수정됨] 유닛 소집 명령을 여러 프레임에 걸쳐 분산 처리합니다.
    /// </summary>
    IEnumerator CallAlliesRoutine()
    {
        // AllyAI.ActiveAllies 리스트를 그대로 사용하면 반복문 중에 리스트가 변경될 위험이 있으므로,
        // 현재 활성화된 아군 목록을 복사본으로 만듭니다.
        List<AllyAI> alliesToCall = AllyAI.ActiveAllies != null ?
                                    new List<AllyAI>(AllyAI.ActiveAllies) :
                                    new List<AllyAI>();

        if (alliesToCall.Count > 0)
        {
            float range = 50f;
            float rangeSq = range * range;
            UnityEngine.Vector2 playerPos = transform.position;
            int count = 0;
            int yieldCounter = 0;
            int yieldInterval = 10; // 10기마다 한 프레임 대기

            foreach (var ally in alliesToCall)
            {
                // 유닛이 파괴되었을 수도 있으므로 null 체크
                if (ally != null && ally.gameObject.activeSelf)
                {
                    float distSq = (playerPos - (UnityEngine.Vector2)ally.transform.position).sqrMagnitude;

                    if (distSq <= rangeSq)
                    {
                        UnitMover2D mover = ally.GetComponent<UnitMover2D>();

                        if (mover != null)
                        {
                            mover.SetMoveTarget(playerPos);
                            count++;

                            // [핵심 최적화] 일정 유닛 처리 후 한 프레임(yield return null) 대기
                            yieldCounter++;
                            if (yieldCounter >= yieldInterval)
                            {
                                yieldCounter = 0;
                                yield return null;
                            }
                        }
                    }
                }
            }
            UnityEngine.Debug.Log($"🚩 아군 집결 명령! (반경 50 내 {count}기 호출, {alliesToCall.Count}기 순회)");
        }
        else
        {
            UnityEngine.Debug.Log("🚩 소집할 아군 유닛이 없습니다.");
        }
        callAlliesCoroutine = null;
    }


    void OnMove(UnityEngine.InputSystem.InputValue value)
    {
        inputVec = value.Get<UnityEngine.Vector2>();
    }

    private void FixedUpdate()
    {
        // [수정] 기본 속도에 외부 환경 속도 배율을 곱하여 현재 속도 계산
        float currentSpeed = speed * speedMultiplier;

        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed)
        {
            currentSpeed *= runSpeedMultiplier;
        }

        UnityEngine.Vector2 nextVec = inputVec * currentSpeed * UnityEngine.Time.fixedDeltaTime;
        rigid.MovePosition(rigid.position + nextVec);
    }

    /// <summary>
    /// 외부 요인에 의한 이동 속도 배율을 설정합니다.
    /// </summary>
    /// <param name="multiplier">1.0f = 정상, 0.5f = 50% 둔화</param>
    public void SetSpeedMultiplier(float multiplier)
    {
        // 기존의 임시 둔화 효과와는 별도로, 늪지대와 같은 영구적인 영역 효과를 담당합니다.
        speedMultiplier = multiplier;
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