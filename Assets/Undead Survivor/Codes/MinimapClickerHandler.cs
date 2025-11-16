using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 미니맵 RawImage 위에서 우클릭하면
/// 그 위치의 월드 좌표를 계산해서 RTSSelection으로 이동 명령을 보낸다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(RawImage))]
public class MinimapClickHandler : MonoBehaviour, IPointerDownHandler
{
    [Header("References")]
    public Camera minimapCamera;   // MinimapCamera 드래그
    public RTSSelection rts;       // RTSSelection 붙어있는 오브젝트 드래그

    private RectTransform rect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    // UI 위에서 마우스 버튼이 눌렸을 때 호출
    public void OnPointerDown(PointerEventData eventData)
    {
        // ❗ 오른쪽 클릭만 처리
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (minimapCamera == null || rts == null)
            return;

        // 1) 이 RawImage 안에서의 로컬 좌표 (-w/2 ~ +w/2, -h/2 ~ +h/2)
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                eventData.position,
                eventData.pressEventCamera,   // Screen Space - Overlay면 null이어도 동작
                out local))
            return;

        // 2) 로컬 좌표를 0~1 UV(Viewport) 좌표로 변환
        Vector2 size = rect.rect.size;
        float u = (local.x / size.x) + 0.5f;  // 0 ~ 1
        float v = (local.y / size.y) + 0.5f;  // 0 ~ 1

        // 3) MinimapCamera 의 Viewport(0~1) 기준으로 월드 좌표 얻기
        //    Orthographic 이라 z에 "카메라가 월드를 보는 거리" 넣어주면 됨
        float z = -minimapCamera.transform.position.z;
        Vector3 vp = new Vector3(u, v, z);
        Vector3 world = minimapCamera.ViewportToWorldPoint(vp);
        world.z = 0f;  // 2D 월드는 z=0 고정

        // 4) RTSSelection 에 이동 명령 전달
        rts.IssueMoveCommand(world);
        if (rts.clearAfterRightClick)  //선택 해제
            rts.ClearSelection();
    }
}
