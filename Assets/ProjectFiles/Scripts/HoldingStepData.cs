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

// What appears when the aircraft pauses at a checkpoint. Knob always comes before the timer.
public enum HoldingCheckpointMode
{
    KnobThenTimer,  // knob -> correct panel -> post-knob audio -> Start Timer button
    KnobOnly,       // knob -> correct panel -> aircraft resumes at cruise speed
    TimerOnly       // Start Timer button straight away
}

// One group per pause point.
[System.Serializable]
public struct HoldingCheckpoint
{
   
    [Tooltip("What appears at this pause.")]
    public HoldingCheckpointMode mode;

    [Header("Text to show when the aircraft pauses")]
    [Tooltip("Info panel text shown as soon as the aircraft pauses here, same moment as Pause Audio.")]
    public string pauseInfoText;

    [Tooltip("Normalized spline T where the aircraft pauses. This is also where the timing leg starts.")]
    public float pauseAtT;

    [Tooltip("Normalized spline T where the timing leg ends. Ignored for KnobOnly.")]
    public float legEndT;

    [Header("Info Text to show while changing knob value")]
    [Tooltip("Info panel text shown while the knob is up. Ignored for TimerOnly.")]
    public string knobInfoText;

    [Tooltip("Course (degrees) the user must dial on the knob. Must match exactly. Ignored for TimerOnly.")]
    public float requiredCourse;

    [Header("Info Text to Show when the start timer button appears")]
    [Tooltip("Info panel text shown once the Start Timer button/timer is running. Ignored for KnobOnly.")]
    public string timerInfoText;

    [Tooltip("Timer length shown in the popup, in minutes (1 or 2). Display only. Ignored for KnobOnly.")]
    public int timerMinutes;

    [Tooltip("Real seconds the countdown takes. Leg speed is auto-calculated from this and the distance to legEndT. Ignored for KnobOnly.")]
    public float simulatedDuration;

    [Header("Audio to play as soon as the aircraft pauses")]
    [Tooltip("Plays as soon as the aircraft pauses. The aircraft (and knob/timer) wait until it finishes. Optional.")]
    public AudioClip pauseAudio;

    [Header("Audio to play between Knob and start timer")]
    [Tooltip("Plays after the 'correct' panel, before the Start Timer button appears. Only used in KnobThenTimer. Optional.")]
    public AudioClip postKnobAudio;
}

// Plays while the aircraft is flying, the moment it passes triggerT. Independent of checkpoints.
[System.Serializable]
public struct HoldingParallelAudio
{
    [Tooltip("Normalized spline T at which this clip starts playing.")]
    public float triggerT;
    public AudioClip clip;
}

[System.Serializable]
public struct HoldingHighlightPart
{
    [Tooltip("Matches this part to a scene entry in HoldingFamiliarizationController's Highlightable Parts list.")]
    public string id;

    public string title;

    [TextArea(2, 5)]
    public string bodyText;

    public AudioClip explanationAudio;
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

    [Header("Familiarization - Guided Highlight Sequence")]
    [Tooltip("Ordered list of parts to highlight one at a time. Empty = this step doesn't use the guided sequence.")]
    public HoldingHighlightPart[] highlightSequence;

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

    [Header("Info Text to Show on start of Simulation ")]
    [Tooltip("Info panel text shown as the aircraft starts flying at the beginning of this step.")]
    public string stepStartInfoText;

    [Header("Simulation - Checkpoints")]
    [Tooltip("One entry per pause point, in the order the aircraft reaches them.")]
    public HoldingCheckpoint[] checkpoints;

    [Header("Simulation - Parallel Audio")]
    [Tooltip("Clips that play while the aircraft flies, each starting when the aircraft passes its Trigger T. Independent of checkpoints.")]
    public HoldingParallelAudio[] parallelAudios;

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