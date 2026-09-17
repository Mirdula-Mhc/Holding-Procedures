// HoldingStepData.cs
//
// Single data type for every step in the Holding Procedures flow - mirrors ScenarioData's
// pattern exactly: ONE list of these lives on HoldingScenarioManager, ONE currentIndex walks
// through it, and stepType tells HoldingUIController which panel to show. A step that doesn't
// need a field (e.g. a Familiarization step's sectorOptions) just leaves it empty, same as
// ScenarioData leaving questionText empty on a video-only entry.

using UnityEngine;
using UnityEngine.Splines;

public enum HoldingStepType
{
    Familiarization,   // Phase 1: one highlighted UI element + voiceover explaining it
    SectorSelect,       // Phase 2 start: pie chart, one sector interactable, auto-advances on pick
    Simulation,         // Phase 2 body: aircraft flies a spline, pauses at checkpoints for a timer
    Recap               // Phase 3: quick fact / summary (unconfirmed content, page type reserved)
}

[CreateAssetMenu(fileName = "Step_", menuName = "A320/Holding Step Data")]
public class HoldingStepData : ScriptableObject
{
    [Header("Identification")]
    public string stepId;
    public HoldingStepType stepType;

    // ------------------------------------------------------------------
    // FAMILIARIZATION (Phase 1)
    // ------------------------------------------------------------------
    [Header("Familiarization")]
    [Tooltip("Identifies which UI element to highlight/pop up for this step. HoldingUIController maps this id to an actual GameObject in the scene - add one entry per familiarization step there.")]
    public string highlightElementId;

    [TextArea(2, 4)]
    public string familiarizationLabel;

    // ------------------------------------------------------------------
    // SECTOR SELECT (Phase 2 start)
    // ------------------------------------------------------------------
    [Header("Sector Select")]
    [Tooltip("Which sector (Direct/Offset/Parallel) is the correct/interactable one on the pie chart for this step. All three segments are shown; only this one is interactable.")]
    public HoldingEntryType correctSector;

    // ------------------------------------------------------------------
    // SIMULATION (Phase 2 body)
    // ------------------------------------------------------------------
    [Header("Simulation - Splines")]
    public SplineContainer worldSpline;
    public SplineContainer uiSpline;

    [Tooltip("Whether HoldingMapView should draw a dotted preview of this entry's path. Only 2 of the 3 entry types need this.")]
    public bool showDottedEntryPath;

    [Header("Simulation - HSI")]
    public float approachCourse = 330f;
    public float holdingInboundCourse = 270f;

    // Inside HoldingStepData.cs
    [Header("Simulation - Checkpoints")]
    [Tooltip("Normalized T where the aircraft pauses for the timer popup.")]
    public float[] pauseAtProgressT;

    [Tooltip("Normalized T where each timing leg ends (when timer reaches 00:00).")]
    public float[] timingLegEndT; // Size must match pauseAtProgressT

    public float defaultCruiseSpeed = 35f;
    public float simulatedTimerDuration = 4f;

    // ------------------------------------------------------------------
    // RECAP (Phase 3 - reserved, content unconfirmed)
    // ------------------------------------------------------------------
    [Header("Recap")]
    [TextArea(2, 4)]
    public string recapText;

    // ------------------------------------------------------------------
    // AUDIO (any step type)
    // ------------------------------------------------------------------
    [Header("Voiceover")]
    public AudioClip voiceover;
}

public enum HoldingEntryType { Direct, Offset, Parallel }