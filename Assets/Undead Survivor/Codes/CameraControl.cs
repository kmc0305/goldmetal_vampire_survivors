using UnityEngine;

public class LoLCamera : MonoBehaviour
{
    [Header("Target (Follow When Locked)")]
    public Transform followTarget;          // 플레이어, 챔피언 등

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.Space;
    public float panSpeed = 20f;
    public float edgeSize = 20f;

    [Header("State")]
    public bool isLocked = true;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        // 🔘 Space 키로 카메라 고정 on/off
        if (Input.GetKeyDown(toggleKey))
        {
            isLocked = !isLocked;
            Debug.Log("[LoLCamera] Toggle Lock. isLocked = " + isLocked);
        }

        if (isLocked)
        {
            // 🔒 고정 모드: 플레이어 따라감
            if (followTarget)
            {
                Vector3 p = transform.position;
                p.x = followTarget.position.x;
                p.y = followTarget.position.y;
                transform.position = p;
            }
        }
        else
        {
            // 🔓 해제 모드: 마우스 위치에 따라 카메라 이동
            EdgePan();
        }
    }

    void EdgePan()
    {
        Vector3 mouse = Input.mousePosition;
        Vector3 dir = Vector3.zero;

        if (mouse.x < edgeSize) dir.x = -1f;
        else if (mouse.x > Screen.width - edgeSize) dir.x = 1f;

        if (mouse.y < edgeSize) dir.y = -1f;
        else if (mouse.y > Screen.height - edgeSize) dir.y = 1f;

        if (dir.sqrMagnitude > 0.01f)
        {
            dir.Normalize();
            transform.position += dir * panSpeed * Time.deltaTime;
        }
    }
}
