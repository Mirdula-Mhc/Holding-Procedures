// SplineAircraftMover.cs
//
// Drives a 3D aircraft Transform along a Unity Spline (com.unity.splines) at a constant
// world-space speed, banking it into turns based on actual turn rate rather than a fixed/faked
// roll value - the same "figure out the physically correct motion rather than fudge it" approach
// used for the ATC sim's aircraft animation (see /areas/atc-signal-sim.md: "Achieved realistic
// aircraft motion without reference footage, through logical reasoning").
//
// DELIBERATELY DOES NOT include the pause/decision-point/timer state machine discussed for the
// sector-entry mechanism - that flow is still being worked out with the pilot and will change.
// This component only answers "given a spline and a speed, move the plane along it and bank it
// correctly" - a Play()/Pause()/Stop() surface is exposed below so a future controller can drive
// the pause/resume beat without this script needing to know anything about *why* it's pausing.
//
// Requires the "Splines" package (com.unity.splines) - Window > Package Manager > Unity Registry.

using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[DisallowMultipleComponent]
public class SplineAircraftMover : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("The spline this aircraft currently follows. Swappable at runtime (e.g. when the pilot's chosen entry type binds a different spline) via SetSpline().")]
    [SerializeField] private SplineContainer spline;

    [Header("Motion")]
    [Tooltip("Constant ground speed along the spline, in world units (meters) per second.")]
    [Min(0f)] public float speed = 60f;

    [Tooltip("How quickly the aircraft's actual roll eases toward the roll its current turn rate calls for. Same role as A320PFD/A320HSI's Smooth Duration.")]
    [Min(0.01f)] public float bankSmoothDuration = 1.2f;

    [Tooltip("Maximum bank angle, degrees, reached at or beyond Bank Rate Reference.")]
    [Min(1f)] public float maxBankDegrees = 25f;

    [Tooltip("Turn rate (degrees/sec of heading change) that produces the full Max Bank Degrees. Smaller = more sensitive (reaches max bank sooner); larger = needs a tighter turn before maxing out.")]
    [Min(0.1f)] public float bankRateReference = 3f;

    [Tooltip("If true, starts moving automatically on Awake. If false, call Play() from another script once the scenario is ready.")]
    public bool playOnAwake = true;

    // ==================================================
    // Runtime state
    // ==================================================
    private float distanceTraveled;
    private float splineLength;
    private bool isPlaying;

    private float previousHeadingY;
    private bool hasPreviousHeading;
    private float currentBank;
    private float bankVelocity;

    /// <summary>Normalized progress along the current spline, 0..1. Read-only from outside; drive motion via speed/Play/Pause, not by writing this directly.</summary>
    public float NormalizedT => splineLength > 0f ? Mathf.Clamp01(distanceTraveled / splineLength) : 0f;

    /// <summary>True once NormalizedT has reached 1 - i.e. the aircraft has reached the end of the currently assigned spline. A future controller (decision pause, scenario advance) watches this rather than this script owning any decision logic itself.</summary>
    public bool ReachedEnd => splineLength > 0f && distanceTraveled >= splineLength;

    /// <summary>The aircraft's current bank/roll angle, degrees - positive = rolling right, matching A320PFD's CurrentAircraftRoll convention so a PFD driven by the same aircraft would agree with this value.</summary>
    public float CurrentBankDegrees => currentBank;

    private void Awake()
    {
        if (spline != null)
            CacheSplineLength();

        if (playOnAwake && spline != null)
            Play();
    }

    private void Update()
    {
        if (!isPlaying || spline == null || splineLength <= 0f)
            return;

        distanceTraveled += speed * Time.deltaTime;
        float clampedDistance = Mathf.Clamp(distanceTraveled, 0f, splineLength);
        float t = clampedDistance / splineLength;

        SplineUtility.Evaluate(spline.Spline, t, out float3 splinePos, out float3 splineTangent, out float3 splineUp);

        Vector3 worldPos = spline.transform.TransformPoint(splinePos);
        Vector3 worldTangent = spline.transform.TransformDirection(((Vector3)splineTangent).normalized);

        transform.position = worldPos;

        if (worldTangent.sqrMagnitude > 0.0001f)
        {
            float headingY = Quaternion.LookRotation(worldTangent, Vector3.up).eulerAngles.y;
            UpdateBank(headingY);

            // Applies heading (yaw) AND the computed bank (roll) together - roll is expressed as
            // a rotation about the aircraft's own forward axis, applied after yaw so it banks
            // around its current nose direction rather than the world Z axis.
            transform.rotation = Quaternion.Euler(0f, headingY, 0f) * Quaternion.Euler(0f, 0f, -currentBank);
        }
    }

    /// <summary>Jumps to a normalized position on the current spline and applies the pose immediately, without playing.</summary>
    public void SeekToNormalized(float t)
    {
        if (spline == null || splineLength <= 0f)
            return;

        distanceTraveled = Mathf.Clamp01(t) * splineLength;
        hasPreviousHeading = false;
        currentBank = 0f;
        bankVelocity = 0f;

        SplineUtility.Evaluate(spline.Spline, Mathf.Clamp01(t), out float3 splinePos, out float3 splineTangent, out float3 splineUp);

        Vector3 worldPos = spline.transform.TransformPoint(splinePos);
        Vector3 worldTangent = spline.transform.TransformDirection(((Vector3)splineTangent).normalized);

        transform.position = worldPos;

        if (worldTangent.sqrMagnitude > 0.0001f)
        {
            float headingY = Quaternion.LookRotation(worldTangent, Vector3.up).eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, headingY, 0f);
        }
    }
    // Bank is derived from the aircraft's OWN actual turn rate (heading change per second),
    // never set directly - so however the spline curves, the visual bank always matches what
    // that curve is actually doing to the heading, the same "derive it, don't fake it" approach
    // A320PFD uses for pitch-from-altitude-rate.
    private void UpdateBank(float headingY)
    {
        float targetBank = 0f;

        if (hasPreviousHeading && Time.deltaTime > 0.0001f)
        {
            float headingDelta = Mathf.DeltaAngle(previousHeadingY, headingY);
            float turnRateDegPerSec = headingDelta / Time.deltaTime;
            float bankFraction = Mathf.Clamp(turnRateDegPerSec / bankRateReference, -1f, 1f);
            targetBank = bankFraction * maxBankDegrees;
        }

        previousHeadingY = headingY;
        hasPreviousHeading = true;

        currentBank = Mathf.SmoothDampAngle(currentBank, targetBank, ref bankVelocity, bankSmoothDuration);
    }


    private void CacheSplineLength()
    {
        splineLength = SplineUtility.CalculateLength(spline.Spline, spline.transform.localToWorldMatrix);
    }

    // ==================================================
    // Public control surface - a future decision/pause controller drives these; this script
    // never decides on its own to pause.
    // ==================================================
    public void Play() => isPlaying = true;
    public void Pause() => isPlaying = false;

    public void Stop()
    {
        isPlaying = false;
        distanceTraveled = 0f;
        hasPreviousHeading = false;
        currentBank = 0f;
        bankVelocity = 0f;
    }

    /// <summary>Swaps the spline this aircraft follows - e.g. once a sector-entry choice (Direct/Parallel/Teardrop) is made - and resets progress to its start. Does not change isPlaying, so a caller pausing before the swap and calling Play() after gets the pause-then-resume behavior.</summary>
    public void SetSpline(SplineContainer newSpline, bool resetDistance = true)
    {
        spline = newSpline;
        CacheSplineLength();

        if (resetDistance)
        {
            distanceTraveled = 0f;
            hasPreviousHeading = false;
        }
    }
}