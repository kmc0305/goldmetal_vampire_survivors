using UnityEngine;

/// <summary>
/// 타겟을 따라가되,
/// 지정한 맵 크기(정사각형) 안에서만 카메라가 움직이도록 제한.
/// Cinemachine Virtual Camera 또는 일반 카메라에 붙여서 사용.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraClampByMapSize : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform target;         // 플레이어 / 중심 유닛

    [Header("Map Settings")]
    public Vector2 mapCenter = Vector2.zero;  // 맵 중심 (기본: (0,0))
    public float mapSize = 100f;             // 맵 한 변 길이 (정사각형)

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1) 우선 타겟을 따라가려는 위치
        Vector3 desired = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z   // z는 카메라 원래 값 유지
        );

        // 2) 맵 반경 (절반 길이)
        float halfMap = mapSize * 0.5f;

        // 3) 카메라 화면 절반 크기 (Pixel Perfect 포함 실제 값)
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth = camHalfHeight * cam.aspect;

        // 만약 카메라가 맵보다 크면 → 그냥 맵 중심 고정
        if (camHalfWidth >= halfMap || camHalfHeight >= halfMap)
        {
            transform.position = new Vector3(
                mapCenter.x,
                mapCenter.y,
                desired.z
            );
            return;
        }

        // 4) 카메라 중심이 움직일 수 있는 최소/최대 x,y 계산
        float minX = mapCenter.x - (halfMap - camHalfWidth);
        float maxX = mapCenter.x + (halfMap - camHalfWidth);
        float minY = mapCenter.y - (halfMap - camHalfHeight);
        float maxY = mapCenter.y + (halfMap - camHalfHeight);

        // 5) 그 범위 안으로 Clamp
        float clampedX = Mathf.Clamp(desired.x, minX, maxX);
        float clampedY = Mathf.Clamp(desired.y, minY, maxY);

        transform.position = new Vector3(clampedX, clampedY, desired.z);
    }
}
