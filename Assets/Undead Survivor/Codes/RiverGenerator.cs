using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [맵 생성기] 거대한 강 (River)
/// 맵을 가로지르는 랜덤한 일차함수 형태의 강을 생성합니다.
/// 물리적인 벽(EdgeCollider)과 시각적인 물(LineRenderer)을 포함합니다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class RiverGenerator : MonoBehaviour
{
    [Header("강 설정")]
    public float riverWidth = 6f;        // 강의 폭
    public int segments = 100;           // 강의 굴곡 디테일 (점 개수)
    public float noiseScale = 0.1f;      // 굽이치는 정도 (펄린 노이즈)
    public float noiseAmount = 5f;       // 굴곡의 세기

    [Header("맵 범위")]
    public UnityEngine.Vector2 mapSize = new UnityEngine.Vector2(100f, 100f);

    [Header("물리 설정")]
    // ★ 중요: 유닛이 강에 닿았을 때 미끄러지게 할 재질 (Friction = 0)
    public PhysicsMaterial2D riverPhysicsMaterial;

    private LineRenderer lineRenderer;
    private EdgeCollider2D edgeColTop;    // 위쪽 강둑
    private EdgeCollider2D edgeColBottom; // 아래쪽 강둑

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        // 라인 렌더러 기본 설정
        lineRenderer.positionCount = segments;
        lineRenderer.startWidth = riverWidth;
        lineRenderer.endWidth = riverWidth;
        lineRenderer.useWorldSpace = true;

        // 콜라이더 생성 (강을 건너지 못하게 하는 투명 벽)
        CreateRiverColliders();

        // 강 생성
        GenerateRiver();
    }

    void CreateRiverColliders()
    {
        // 위쪽 강둑 콜라이더
        GameObject topBank = new GameObject("RiverBank_Top");
        topBank.transform.parent = transform;
        topBank.layer = gameObject.layer; // 강과 같은 레이어 사용 (중요)
        edgeColTop = topBank.AddComponent<EdgeCollider2D>();
        if (riverPhysicsMaterial) edgeColTop.sharedMaterial = riverPhysicsMaterial;

        // 아래쪽 강둑 콜라이더
        GameObject botBank = new GameObject("RiverBank_Bottom");
        botBank.transform.parent = transform;
        botBank.layer = gameObject.layer;
        edgeColBottom = botBank.AddComponent<EdgeCollider2D>();
        if (riverPhysicsMaterial) edgeColBottom.sharedMaterial = riverPhysicsMaterial;
    }

    void GenerateRiver()
    {
        // 1. 랜덤한 시작점과 끝점 결정 (일차함수 y = ax + b 형태)
        // 왼쪽 벽(-x) 어딘가에서 오른쪽 벽(+x) 어딘가로 이어짐
        float startY = UnityEngine.Random.Range(-mapSize.y * 0.8f, mapSize.y * 0.8f);
        float endY = UnityEngine.Random.Range(-mapSize.y * 0.8f, mapSize.y * 0.8f);

        UnityEngine.Vector3 startPos = new UnityEngine.Vector3(-mapSize.x, startY, 0);
        UnityEngine.Vector3 endPos = new UnityEngine.Vector3(mapSize.x, endY, 0);

        // 2. 점 생성 및 노이즈 적용
        UnityEngine.Vector3[] points = new UnityEngine.Vector3[segments];
        List<UnityEngine.Vector2> colPointsTop = new List<UnityEngine.Vector2>();
        List<UnityEngine.Vector2> colPointsBottom = new List<UnityEngine.Vector2>();

        float halfWidth = riverWidth * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / (segments - 1);

            // 기본 직선 보간 (Lerp)
            UnityEngine.Vector3 basePos = UnityEngine.Vector3.Lerp(startPos, endPos, t);

            // 펄린 노이즈로 Y축 굴곡 추가 (자연스러운 강 모양)
            float noise = Mathf.PerlinNoise(basePos.x * noiseScale, basePos.y * noiseScale) - 0.5f;
            basePos.y += noise * noiseAmount;

            points[i] = basePos;

            // 콜라이더 점 계산 (강의 폭만큼 위/아래로 벌림)
            // 강이 기울어져 있을 수 있으므로 법선 벡터를 구해서 벌려야 정확하지만,
            // 간단하게 Y축으로만 벌려도 게임플레이엔 지장 없음
            colPointsTop.Add(new UnityEngine.Vector2(basePos.x, basePos.y + halfWidth));
            colPointsBottom.Add(new UnityEngine.Vector2(basePos.x, basePos.y - halfWidth));
        }

        // 3. 라인 렌더러 및 콜라이더에 점 할당
        lineRenderer.SetPositions(points);
        edgeColTop.SetPoints(colPointsTop);
        edgeColBottom.SetPoints(colPointsBottom);
    }
}