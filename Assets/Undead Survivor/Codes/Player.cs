using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    [Header("게임 오버 UI")]
    public CanvasGroup gameOverUI;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI timeResultText;

    [Header("게임 오버 연출 설정")]
    public Camera mainCamera;
    public GameObject virtualCamera;
    public float slowMotionScale = 0.2f;
    public float zoomSize = 3.5f;
    public float deathSequenceDuration = 2.0f;

    [Header("입력 및 속도")]
    public Vector2 inputVec;
    public float speed;
    public float speedMultiplier = 1f;
    public float runSpeedMultiplier = 1.5f;
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

        if (mainCamera == null) mainCamera = Camera.main;
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
        if (gameOverUI.gameObject.activeSelf) return;
        Debug.Log("=== GAME OVER SEQUENCE START ===");
        StartCoroutine(GameOverSequence());
    }

    IEnumerator GameOverSequence()
    {
        // 1. 물리 정지
        if (rigid != null) rigid.linearVelocity = Vector2.zero;

        // 2. 방해꾼 끄기 (시네머신, 픽셀 퍼펙트 등)
        if (virtualCamera != null) virtualCamera.SetActive(false);
        if (mainCamera != null)
        {
            var pixelCam = mainCamera.GetComponent<PixelPerfectCamera>();
            if (pixelCam != null) pixelCam.enabled = false;
            var cineBrain = mainCamera.GetComponent<CinemachineBrain>();
            if (cineBrain != null) cineBrain.enabled = false;
        }

        // ====================================================
        // ★ [핵심] 사망 애니메이션 실행 코드
        // ====================================================
        if (anim != null)
        {
            // 아까 1단계에서 만든 Trigger 이름이 "Dead"여야 합니다.
            anim.SetTrigger("Dead");
            Debug.Log("💀 사망 애니메이션 실행 명령 보냄!");
        }
        // ====================================================

        // 3. 슬로우 모션 시작
        Time.timeScale = slowMotionScale;

        float timer = 0f;
        float startSize = 5f;
        if (mainCamera != null) startSize = mainCamera.orthographicSize;

        // 4. 줌인 연출
        while (timer < deathSequenceDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / deathSequenceDuration;

            if (mainCamera != null)
            {
                mainCamera.orthographicSize = Mathf.Lerp(startSize, zoomSize, t);
                Vector3 targetPos = transform.position;
                targetPos.z = -10f;
                mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPos, t * 0.1f);
            }
            yield return null;
        }

        // 5. 완전 정지
        Time.timeScale = 0f;
        if (mainCamera != null) mainCamera.orthographicSize = zoomSize;

        UpdateGameOverTexts();

        // 6. UI 등장
        if (gameOverUI != null)
        {
            gameOverUI.gameObject.SetActive(true);
            yield return StartCoroutine(GameOverFadeEffect());
        }
    }

    void UpdateGameOverTexts()
    {
        if (GameManager.instance != null)
        {
            if (resultText != null)
                resultText.text = "Score : " + GameManager.instance.kill;

            if (timeResultText != null)
            {
                float finalTime = GameManager.instance.gameTime;
                int min = Mathf.FloorToInt(finalTime / 60);
                int sec = Mathf.FloorToInt(finalTime % 60);
                timeResultText.text = $"Survive Time : {min:D2}:{sec:D2}";
            }
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    IEnumerator GameOverFadeEffect()
    {
        float duration = 1.0f;
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

    // --- 이동 및 물리 로직 ---

    void OnMove(InputValue value)
    {
        if (targetable != null && targetable.isDead) { inputVec = Vector2.zero; return; }
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

    // --- 아군 소집 로직 (CallAllies) ---

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