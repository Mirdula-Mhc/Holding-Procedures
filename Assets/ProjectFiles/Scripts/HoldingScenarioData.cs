// HoldingScenarioData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Scenario_Direct", menuName = "A320/Holding Scenario Data")]
public class HoldingScenarioData : ScriptableObject
{
    [Header("Procedure Info")]
    public string scenarioName = "Direct Entry";
    public HoldingProcedureManager.EntryType entryType;

    [Header("HSI Settings")]
    [Tooltip("Heading tracked into the fix during entry (e.g., 330 for direct).")]
    public float approachCourse = 330f;
    [Tooltip("Inbound holding radial (e.g., 270).")]
    public float holdingInboundCourse = 270f;

    [Header("Pauses & Checkpoints")]
    [Tooltip("Normalized spline progress T (0.0 to 1.0) where aircraft pauses for the timer.")]
    public float[] pauseAtProgressT;

    [Header("Timer Settings")]
    [Tooltip("Simulated countdown duration in real seconds.")]
    public float simulatedTimerDuration = 4f;

    [Header("Audio & Narration (Phase 1 & 2)")]
    public AudioClip introductionVoiceover;
    public AudioClip fixPassageVoiceover;
}