// HoldingMapIconFollower.cs
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[DisallowMultipleComponent]
public class HoldingMapIconFollower : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("The 3D aircraft driving progress.")]
    [SerializeField] private SplineAircraftMover trackedAircraft;

    [Tooltip("The 3D Spline prefab aligned with the map.")]
    [SerializeField] private SplineContainer uiSpline;

    [Header("Target Icon")]
    [SerializeField] private RectTransform aircraftIcon;

    [Header("Rotation Settings")]
    public bool rotateToFaceDirection = true;
    [Tooltip("Fine-tune offset in 90-degree steps if the sprite is sideways.")]
    public float iconAngleOffset = 0f;

    private void LateUpdate()
    {
        if (trackedAircraft == null || aircraftIcon == null || uiSpline == null)
            return;

        float t = trackedAircraft.NormalizedT;

        // 1. Evaluate position & tangent along the spline
        SplineUtility.Evaluate(uiSpline.Spline, t, out float3 localSplinePos, out float3 localSplineTangent, out _);

        // 2. Direct World Position: Lock Z so it stays perfectly flat on the Canvas plane
        Vector3 worldPos = uiSpline.transform.TransformPoint(localSplinePos);
        worldPos.z = aircraftIcon.position.z;
        aircraftIcon.position = worldPos;

        // 3. Canvas-Space Rotation
        if (rotateToFaceDirection)
        {
            Vector3 worldTangent = uiSpline.transform.TransformDirection(localSplineTangent);
            Vector3 canvasTangent = aircraftIcon.parent != null
                ? aircraftIcon.parent.InverseTransformDirection(worldTangent)
                : worldTangent;

            if (canvasTangent.sqrMagnitude > 0.0001f)
            {
                float moveAngle = Mathf.Atan2(canvasTangent.y, canvasTangent.x) * Mathf.Rad2Deg;
                aircraftIcon.localRotation = Quaternion.Euler(0f, 0f, moveAngle + 90f + iconAngleOffset);
            }
        }
    }

    public void SetUiSpline(SplineContainer newUiSpline)
    {
        uiSpline = newUiSpline;
    }
}