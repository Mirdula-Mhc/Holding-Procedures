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

    [Tooltip("The duplicated UI spline sitting on your Map canvas.")]
    [SerializeField] private SplineContainer uiSpline;

    [Header("Target Icon")]
    [SerializeField] private RectTransform aircraftIcon;

    [Header("Rotation Settings")]
    public bool rotateToFaceDirection = true;
    [Tooltip("Fine-tune offset if needed (leave at 0).")]
    public float iconAngleOffset = 0f;

    private void LateUpdate()
    {
        if (trackedAircraft == null || aircraftIcon == null || uiSpline == null)
            return;

        float t = trackedAircraft.NormalizedT;

        // 1. Position: Sample position & tangent from UI Spline
        SplineUtility.Evaluate(uiSpline.Spline, t, out float3 localSplinePos, out float3 localSplineTangent, out _);

        Vector3 worldPos = uiSpline.transform.TransformPoint(localSplinePos);
        worldPos.z = aircraftIcon.position.z;
        aircraftIcon.position = worldPos;

        // 2. Rotation: Transform tangent directly into Canvas local space
        if (rotateToFaceDirection)
        {
            Vector3 worldTangent = uiSpline.transform.TransformDirection(localSplineTangent);
            Vector3 canvasTangent = aircraftIcon.parent != null
                ? aircraftIcon.parent.InverseTransformDirection(worldTangent)
                : worldTangent;

            if (canvasTangent.sqrMagnitude > 0.0001f)
            {
                // Calculate 2D travel angle on canvas (-180 to +180)
                float moveAngle = Mathf.Atan2(canvasTangent.y, canvasTangent.x) * Mathf.Rad2Deg;

                // +90 deg aligns the down-facing sprite nose directly into the direction of travel
                aircraftIcon.localRotation = Quaternion.Euler(0f, 0f, moveAngle + 90f + iconAngleOffset);
            }
        }
    }

    public void SetUiSpline(SplineContainer newUiSpline)
    {
        uiSpline = newUiSpline;
    }
}