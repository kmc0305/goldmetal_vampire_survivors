using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 플레이어의 입력을 받고, 이동 및 애니메이션을 처리합니다.
/// [기능 추가]: 스페이스바 입력 시 아군 집결(CallAllies) - 반경 50m 이내, 타워 회피 적용 및 Formation 적용
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

    [Header("아군 소집 대형 설정")]
    public float formationRadius = 3.0f; // 플레이어를 중심으로 유닛들이 서는 원형 반경

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
    /// 지정된 인덱스와 총 개수에 따라 원형 대형 위치를 계산합니다.
    /// </summary>
    /// <param name="index">현재 유닛의 순서</param>
    /// <param name="totalCount">총 유닛 수</param>
    /// <param name="center">대형의 중심 위치 (플레이어 위치)</param>
    /// <param name="radius">대형의 반경</param>
    /// <returns>유닛이 서야 할 월드 위치</returns>
    UnityEngine.Vector2 CalculateCircularPosition(int index, int totalCount, UnityEngine.Vector2 center, float radius)
    {
        if (totalCount <= 1)
        {
            // 유닛이 없거나 하나뿐이면 중심 위치 그대로 반환
            return center;
        }

        // 원형 대형: 360도를 총 유닛 수로 나눈 각도 계산
        float angle = index * (360f / totalCount);

        // 유니티는 Radian을 사용하므로 Degress를 Radian으로 변환
        float radian = angle * Mathf.Deg2Rad;

        // 원형 위치 계산
        float x = center.x + radius * Mathf.Cos(radian);
        float y = center.y + radius * Mathf.Sin(radian);

        return new UnityEngine.Vector2(x, y);
    }

    /// <summary>
    /// [수정됨] 유닛 소집 명령을 여러 프레임에 걸쳐 분산 처리하고, Formation을 적용합니다.
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

            // [추가] 소집 대상 유닛을 미리 파악하여 리스트에 담고 총 개수를 구합니다.
            List<AllyAI> targetAllies = new List<AllyAI>();
            foreach (var ally in alliesToCall)
            {
                if (ally != null && ally.gameObject.activeSelf)
                {
                    float distSq = (playerPos - (UnityEngine.Vector2)ally.transform.position).sqrMagnitude;
                    if (distSq <= rangeSq)
                    {
                        targetAllies.Add(ally);
                    }
                }
            }

            int count = 0;
            int yieldCounter = 0;
            int yieldInterval = 10; // 10기마다 한 프레임 대기
            int totalTargetCount = targetAllies.Count;
            int currentUnitIndex = 0; // 대형 계산을 위한 인덱스


            foreach (var ally in targetAllies)
            {
                UnitMover2D mover = ally.GetComponent<UnitMover2D>();

                if (mover != null)
                {
                    // [핵심 수정] CalculateCircularPosition 함수를 이용해 대형 위치 계산
                    UnityEngine.Vector2 finalTargetPos = CalculateCircularPosition(
                        currentUnitIndex,
                        totalTargetCount,
                        playerPos,
                        formationRadius
                    );

                    // UnitMover2D의 SetMoveTarget을 사용 (타워 회피 및 충돌 무시 로직 포함)
                    mover.SetMoveTarget(finalTargetPos);

                    currentUnitIndex++; // 다음 유닛 인덱스 증가
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
            UnityEngine.Debug.Log($"🚩 아군 집결 명령! (반경 50 내 {count}기 호출, {alliesToCall.Count}기 순회, 대형 적용)");
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