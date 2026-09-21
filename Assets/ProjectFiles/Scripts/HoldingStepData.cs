// HoldingStepData.cs
//
// Single data type for every step in the Holding Procedures flow - mirrors ScenarioData's
// pattern: ONE list of these lives on HoldingScenarioManager, ONE currentIndex walks
// through it, and stepType tells HoldingUIController which panel to show.

using UnityEngine;
using UnityEngine.Splines;

public enum HoldingStepType
{
    Familiarization,
    SectorSelect,
    Simulation,
    Recap
}

public enum HoldingEntryType { Direct, Offset, Parallel }

// One group per pause point. The aircraft pauses at pauseAtT, the course knob appears,
// then the Start Timer popup, then the aircraft flies to legEndT while the timer counts down.
[System.Serializable]
public struct HoldingCheckpoint
{
    [Tooltip("Course (degrees) the user must dial on the knob. Set button appears within +/- tolerance of this.")]
     public float requiredCourse;

    [Tooltip("Timer length shown in the popup, in minutes (1 or 2). Display only - not real time.")]
     public int timerMinutes;

    [Tooltip("Normalized spline T where the aircraft pauses. This is also where the timing leg starts.")]
     public float pauseAtT;

    [Tooltip("Normalized spline T where the timing leg ends (timer reaches 00:00 as the aircraft arrives here).")]
     public float legEndT;

    [Tooltip("Real seconds the countdown takes. The aircraft's leg speed is auto-calculated from this and the distance to legEndT.")]
    public float simulatedDuration;
}

[CreateAssetMenu(fileName = "Step_", menuName = "A320/Holding Step Data")]
public class HoldingStepData : ScriptableObject
{
    [Header("Identification")]
    public string stepId;
    public HoldingStepType stepType;

    // ------------------------------------------------------------------
    // FAMILIARIZATION
    // ------------------------------------------------------------------
    [Header("Familiarization")]
    [Tooltip("Identifies which UI element to highlight for this step. HoldingUIController maps this id to a GameObject in the scene.")]
    public string highlightElementId;

    [TextArea(2, 4)]
    public string familiarizationLabel;

    [Header("Info / Instruction Panel")]
    public bool showInfoPanel;

    [TextArea(3, 6)]
    public string infoPanelText;

    // ------------------------------------------------------------------
    // SECTOR SELECT
    // ------------------------------------------------------------------
    [Header("Sector Select")]
    public HoldingEntryType correctSector;

    // ------------------------------------------------------------------
    // SIMULATION
    // ------------------------------------------------------------------
    [Header("Simulation - Splines")]
    public SplineContainer worldSpline;
    public SplineContainer uiSpline;

    [Header("Entry Path Display")]
    [Tooltip("Whether HoldingMapView should draw a dotted preview of this entry's path. False for Direct, true for Offset/Parallel.")]
    public bool showDottedEntryPath = true;

    [Tooltip("Normalized T where the entry maneuver finishes merging into the pattern.")]
    public float entryPathEndT = 0.40f;

    [Header("Simulation - HSI")]
    public float approachCourse = 330f;
    public float holdingInboundCourse = 270f;

    [Header("Simulation - Speed")]
    [Tooltip("Aircraft speed between timing legs. Leg speeds are auto-calculated per checkpoint.")]
    public float defaultCruiseSpeed = 35f;

    [Header("Simulation - Checkpoints")]
    [Tooltip("One entry per pause point. Direct = 1, Offset/Parallel = 2.")]
    public HoldingCheckpoint[] checkpoints;

    // ------------------------------------------------------------------
    // RECAP
    // ------------------------------------------------------------------
    [Header("Recap")]
    [TextArea(2, 4)]
    public string recapText;

    // ------------------------------------------------------------------
    // AUDIO
    // ------------------------------------------------------------------
    [Header("Voiceover")]
    public AudioClip voiceover;
}