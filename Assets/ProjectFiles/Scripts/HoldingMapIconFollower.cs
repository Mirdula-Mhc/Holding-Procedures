using UnityEngine;

[DisallowMultipleComponent]
public class HoldingMapIconFollower : MonoBehaviour
{
    [Tooltip("The aircraft whose spline progress (0..1) drives the icon along the 2D path below.")]
    [SerializeField] private SplineAircraftMover trackedAircraft;

    [Tooltip("The icon RectTransform to move - e.g. Aircraft_icon.")]
    [SerializeField] private RectTransform aircraftIcon;

    [Tooltip("Waypoints placed by hand tracing the path the icon should follow, in order, for t=0..1. Needs at least 2.")]
    [SerializeField] private RectTransform[] pathWaypoints;

    [Tooltip("If true, the icon rotates to face along its path.")]
    public bool rotateToFaceDirection = true;

    [Tooltip("Offset applied on top of the direction (0 = artwork points straight UP by default).")]
    public float iconForwardOffsetDegrees = 0f;

    [Tooltip("How smoothly the icon rotates into turns (seconds). 0.05 - 0.1 gives snappy yet smooth turns.")]
    [Range(0.01f, 0.5f)] public float rotationSmoothTime = 0.08f;

    private float[] cumulativeDistances;
    private float totalPathLength;
    private float currentZRotation;
    private float rotationVelocity;

    private void Awake()
    {
        CalculatePathDistances();
    }

    private void OnValidate()
    {
        CalculatePathDistances();
    }

    public void CalculatePathDistances()
    {
        if (pathWaypoints == null || pathWaypoints.Length < 2)
            return;

        cumulativeDistances = new float[pathWaypoints.Length];
        cumulativeDistances[0] = 0f;
        totalPathLength = 0f;

        for (int i = 0; i < pathWaypoints.Length - 1; i++)
        {
            if (pathWaypoints[i] != null && pathWaypoints[i + 1] != null)
            {
                float segmentDist = Vector2.Distance(pathWaypoints[i].anchoredPosition, pathWaypoints[i + 1].anchoredPosition);
                totalPathLength += segmentDist;
            }
            cumulativeDistances[i + 1] = totalPathLength;
        }
    }

    private void Update()
    {
        if (trackedAircraft == null || aircraftIcon == null || pathWaypoints == null || pathWaypoints.Length < 2)
            return;

        if (totalPathLength <= 0f)
            CalculatePathDistances();

        PlaceIconAtT(trackedAircraft.NormalizedT);
    }

    private void LateUpdate()
    {
        if (trackedAircraft == null || aircraftIcon == null || pathWaypoints == null || pathWaypoints.Length < 2)
            return;

        if (totalPathLength <= 0f)
            CalculatePathDistances();

        PlaceIconAtT(trackedAircraft.NormalizedT);
    }
    private void PlaceIconAtT(float t)
    {
        float targetDistance = Mathf.Clamp01(t) * totalPathLength;

        // Locate segment based on true pixel distance traveled
        int segIndex = 0;
        while (segIndex < cumulativeDistances.Length - 2 && cumulativeDistances[segIndex + 1] < targetDistance)
        {
            segIndex++;
        }

        RectTransform from = pathWaypoints[segIndex];
        RectTransform to = pathWaypoints[segIndex + 1];

        if (from == null || to == null)
            return;

        float segmentLength = cumulativeDistances[segIndex + 1] - cumulativeDistances[segIndex];
        float localT = segmentLength > 0.001f ? (targetDistance - cumulativeDistances[segIndex]) / segmentLength : 0f;

        // 1. Position interpolation
        aircraftIcon.anchoredPosition = Vector2.Lerp(from.anchoredPosition, to.anchoredPosition, localT);

        // 2. Smooth rotation interpolation
        //if (rotateToFaceDirection)
        //{
        //    Vector2 dir = to.anchoredPosition - from.anchoredPosition;
        //    if (dir.sqrMagnitude > 0.001f)
        //    {
        //        float targetAngle = -Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + iconForwardOffsetDegrees;
        //        currentZRotation = Mathf.SmoothDampAngle(currentZRotation, targetAngle, ref rotationVelocity, rotationSmoothTime);
        //        aircraftIcon.localRotation = Quaternion.Euler(0f, 0f, currentZRotation);
        //    }
        //}

        // Inside PlaceIconAtT() in HoldingMapIconFollower.cs:
        if (rotateToFaceDirection && trackedAircraft != null)
        {
            // Mirror 3D world yaw onto 2D UI Z rotation
            float targetAngle = -trackedAircraft.transform.eulerAngles.y + iconForwardOffsetDegrees;
            currentZRotation = Mathf.SmoothDampAngle(currentZRotation, targetAngle, ref rotationVelocity, rotationSmoothTime);
            aircraftIcon.localRotation = Quaternion.Euler(0f, 0f, currentZRotation);
        }
    }
}