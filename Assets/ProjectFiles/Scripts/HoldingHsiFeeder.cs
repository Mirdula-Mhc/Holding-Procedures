// HoldingHsiFeeder.cs
//
// Pure one-way readout: every frame, reads the tracked aircraft's world position/heading and the
// scenario's fix/inbound-course data, and writes the corresponding heading/DME/CDI-deviation/
// TO-FROM values onto an A320HSI component. The HSI never influences the aircraft - this script
// is the entire boundary between "3D flight" and "instrument display", matching the architecture
// decision that the 3D aircraft Transform is the single source of truth and the HSI is a readout.
//
// Deliberately does not touch A320HSI's course/ident/freq/DME-radio-2/display-range fields -
// those are set once per scenario (e.g. by whatever loads the ScriptableObject scenario data),
// not recomputed every frame, since they don't depend on the aircraft's live position.

using UnityEngine;

[DisallowMultipleComponent]
public class HoldingHsiFeeder : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("The aircraft whose position/heading drives the HSI readout.")]
    [SerializeField] private Transform aircraft;

    [Tooltip("The holding fix / VOR station this radio is tuned to.")]
    [SerializeField] private Transform fix;

    [Header("Target")]
    [SerializeField] private A320HSI hsi;

    [Header("Course Geometry")]
    [Tooltip("The inbound course TO the fix, degrees magnetic - e.g. 270 for the reference scenario. This script does not compute this from the scenario layout; set it to match whatever course the current holding pattern actually uses.")]
    [Range(0f, 359.99f)] public float inboundCourse = 270f;

    [Tooltip("World units (meters) per nautical mile, for the DME readout. 1852 is the real-world conversion; lower this if your scene uses a compressed/scaled-down world for the holding pattern.")]
    [Min(0.01f)] public float worldUnitsPerNauticalMile = 1852f;

    [Header("TO/FROM")]
    [Tooltip("Distance (world units) from the fix inside which the TO/FROM flag is allowed to flip - approximates the real 'cone of confusion' region directly over a VOR, where the flag is unreliable. The pilot's script explicitly calls out cone-of-confusion behavior near the fix, so this is exposed rather than hardcoded.")]
    [Min(0f)] public float coneOfConfusionRadius = 15f;

    private void Update()
    {
        if (aircraft == null || fix == null || hsi == null)
            return;

        Vector3 toAircraft = aircraft.position - fix.position;
        toAircraft.y = 0f; // top-down/horizontal-only, matching the map's XZ convention

        float distanceWorldUnits = toAircraft.magnitude;

        hsi.heading = Wrap360(aircraft.eulerAngles.y);
        hsi.course = inboundCourse;
        hsi.dme1 = distanceWorldUnits / worldUnitsPerNauticalMile;
        hsi.deviation = ComputeCrossTrackDeviation(toAircraft, distanceWorldUnits);
        hsi.navFlag = ComputeNavFlag(toAircraft, distanceWorldUnits);
    }

    // Cross-track deviation off the inbound course radial, expressed as an angular deflection
    // (degrees) the way a real CDI needle reads it - NOT a raw linear distance, so the needle's
    // sensitivity naturally increases the closer the aircraft is to the fix, matching real VOR
    // behavior (and matching A320HSI.deviation's existing -10..+10 degree range/convention).
    private float ComputeCrossTrackDeviation(Vector3 toAircraft, float distanceWorldUnits)
    {
        if (distanceWorldUnits < 0.01f)
            return 0f;

        float courseRad = inboundCourse * Mathf.Deg2Rad;
        Vector3 courseDir = new Vector3(Mathf.Sin(courseRad), 0f, Mathf.Cos(courseRad));

        // Negated: when displaced right (+angle), needle deflects left (-angle) to indicate fly-left
        float signedAngle = Vector3.SignedAngle(courseDir, toAircraft.normalized, Vector3.up);
        return Mathf.Clamp(-signedAngle, -10f, 10f);
    }

    // TO if the aircraft is on the inbound side of the fix relative to the course direction,
    // FROM on the outbound side, OFF within the cone-of-confusion radius directly over the fix
    // where a real VOR receiver's flag is unreliable.
    private A320HSI.NavFlag ComputeNavFlag(Vector3 toAircraft, float distanceWorldUnits)
    {
        if (distanceWorldUnits <= coneOfConfusionRadius)
            return A320HSI.NavFlag.OFF;

        float courseRad = inboundCourse * Mathf.Deg2Rad;
        Vector3 courseDir = new Vector3(Mathf.Sin(courseRad), 0f, Mathf.Cos(courseRad));

        // Positive dot = aircraft is on the far side of the fix along the course direction
        // (i.e. beyond the fix, outbound) -> FROM. Negative = still approaching -> TO.
        float alongCourse = Vector3.Dot(toAircraft.normalized, courseDir);
        return alongCourse >= 0f ? A320HSI.NavFlag.FROM : A320HSI.NavFlag.TO;
    }

    private static float Wrap360(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped < 0f)
            wrapped += 360f;
        return wrapped;
    }
}