using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // TextMeshPro 필수

public class Player : MonoBehaviour
{
    [Header("게임 오버 UI")]
    public CanvasGroup gameOverUI;

    // [기존] 킬 수 텍스트
    public TextMeshProUGUI resultText;

    // [추가] 생존 시간 텍스트 변수
    public TextMeshProUGUI timeResultText;

    [Header("입력 및 속도")]
    public Vector2 inputVec;
    public float speed;
    public float speedMultiplier = 1f;
    public float runSpeedMultiplier = 1.5f;

    [Header("아군 소집 대형 설정")]
    public float formationRadius = 3.0f;

    Rigidbody2D rigid;
    SpriteRenderer spriter;
    Animator anim;
    Targetable targetable;
    private Coroutine callAlliesCoroutine;

    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        targetable = GetComponent<Targetable>();

        if (speed == 0) speed = 8;

        if (gameOverUI != null)
        {
            gameOverUI.alpha = 0f;
            gameOverUI.interactable = false;
            gameOverUI.blocksRaycasts = false;
            gameOverUI.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (targetable != null && targetable.isDead) return;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (callAlliesCoroutine != null) StopCoroutine(callAlliesCoroutine);
            callAlliesCoroutine = StartCoroutine(CallAlliesRoutine());
        }
    }

    public void TriggerGameOver()
    {
        Debug.Log("=== GAME OVER ===");

        if (rigid != null) rigid.linearVelocity = Vector2.zero;

        Time.timeScale = 0f;

        // [1] 킬 수 표시 (기존 코드)
        if (GameManager.instance != null && resultText != null)
        {
            // GameManager 변수명이 kill이 맞는지 확인하세요!
            resultText.text = "Score : " + GameManager.instance.kill;
        }

        // [2] 생존 시간 표시 (추가된 코드)
        if (GameManager.instance != null && timeResultText != null)
        {
            // GameManager에 시간이 'gameTime'이라는 변수로 있다고 가정합니다.
            float finalTime = GameManager.instance.gameTime;

            // 시간을 분:초로 계산
            int min = Mathf.FloorToInt(finalTime / 60);
            int sec = Mathf.FloorToInt(finalTime % 60);

            // "00:00" 형태로 텍스트 갱신
            timeResultText.text = $"Survive : {min:D2}:{sec:D2}";
        }

        if (gameOverUI != null)
        {
            gameOverUI.gameObject.SetActive(true);
            StartCoroutine(GameOverFadeEffect());
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    IEnumerator GameOverFadeEffect()
    {
        float duration = 2.0f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            gameOverUI.alpha = Mathf.Lerp(0f, 1f, timer / duration);
            yield return null;
        }

        gameOverUI.alpha = 1f;
        gameOverUI.interactable = true;
        gameOverUI.blocksRaycasts = true;
    }

    // --- 아래는 기존 이동/소집 코드와 동일 (생략 없음) ---

    void OnMove(InputValue value)
    {
        if (targetable != null && targetable.isDead)
        {
            inputVec = Vector2.zero;
            return;
        }
        inputVec = value.Get<Vector2>();
    }

    private void FixedUpdate()
    {
        if (targetable != null && targetable.isDead) return;

        float currentSpeed = speed * speedMultiplier;
        if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
        {
            currentSpeed *= runSpeedMultiplier;
        }

        Vector2 nextVec = inputVec * currentSpeed * Time.fixedDeltaTime;
        rigid.MovePosition(rigid.position + nextVec);
    }

    private void LateUpdate()
    {
        if (targetable != null && targetable.isDead) return;

        anim.SetFloat("Speed", inputVec.magnitude);
        if (inputVec.x != 0) spriter.flipX = inputVec.x < 0;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    Vector2 CalculateCircularPosition(int index, int totalCount, Vector2 center, float radius)
    {
        if (totalCount <= 1) return center;
        float angle = index * (360f / totalCount);
        float radian = angle * Mathf.Deg2Rad;
        return new Vector2(center.x + radius * Mathf.Cos(radian), center.y + radius * Mathf.Sin(radian));
    }

    IEnumerator CallAlliesRoutine()
    {
        if (targetable != null && targetable.isDead) yield break;

        List<AllyAI> alliesToCall = AllyAI.ActiveAllies != null ? new List<AllyAI>(AllyAI.ActiveAllies) : new List<AllyAI>();

        if (alliesToCall.Count > 0)
        {
            float range = 50f;
            float rangeSq = range * range;
            Vector2 playerPos = transform.position;
            List<AllyAI> targetAllies = new List<AllyAI>();

            foreach (var ally in alliesToCall)
            {
                if (ally != null && ally.gameObject.activeSelf)
                {
                    if (((Vector2)transform.position - (Vector2)ally.transform.position).sqrMagnitude <= rangeSq)
                        targetAllies.Add(ally);
                }
            }

            int count = 0;
            int yieldCounter = 0;
            int currentUnitIndex = 0;

            foreach (var ally in targetAllies)
            {
                UnitMover2D mover = ally.GetComponent<UnitMover2D>();
                if (mover != null)
                {
                    Vector2 finalTargetPos = CalculateCircularPosition(currentUnitIndex, targetAllies.Count, playerPos, formationRadius);
                    mover.SetMoveTarget(finalTargetPos);
                    currentUnitIndex++;
                    count++;
                    yieldCounter++;
                    if (yieldCounter >= 10)
                    {
                        yieldCounter = 0;
                        yield return null;
                    }
                }
            }
        }
        callAlliesCoroutine = null;
    }
}