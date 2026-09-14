// HoldingMapView.cs
//
// Owns two things on the existing holding-pattern map RectTransform (a Screen Space - Camera
// canvas panel already in the scene - this script does NOT create its own canvas, unlike
// A320PFD/A320HSI, since a map panel already exists to plug into):
//
//   1. A live aircraft icon that tracks the 3D aircraft's position/heading every frame.
//   2. A static dotted preview line for the currently selected entry spline.
//
// Both use HoldingPathProjector.WorldToMapPosition() for the world-to-map conversion - the SAME
// function, called with the SAME worldOrigin/scale - so the icon can never visually drift off
// the dotted line it's supposed to be flying along. If you ever see them disagree, the bug is in
// how this script computed a differing origin/scale for one vs the other, not in the projector
// itself (there is only one projector function, so it cannot itself be the source of a mismatch).
//
// This script only reads the aircraft's Transform (via the SplineAircraftMover it's watching) -
// it never writes to it. Data flows one way: 3D aircraft -> map. Matches the architecture
// decision that the map/HSI are readouts, not drivers, of flight mechanics.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;
using Unity.Mathematics;

[DisallowMultipleComponent]
public class HoldingMapView : MonoBehaviour
{
    [Header("Map Panel")]
    [Tooltip("The existing map/diagram RectTransform already in the scene (Screen Space - Camera canvas). Icon and dotted-path dots are parented under this.")]
    [SerializeField] private RectTransform mapPanel;

    [Header("World <-> Map Mapping")]
    [Tooltip("The world-space Transform that maps to the map panel's local (0,0) - typically the holding fix.")]
    [SerializeField] private Transform worldOrigin;

    [Tooltip("How many map-local units correspond to one world unit (meter). Tune so the full holding pattern fits inside the map panel's rect.")]
    public float worldUnitsToMapPixels = 1f;

    [Header("Aircraft Icon")]
    [Tooltip("The aircraft being tracked. Read via its Transform every frame - never written to.")]
    [SerializeField] private SplineAircraftMover trackedAircraft;

    [Tooltip("Existing aircraft icon RectTransform on the map, already styled/sized in the scene. If left empty, a plain small triangle icon is generated at runtime.")]
    [SerializeField] private RectTransform aircraftIcon;

    [Tooltip("Only used if Aircraft Icon above is left empty - color of the runtime-generated fallback icon.")]
    public Color fallbackIconColor = new Color(1f, 0.835f, 0.29f); // matches A320HSI's ndYellow

    [Header("Dotted Entry Path")]
    [Tooltip("Existing dot prefab/RectTransform template to clone along the path. If left empty, a plain small circle dot is generated at runtime.")]
    [SerializeField] private RectTransform dotTemplate;

    [Tooltip("Spacing between dots along the spline, in world units (meters) - NOT map pixels, so the dot density stays consistent regardless of worldUnitsToMapPixels.")]
    [Min(0.1f)] public float dotSpacingWorldUnits = 20f;

    public Color entryPathHighlightColor = new Color(0.345f, 0.843f, 0.925f); // matches A320HSI's ndCyan

    // ==================================================
    // Runtime state
    // ==================================================
    private readonly List<RectTransform> activeDots = new List<RectTransform>();
    private RectTransform generatedIcon;
    private static Sprite cachedDotSprite;
    private static Sprite cachedIconSprite;

    private void Awake()
    {
        if (aircraftIcon == null)
            aircraftIcon = BuildFallbackIcon();
    }

    private void Update()
    {
        if (trackedAircraft == null || mapPanel == null || worldOrigin == null || aircraftIcon == null)
            return;

        HoldingPathProjector.ApplyToRectTransform(
            aircraftIcon,
            trackedAircraft.transform.position,
            trackedAircraft.transform.eulerAngles.y,
            worldOrigin.position,
            worldUnitsToMapPixels);
    }

    /// <summary>
    /// Samples the given spline and lays out dotted-line markers along it on the map, in the
    /// given highlight color, using the exact same world-to-map projection as the live aircraft
    /// icon. Call this once when an entry type is selected/previewed; call ClearEntryPath()
    /// first if a previous preview is still showing.
    /// </summary>
    public void ShowEntryPath(SplineContainer spline, Color? overrideColor = null)
    {
        ClearEntryPath();

        if (spline == null || mapPanel == null || worldOrigin == null)
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
            dot.anchoredPosition = HoldingPathProjector.WorldToMapPosition(worldPos, worldOrigin.position, worldUnitsToMapPixels);

            Image dotImg = dot.GetComponent<Image>();
            if (dotImg != null)
                dotImg.color = color;

            activeDots.Add(dot);
        }
    }

    /// <summary>Removes any dotted entry-path currently shown, so a new one can be drawn for a different scenario/entry choice.</summary>
    public void ClearEntryPath()
    {
        foreach (RectTransform dot in activeDots)
        {
            if (dot != null)
                Destroy(dot.gameObject);
        }
        activeDots.Clear();
    }

    // ==================================================
    // Dot / icon creation
    // ==================================================
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

    private RectTransform BuildFallbackIcon()
    {
        GameObject go = new GameObject("AircraftIcon_Generated", typeof(RectTransform));
        go.transform.SetParent(mapPanel, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(14f, 16f);

        Image img = go.AddComponent<Image>();
        img.sprite = GetIconSprite();
        img.color = fallbackIconColor;
        img.raycastTarget = false;

        generatedIcon = rt;
        return rt;
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

    // Simple upward-pointing triangle, matching the same procedural-sprite approach used in
    // A320PFD/A320HSI for their aircraft symbols, so a fallback icon here looks consistent with
    // the rest of the instrument set if no custom icon is assigned.
    private static Sprite GetIconSprite()
    {
        if (cachedIconSprite != null)
            return cachedIconSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float t = (float)y / (size - 1);
            float halfWidth = (size * 0.5f) * t;

            for (int x = 0; x < size; x++)
            {
                float distOutsideEdge = Mathf.Abs(x + 0.5f - size * 0.5f) - halfWidth;
                float alpha = Mathf.Clamp01(0.5f - distOutsideEdge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedIconSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return cachedIconSprite;
    }
}