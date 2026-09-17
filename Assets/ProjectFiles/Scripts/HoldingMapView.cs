// HoldingMapView.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;
using Unity.Mathematics;

[DisallowMultipleComponent]
public class HoldingMapView : MonoBehaviour
{
    [Header("Map Panel")]
    [Tooltip("The existing map/diagram RectTransform already in the scene.")]
    [SerializeField] private RectTransform mapPanel;

    [Header("World <-> Map Mapping")]
    [Tooltip("The world-space Transform that maps to the map panel's local (0,0) - typically the holding fix.")]
    [SerializeField] private Transform worldOrigin;

    [Tooltip("Scale factor to fit the pattern inside the map panel.")]
    public float worldUnitsToMapPixels = 1f;

    [Header("Dotted Entry Path")]
    [SerializeField] private RectTransform dotTemplate;
    [Min(0.1f)] public float dotSpacingWorldUnits = 20f;
    public Color entryPathHighlightColor = new Color(0.345f, 0.843f, 0.925f);

    private readonly List<RectTransform> activeDots = new List<RectTransform>();
    private static Sprite cachedDotSprite;

    // NOTE: Update() has been removed so this script NEVER touches or overrides the aircraft icon.

    public void ShowEntryPath(SplineContainer spline, Color? overrideColor = null)
    {
        ClearEntryPath();

        if (spline == null || mapPanel == null)// || worldOrigin == null)
            return;

        Color color = overrideColor ?? entryPathHighlightColor;
        float splineLength = SplineUtility.CalculateLength(spline.Spline, spline.transform.localToWorldMatrix);
        if (splineLength <= 0f)
            return;

        int dotCount = Mathf.Max(2, Mathf.RoundToInt(splineLength / dotSpacingWorldUnits));

        for (int i = 0; i <= dotCount; i++)
        {
            float t = (float)i / dotCount;
            SplineUtility.Evaluate(spline.Spline, t, out float3 splinePos, out _, out _);
            Vector3 worldPos = spline.transform.TransformPoint(splinePos);

            RectTransform dot = SpawnDot();
            //dot.anchoredPosition = HoldingPathProjector.WorldToMapPosition(worldPos, worldOrigin.position, worldUnitsToMapPixels);

            Image dotImg = dot.GetComponent<Image>();
            if (dotImg != null)
                dotImg.color = color;

            activeDots.Add(dot);
        }
    }

    public void ClearEntryPath()
    {
        foreach (RectTransform dot in activeDots)
        {
            if (dot != null)
                Destroy(dot.gameObject);
        }
        activeDots.Clear();
    }

    private RectTransform SpawnDot()
    {
        RectTransform dot;

        if (dotTemplate != null)
        {
            dot = Instantiate(dotTemplate, mapPanel);
            dot.gameObject.SetActive(true);
        }
        else
        {
            GameObject go = new GameObject("EntryPathDot", typeof(RectTransform));
            go.transform.SetParent(mapPanel, false);
            dot = go.GetComponent<RectTransform>();
            dot.sizeDelta = new Vector2(6f, 6f);
            Image img = go.AddComponent<Image>();
            img.sprite = GetDotSprite();
            img.raycastTarget = false;
        }

        dot.anchorMin = new Vector2(0.5f, 0.5f);
        dot.anchorMax = new Vector2(0.5f, 0.5f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        return dot;
    }

    private static Sprite GetDotSprite()
    {
        if (cachedDotSprite != null)
            return cachedDotSprite;

        const int diameter = 64;
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float radius = diameter * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedDotSprite = Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
        return cachedDotSprite;
    }
}