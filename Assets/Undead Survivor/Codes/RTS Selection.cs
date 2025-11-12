using System.Collections.Generic;
using UnityEngine;

public class RTSSelection : MonoBehaviour
{
    [Header("Filters")]
    public LayerMask selectableLayers;
    public KeyCode addMultiKey = KeyCode.LeftShift;

    [Header("Box UI")]
    public Color boxColor = new Color(0f, 0.7f, 1f, 0.2f);
    public Color boxBorder = new Color(0f, 0.7f, 1f, 0.9f);

    [Header("Behavior")]
    public bool clearAfterRightClick = true;

    private Texture2D tex;
    private Vector2 dragStart;
    private bool dragging;
    private readonly List<Selectable> selected = new();
    private Camera cam;

    void Awake()
    {
        cam = Camera.main;
        tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) { dragging = true; dragStart = Input.mousePosition; }

        if (Input.GetMouseButtonUp(0))
        {
            if (!dragging) return;
            dragging = false;

            Rect screenRect = GetScreenRect(dragStart, Input.mousePosition);
            if (!Input.GetKey(addMultiKey)) ClearSelection();

            Selectable[] all = Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);
            foreach (var s in all)
            {
                if (!s) continue;
                if (((1 << s.gameObject.layer) & selectableLayers) == 0) continue;

                Vector3 sp = cam.WorldToScreenPoint(s.transform.position);
                if (screenRect.Contains(sp, true))
                    AddToSelection(s);
            }

            // 작은 클릭일 경우 단일 선택
            if (screenRect.size.magnitude < 4f)
            {
                var hit = Physics2D.OverlapPoint(cam.ScreenToWorldPoint(Input.mousePosition), selectableLayers);
                if (hit)
                {
                    var s = hit.GetComponentInParent<Selectable>();
                    if (s) AddToSelection(s);
                }
            }
        }

        // 우클릭 이동
        if (Input.GetMouseButtonDown(1) && selected.Count > 0)
        {
            Vector3 wp = cam.ScreenToWorldPoint(Input.mousePosition);
            wp.z = 0f;
            IssueMoveCommand(wp);
            if (clearAfterRightClick) ClearSelection();
        }
    }

    void OnGUI()
    {
        if (!dragging) return;
        Rect guiRect = GetGUIRect(dragStart, Input.mousePosition);
        DrawScreenRect(guiRect, boxColor);
        DrawScreenRectBorder(guiRect, 2f, boxBorder);
    }

    // --- 명령/선택 로직 ---
    void AddToSelection(Selectable s) { if (!selected.Contains(s)) { selected.Add(s); s.SetSelected(true); } }
    void ClearSelection() { foreach (var s in selected) if (s) s.SetSelected(false); selected.Clear(); }

    void IssueMoveCommand(Vector3 dest)
    {
        int count = selected.Count;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        float spacing = 0.8f;

        int row = 0, col = 0;
        Vector2 origin = dest;
        for (int i = 0; i < count; i++)
        {
            var s = selected[i];
            if (!s) continue;

            Vector2 offset = new((col - (cols - 1) / 2f) * spacing,
                                 (-(row) + (cols - 1) / 2f) * spacing);
            Vector2 target = origin + offset;

            var mover = s.GetComponent<UnitMover2D>();
            if (mover) mover.SetMoveTarget(target);

            col++;
            if (col >= cols) { col = 0; row++; }
        }
    }

    // --- 드래그 박스 유틸 ---
    Rect GetScreenRect(Vector2 a, Vector2 b)
    {
        var min = Vector2.Min(a, b);
        var max = Vector2.Max(a, b);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    Rect GetGUIRect(Vector2 a, Vector2 b)
    {
        Rect r = GetScreenRect(a, b);
        r.y = Screen.height - r.y - r.height;
        return r;
    }

    void DrawScreenRect(Rect rect, Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, tex);
        GUI.color = old;
    }

    void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }
}