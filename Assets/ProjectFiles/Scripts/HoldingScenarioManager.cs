// HoldingScenarioManager.cs
//
// Mirrors ScenarioManager.cs exactly. ONE list, ONE currentIndex, events fired outward,
// navigation only via GoNext()/GoPrevious(). This script never touches UI, audio, or the
// aircraft/HSI/map components directly - HoldingUIController, HoldingAudioController and the
// simulation components all react to its events, the same separation ATC uses.

using UnityEngine;
using System;
using System.Collections.Generic;

public class HoldingScenarioManager : MonoBehaviour
{
    [Header("Step Data")]
    [SerializeField] private List<HoldingStepData> steps;

    // ------------------------------------------------------------------
    // EVENTS
    // ------------------------------------------------------------------

    /// <summary>Fired when a step should begin (Next/Previous just navigated, or on Start).</summary>
    public event Action<HoldingStepData, int> OnStepLoaded;

    /// <summary>Fired when voiceover for the current step finishes playing.</summary>
    public event Action OnVoiceoverComplete;

    /// <summary>Fired when the correct sector is picked on a SectorSelect step.</summary>
    public event Action<HoldingStepData, HoldingEntryType> OnSectorSelected;

    /// <summary>Fired each time a Simulation step's aircraft reaches a checkpoint (pause point).</summary>
    public event Action<int> OnCheckpointReached;

    /// <summary>Fired once a Simulation step's aircraft has cleared every checkpoint and reached the end.</summary>
    public event Action OnSimulationStepComplete;

    /// <summary>Fired after the final step.</summary>
    public event Action OnAllStepsComplete;

    // ------------------------------------------------------------------
    // STATE
    // ------------------------------------------------------------------

    private int currentIndex;
    private bool[] voiceoverPlayed;
    private bool[] stepCompleted; // sector picked / simulation finished / familiarization VO done

    public int CurrentIndex => currentIndex;
    public int StepCount => steps.Count;

    public HoldingStepData CurrentStep =>
        currentIndex >= 0 && currentIndex < steps.Count ? steps[currentIndex] : null;

    public HoldingStepData GetStep(int index) =>
        index >= 0 && index < steps.Count ? steps[index] : null;

    public bool IsCompleted(int index) =>
        stepCompleted != null && index >= 0 && index < stepCompleted.Length && stepCompleted[index];

    public bool VoiceoverPlayed(int index) =>
        voiceoverPlayed != null && index >= 0 && index < voiceoverPlayed.Length && voiceoverPlayed[index];

    public void MarkVoiceoverPlayed(int index)
    {
        if (voiceoverPlayed != null && index >= 0 && index < voiceoverPlayed.Length)
            voiceoverPlayed[index] = true;
    }

    // ------------------------------------------------------------------
    // INITIALIZE
    // ------------------------------------------------------------------

    private void Start()
    {
        stepCompleted = new bool[steps.Count];
        voiceoverPlayed = new bool[steps.Count];

        ShowStep(0);
    }

    // ------------------------------------------------------------------
    // LOADING
    // ------------------------------------------------------------------

    public void ShowStep(int index)
    {
        if (index < 0 || index >= steps.Count)
            return;

        currentIndex = index;
        OnStepLoaded?.Invoke(steps[index], index);
    }

    public void NotifyVoiceoverComplete()
    {
        MarkVoiceoverPlayed(currentIndex);
        OnVoiceoverComplete?.Invoke();
    }

    // ------------------------------------------------------------------
    // SECTOR SELECT
    // ------------------------------------------------------------------

    /// <summary>Called by HoldingUIController when the (only interactable) sector button is clicked. Auto-advances immediately, mirroring "selecting an entry automatically transitions to the next page."</summary>
    public void SelectSector(HoldingEntryType sector)
    {
        HoldingStepData step = CurrentStep;
        if (step == null || step.stepType != HoldingStepType.SectorSelect)
            return;

        if (stepCompleted[currentIndex])
            return;

        stepCompleted[currentIndex] = true;
        OnSectorSelected?.Invoke(step, sector);

        GoNext();
    }

    // ------------------------------------------------------------------
    // SIMULATION
    // ------------------------------------------------------------------

    public void NotifyCheckpointReached(int checkpointIndex)
    {
        OnCheckpointReached?.Invoke(checkpointIndex);
    }

    /// <summary>Called by the simulation driver once every checkpoint on the current step is cleared and the aircraft has reached the end of its spline.</summary>
    public void NotifySimulationStepComplete()
    {
        if (currentIndex < 0 || currentIndex >= stepCompleted.Length)
            return;

        stepCompleted[currentIndex] = true;
        OnSimulationStepComplete?.Invoke();
    }

    // ------------------------------------------------------------------
    // NAVIGATION
    // ------------------------------------------------------------------

    public void GoNext()
    {
        HoldingStepData current = CurrentStep;
        if (current == null)
            return;

        // SectorSelect and Simulation steps must report completion before advancing.
        // Familiarization/Recap steps gate on voiceover instead (checked by UIController via
        // VoiceoverPlayed/UnlockNavigation - Next simply won't be interactable until then).
        bool requiresCompletion =
            current.stepType == HoldingStepType.SectorSelect ||
            current.stepType == HoldingStepType.Simulation;

        if (requiresCompletion && !IsCompleted(currentIndex))
            return;

        if (currentIndex >= steps.Count - 1)
        {
            OnAllStepsComplete?.Invoke();
            return;
        }

        ShowStep(currentIndex + 1);
    }

    public void GoPrevious()
    {
        if (currentIndex <= 0)
            return;

        ShowStep(currentIndex - 1);
    }
}