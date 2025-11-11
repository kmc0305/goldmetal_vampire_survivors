using UnityEngine;
using Cinemachine; // Cinemachine(시네머신) 네임스페이스 추가 (필수)

/// <summary>
/// 'Y' 키로 카메라 잠금(Locked) / 잠금 해제(Unlocked)를 토글(Toggle)합니다.
/// 잠금이 해제되면 마우스 커서를 화면 가장자리로 이동시켜 맵을 스크롤(Scroll)할 수 있습니다.
/// </summary>
public class CameraLock : MonoBehaviour
{
    [Header("카메라 설정")]
    [Tooltip("씬(Scene)에 있는 Cinemachine 가상 카메라")]
    public CinemachineVirtualCamera virtualCamera; // 씬(Scene)의 가상 카메라

    [Header("잠금 해제(Unlocked) 시 설정")]
    [Tooltip("카메라 자유 이동 속도")]
    public float cameraMoveSpeed = 20f;
    [Tooltip("마우스가 화면 가장자리에 얼마나 가까워야 스크롤될지 (픽셀 단위)")]
    public float edgeScrollSize = 40f;

    private Transform playerTransform; // 플레이어의 Transform
    private bool isCameraLocked = true; // 카메라 잠금 상태 (기본값: 잠금)

    void Start()
    {
        // 1. GameManager(게임 매니저)에서 플레이어 정보를 가져옴
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            playerTransform = GameManager.instance.player.transform;
        }
        else
        {
            // [오류 수정 1] Debug가 'UnityEngine'의 Debug임을 명시
            UnityEngine.Debug.LogError("CameraLock.cs: GameManager(게임 매니저) 또는 Player(플레이어)를 찾을 수 없습니다!");
            return;
        }

        // 2. virtualCamera(가상 카메라)가 인스펙터(Inspector)에서 할당되지 않았다면, 씬(Scene)에서 직접 찾음
        if (virtualCamera == null)
        {
            // [경고 수정] 구식 FindObjectOfType 대신 최신 FindFirstObjectByType 사용
            virtualCamera = FindFirstObjectByType<CinemachineVirtualCamera>();
        }

        // 3. 게임 시작 시, 카메라를 플레이어에게 잠금
        LockCameraToPlayer();
    }

    void Update()
    {
        // 'Y' 키를 눌렀는지 감지
        if (Input.GetKeyDown(KeyCode.Y))
        {
            // 카메라 잠금 상태를 토글(Toggle)
            isCameraLocked = !isCameraLocked;

            if (isCameraLocked)
            {
                // [카메라 잠금]
                LockCameraToPlayer();
            }
            else
            {
                // [카메라 잠금 해제]
                UnlockCamera();
            }
        }

        // 카메라가 잠겨있지 않다면(Unlocked), 자유 이동 로직(Logic) 실행
        if (!isCameraLocked)
        {
            MoveCameraWithMouseEdge();
        }
    }

    /// <summary>
    /// 카메라를 플레이어에게 잠급니다.
    /// </summary>
    private void LockCameraToPlayer()
    {
        if (virtualCamera != null)
        {
            // Cinemachine(시네머신)의 'Follow(따라가기)' 타겟을 다시 플레이어로 설정
            virtualCamera.Follow = playerTransform;
        }
    }

    /// <summary>
    /// 플레이어에게서 카메라 잠금을 해제합니다.
    /// </summary>
    private void UnlockCamera()
    {
        if (virtualCamera != null)
        {
            // Cinemachine(시네머신)의 'Follow(따라가기)' 타겟을 null(없음)로 설정
            // 이렇게 하면 카메라는 그 자리에 멈춥니다.
            virtualCamera.Follow = null;
        }
    }

    /// <summary>
    /// 잠금 해제(Unlocked) 상태일 때, 마우스 커서 위치에 따라 카메라를 이동시킵니다.
    /// </summary>
    private void MoveCameraWithMouseEdge()
    {
        if (virtualCamera == null) return;

        // 이동 방향 초기화
        // [오류 수정 2] Vector3가 'UnityEngine'의 Vector3임을 명시
        UnityEngine.Vector3 moveInput = UnityEngine.Vector3.zero;

        // 마우스가 화면 가장자리에 있는지 확인
        if (Input.mousePosition.x < edgeScrollSize)
        {
            moveInput.x = -1; // 왼쪽
        }
        else if (Input.mousePosition.x > Screen.width - edgeScrollSize)
        {
            moveInput.x = +1; // 오른쪽
        }

        if (Input.mousePosition.y < edgeScrollSize)
        {
            moveInput.y = -1; // 아래
        }
        else if (Input.mousePosition.y > Screen.height - edgeScrollSize)
        {
            moveInput.y = +1; // 위
        }

        // 가상 카메라(Virtual Camera)의 Transform(트랜스폼)을 직접 이동시킴
        // Time.deltaTime을 곱해 프레임(Frame)에 독립적인 속도로 이동
        // (moveInput이 UnityEngine.Vector3이므로 이 줄은 자동으로 수정됩니다)
        virtualCamera.transform.Translate(moveInput * cameraMoveSpeed * Time.deltaTime, Space.World);
    }
}