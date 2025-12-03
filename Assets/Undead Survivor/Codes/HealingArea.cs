using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [힐 장판 로직] 장판 영역 내에 있는 대상의 체력을 주기적으로 회복시킵니다.
/// 이 스크립트가 붙은 오브젝트에는 반드시 isTrigger가 활성화된 Collider2D가 있어야 합니다.
/// </summary>
public class HealingArea : MonoBehaviour
{
    [Header("회복 설정")]
    [Tooltip("초당 회복되는 체력량")]
    public float healPerSecond = 5f;
    [Tooltip("체력 회복 효과가 적용될 대상 레이어")]
    public LayerMask targetLayer; // Player 또는 Ally 레이어 설정

    [Header("작동 주기")]
    private const float HEAL_INTERVAL = 0.5f; // 0.5초마다 회복 적용
    private float healTimer = 0f;

    [Header("수명 설정")]
    [Tooltip("장판이 자동으로 사라질 시간 (0 또는 음수면 영구 지속)")]
    public float lifetime = 60f; // 기본 60초 (1분)

    [Header("시각 효과 설정")]
    [Tooltip("장판이 켜져 있을 때 주기적으로 깜빡이는 효과를 줍니다.")]
    public Color activeColor = new Color(0.1f, 1f, 0.1f, 0.5f); // 활성화 시 빛날 색상 (연한 녹색)
    public float flashSpeed = 2f; // 깜빡이는 속도

    // 내부 변수: 범위 내에 있는 Targetable 컴포넌트 목록
    private readonly List<Targetable> targetsInArea = new List<Targetable>();
    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            // 동상 프리팹 자체에 SpriteRenderer가 없으면 자식에서 찾음
            sr = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Start()
    {
        // 시각 효과 코루틴 시작
        if (sr != null)
        {
            StartCoroutine(FlashRoutine(sr.color));
        }

        // 수명 설정이 되어 있다면, 자동 파괴 코루틴 시작
        if (lifetime > 0)
        {
            StartCoroutine(AutoDestroyRoutine());
        }
    }

    // --- 장판 활성화 깜빡임 효과 (Rune Glow) ---
    private IEnumerator FlashRoutine(Color baseColor)
    {
        float t = 0f;
        while (true)
        {
            if (sr == null) break;

            t += Time.deltaTime * flashSpeed;
            float alpha = 0.5f + Mathf.PingPong(t, 0.5f); // 0.5 ~ 1.0 사이로 반복

            // 기본 색상과 활성화 색상을 알파값에 따라 섞습니다.
            sr.color = Color.Lerp(baseColor, activeColor, alpha);

            yield return null;
        }
    }


    // --- 대상 진입 감지 (Enter Logic) ---
    private void OnTriggerEnter2D(Collider2D other)
    {
        // [디버그] 트리거 감지 확인
        Debug.Log($"[HealingArea] OnTriggerEnter2D: {other.name}, Layer: {other.gameObject.layer}, targetLayer: {targetLayer.value}");

        // 1. 레이어 마스크 체크: 설정된 대상(Player/Ally)만 처리
        if (((1 << other.gameObject.layer) & targetLayer) == 0)
        {
            Debug.Log($"[HealingArea] Layer mismatch - skipping {other.name}");
            return;
        }

        Targetable target = other.GetComponent<Targetable>();

        // 2. 유효성 체크: Targetable이 있고, 죽지 않았고, 리스트에 없을 때만 추가
        if (target != null && !target.isDead && !targetsInArea.Contains(target))
        {
            targetsInArea.Add(target);
            Debug.Log($"[HealingArea] Added {other.name} to healing list. Total: {targetsInArea.Count}");
        }
    }

    // --- 대상 이탈 감지 (Exit Logic) ---
    private void OnTriggerExit2D(Collider2D other)
    {
        Targetable target = other.GetComponent<Targetable>();
        if (target != null)
        {
            targetsInArea.Remove(target);

            // ★ 수정된 부분: 유닛이 범위를 벗어나는 즉시 힐 플래시를 중지합니다.
            target.StopHealFlashAndResetColor();
        }
    }

    // --- 주기적인 체력 회복 적용 ---
    private void Update()
    {
        healTimer += Time.deltaTime;

        if (healTimer >= HEAL_INTERVAL)
        {
            healTimer = 0f;
            ApplyHealing();
        }
    }

    /// <summary>
    /// 범위 내 모든 대상에게 체력 회복을 적용합니다.
    /// </summary>
    void ApplyHealing()
    {
        // 1회당 회복량 계산
        float healAmount = healPerSecond * HEAL_INTERVAL;

        // [디버그] 힐링 적용 확인
        if (targetsInArea.Count > 0)
        {
            Debug.Log($"[HealingArea] ApplyHealing to {targetsInArea.Count} targets, amount: {healAmount}");
        }

        // 역방향 순회: 대상이 리스트에 있는 동안만 힐을 적용
        for (int i = targetsInArea.Count - 1; i >= 0; i--)
        {
            Targetable target = targetsInArea[i];

            // 대상이 유효하고, 죽지 않았을 때만 회복 적용
            if (target != null && !target.isDead)
            {
                target.Heal(healAmount);
                Debug.Log($"[HealingArea] Healed {target.name}: {target.currentHealth}/{target.maxHealth}");
            }
            else
            {
                // 대상이 파괴되거나 비활성화된 경우 리스트에서 제거
                targetsInArea.RemoveAt(i);
            }
        }
    }
    // (AutoDestroyRoutine 코루틴 생략)
    private IEnumerator AutoDestroyRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (gameObject != null)
        {
            UnityEngine.Object.Destroy(gameObject);
            UnityEngine.Debug.Log($"Healing Area expired and destroyed: {gameObject.name}");
        }
    }
}