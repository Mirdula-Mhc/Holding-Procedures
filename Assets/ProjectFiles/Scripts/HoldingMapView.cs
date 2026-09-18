// HoldingMapView.cs
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[DisallowMultipleComponent]
public class HoldingMapView : MonoBehaviour
{
    [Header("Line Renderer")]
    [SerializeField] private LineRenderer entryLineRenderer;

    [Header("Curve Sampling")]
    [Tooltip("Number of segments used to smooth the curved entry lines.")]
    [Range(20, 100)]
    [SerializeField] private int resolution = 60;

    [Tooltip("Pulls the line slightly closer to Map Camera so it renders in front of the terrain image.")]
    [SerializeField] private float forwardZOffset = -0.2f;

    private void Awake()
    {
        if (entryLineRenderer != null)
            entryLineRenderer.enabled = false;
    }

    public void ShowEntryPath(SplineContainer splineContainer, float endT)
    {
        if (entryLineRenderer == null || splineContainer == null || splineContainer.Spline == null)
            return;

        var spline = splineContainer.Spline;
        entryLineRenderer.positionCount = resolution;
        entryLineRenderer.useWorldSpace = true;
        entryLineRenderer.enabled = true;

        // Anchor Z directly on the front face of the Map panel (Canvas_SSC is at Z = 85.39)
        float frontPlaneZ = transform.position.z + forwardZOffset;

        // Inside HoldingMapView.cs -> ShowEntryPath()

        for (int i = 0; i < resolution; i++)
        {
            float t = Mathf.Lerp(0f, endT, (float)i / (resolution - 1));
            SplineUtility.Evaluate(spline, t, out float3 localPos, out _, out _);

            // 1. Convert local spline curve to canvas world coordinates (Z ? 95.39)
            Vector3 worldPoint = splineContainer.transform.TransformPoint((Vector3)localPos);

            // 2. Step 0.5 units closer to Map Camera so it renders in front of the map image
            worldPoint.z -= 0.5f;

            entryLineRenderer.SetPosition(i, worldPoint);
        }
    }

    public void ClearEntryPath()
    {
        if (entryLineRenderer != null)
        {
            entryLineRenderer.positionCount = 0;
            entryLineRenderer.enabled = false;
        }
    }
}