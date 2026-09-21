// HoldingHsiFeeder.cs
using UnityEngine;

[DisallowMultipleComponent]
public class HoldingHsiFeeder : MonoBehaviour
{
    [Header("Source Aircraft")]
    [SerializeField] private Transform aircraft;

    [Header("Primary Station")]
    [SerializeField] private Transform fix;

    [Header("Target Instrument")]
    [SerializeField] private A320HSI hsi;

    [Header("Course Settings")]
    [Tooltip("Course tracked into the fix during initial entry (e.g. 330 for direct entry).")]
    [Range(0f, 359.99f)] public float approachCourse = 330f;

    [Tooltip("Inbound course for the holding pattern (270 for standard hold).")]
    [Range(0f, 359.99f)] public float holdingInboundCourse = 270f;

    [Tooltip("World units (meters) per nautical mile. Tune so DME fits scene scale.")]
    [Min(0.01f)] public float worldUnitsPerNauticalMile = 10f;

    [Tooltip("Minimum DME reading at the fix (simulates altitude slant range, e.g. 1.2 NM).")]
    [Min(0f)] public float minDmeNauticalMiles = 1.2f;

    [Header("Fix Passage & Cone of Confusion")]
    [Tooltip("Distance threshold (world units) to consider the fix reached.")]
    public float fixCrossingRadius = 5f;

    [Tooltip("Radius directly over the fix where station signal is lost (NAV flag turns ON, needle centers).")]
    public float coneOfConfusionRadius = 4f;

    /// <summary>
    /// While true, this feeder stops writing hsi.course (the course knob owns it), and the
    /// heading/deviation/flag/DME values are frozen exactly as they were. Set by
    /// HoldingUIController while the course knob is on screen.
    /// </summary>
    [HideInInspector] public bool courseOverride;

    private bool hasCrossedFix = false;

    private void Start()
    {
        hasCrossedFix = false;
    }

    private void Update()
    {
        if (aircraft == null || fix == null || hsi == null)
            return;

        // Knob is active: the aircraft is paused and the whole HSI must stay exactly as it was.
        // Only the course pointer changes, and the knob drives that itself.
        if (courseOverride)
            return;

        Vector3 toAircraft = aircraft.position - fix.position;
        toAircraft.y = 0f;
        float groundDistance = toAircraft.magnitude;

        // Switch to holding course once the fix is crossed
        if (!hasCrossedFix && groundDistance <= fixCrossingRadius)
        {
            hasCrossedFix = true;
        }

        float activeCourse = hasCrossedFix ? holdingInboundCourse : approachCourse;

        // 1. Heading & Course
        hsi.heading = Wrap360(aircraft.eulerAngles.y);
        hsi.course = activeCourse;

        // 2. DME
        float rawDme = groundDistance / worldUnitsPerNauticalMile;
        hsi.dme1 = Mathf.Max(rawDme, minDmeNauticalMiles);

        Vector3 simulatedSpaOffset = new Vector3(100f, 0f, -50f);
        float dme2Dist = (aircraft.position - (fix.position + simulatedSpaOffset)).magnitude / worldUnitsPerNauticalMile;
        hsi.dme2 = Mathf.Max(dme2Dist, 2.5f);

        // 3. Flags & Cone of Confusion
        bool inCone = groundDistance <= coneOfConfusionRadius;
        hsi.navFlag = inCone ? A320HSI.NavFlag.OFF : ComputeNavFlag(toAircraft, activeCourse);

        // 4. CDI Deviation (pinned to 0 inside cone of confusion to eliminate glitching)
        if (inCone)
        {
            hsi.deviation = 0f;
        }
        else
        {
            hsi.deviation = ComputeCrossTrackDeviation(toAircraft, groundDistance, activeCourse);
        }
    }

    private float ComputeCrossTrackDeviation(Vector3 toAircraft, float groundDistance, float course)
    {
        if (groundDistance < 0.01f)
            return 0f;

        float courseRad = course * Mathf.Deg2Rad;
        Vector3 courseDir = new Vector3(Mathf.Sin(courseRad), 0f, Mathf.Cos(courseRad));

        float crossTrack = Vector3.Cross(courseDir, toAircraft).y;

        // Fly-To deflection
        float angularDeviation = -Mathf.Atan2(crossTrack, groundDistance) * Mathf.Rad2Deg;
        return Mathf.Clamp(angularDeviation, -10f, 10f);
    }

    private A320HSI.NavFlag ComputeNavFlag(Vector3 toAircraft, float course)
    {
        float courseRad = course * Mathf.Deg2Rad;
        Vector3 courseDir = new Vector3(Mathf.Sin(courseRad), 0f, Mathf.Cos(courseRad));

        float alongCourse = Vector3.Dot(toAircraft.normalized, courseDir);
        return alongCourse >= 0f ? A320HSI.NavFlag.FROM : A320HSI.NavFlag.TO;
    }

    public void ResetFixCrossing()
    {
        hasCrossedFix = false;
    }

    private static float Wrap360(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped < 0f) wrapped += 360f;
        return wrapped;
    }
}