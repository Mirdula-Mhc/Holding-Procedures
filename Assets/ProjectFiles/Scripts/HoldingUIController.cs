// HoldingUIController.cs
//
// Mirrors UIController.cs: subscribes to HoldingScenarioManager's events, shows/hides panels
// based on the current step's stepType, and is the ONLY thing that touches nextButton /
// previousButton .interactable (via UnlockNavigation - the single gatekeeper, same role as
// ATC's UIController.UnlockNavigation()). There is exactly one Next button and one Previous
// button for the whole flow - no per-step, no per-sector buttons.
//
// This script also owns wiring the current step's data into the existing simulation components
// (SplineAircraftMover, HoldingHsiFeeder, HoldingMapIconFollower, HoldingMapView,
// HoldingTimerPopup) - those components stay exactly as already built; this is the layer that
// tells them which scenario's values to use, same relationship UIController has to
// AnimationController/VideoController in ATC.

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;

public class HoldingUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HoldingScenarioManager scenarioManager;

    [Header("Familiarization Panel")]
    [SerializeField] private GameObject familiarizationPanel;
    [SerializeField] private TextMeshProUGUI familiarizationLabelText;

    [Tooltip("Every highlightable element on the familiarization page, keyed by the id used in HoldingStepData.highlightElementId. Only the matching entry is shown/highlighted per step.")]
    [SerializeField] private List<HighlightElement> highlightElements;

    [Header("Sector Select Panel")]
    [SerializeField] private GameObject sectorSelectPanel;
    [Tooltip("All three sector buttons - always visible together. Only the one matching the current step's correctSector is made interactable.")]
    [SerializeField] private Button directButton;
    public Image directImg;
    [SerializeField] private Button offsetButton;
    public Image offsetImg;
    [SerializeField] private Button parallelButton;
    public Image parallelImg;

    [Header("Simulation Panel")]
    [SerializeField] private GameObject simulationPanel;
    [SerializeField] private SplineAircraftMover aircraftMover;
    [SerializeField] private HoldingHsiFeeder hsiFeeder;
    [SerializeField] private HoldingMapIconFollower mapIconFollower;
    [SerializeField] private HoldingMapView mapView;
    [SerializeField] private HoldingTimerPopup timerPopup;

    [Header("Timing Calibration")]
    [Tooltip("Target T where the timing leg ends (start of the turn).")]
    [SerializeField] private float timingLegEndT = 0.45f;

    [Header("Recap Panel")]
    [SerializeField] private GameObject recapPanel;
    [SerializeField] private TextMeshProUGUI recapText;

    [Header("Navigation")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;

    [System.Serializable]
    public struct HighlightElement
    {
        public string id;
        public GameObject element;
    }

    // ------------------------------------------------------------------
    // Simulation runtime state (per current step)
    // ------------------------------------------------------------------
    private HoldingStepData activeSimStep;
    private int nextCheckpointIndex;

    private void Awake()
    {
        nextButton.onClick.AddListener(scenarioManager.GoNext);
        previousButton.onClick.AddListener(scenarioManager.GoPrevious);

        directButton.onClick.AddListener(() => scenarioManager.SelectSector(HoldingEntryType.Direct));
        offsetButton.onClick.AddListener(() => scenarioManager.SelectSector(HoldingEntryType.Offset));
        parallelButton.onClick.AddListener(() => scenarioManager.SelectSector(HoldingEntryType.Parallel));
    }

    private void Start()
    {
        directImg.alphaHitTestMinimumThreshold = 0.01f;
        offsetImg.alphaHitTestMinimumThreshold = 0.01f;
        parallelImg.alphaHitTestMinimumThreshold = 0.01f;
    }

    private void OnEnable()
    {
        scenarioManager.OnStepLoaded += HandleStepLoaded;
        scenarioManager.OnVoiceoverComplete += HandleVoiceoverComplete;
        scenarioManager.OnSectorSelected += HandleSectorSelected;
        scenarioManager.OnCheckpointReached += HandleCheckpointReached;
        scenarioManager.OnSimulationStepComplete += HandleSimulationStepComplete;
        scenarioManager.OnAllStepsComplete += HandleAllStepsComplete;
    }

    private void OnDisable()
    {
        scenarioManager.OnStepLoaded -= HandleStepLoaded;
        scenarioManager.OnVoiceoverComplete -= HandleVoiceoverComplete;
        scenarioManager.OnSectorSelected -= HandleSectorSelected;
        scenarioManager.OnCheckpointReached -= HandleCheckpointReached;
        scenarioManager.OnSimulationStepComplete -= HandleSimulationStepComplete;
        scenarioManager.OnAllStepsComplete -= HandleAllStepsComplete;
    }

    // ------------------------------------------------------------------
    // STEP LOADED
    // ------------------------------------------------------------------

    private void HandleStepLoaded(HoldingStepData step, int index)
    {
        StopAllCoroutines();
        HideAllPanels();

        switch (step.stepType)
        {
            case HoldingStepType.Familiarization:
                ShowFamiliarizationStep(step);
                break;

            case HoldingStepType.SectorSelect:
                ShowSectorSelectStep(step);
                break;

            case HoldingStepType.Simulation:
                ShowSimulationStep(step);
                break;

            case HoldingStepType.Recap:
                ShowRecapStep(step);
                break;
        }
    }

    // ------------------------------------------------------------------
    // FAMILIARIZATION
    // ------------------------------------------------------------------

    private void ShowFamiliarizationStep(HoldingStepData step)
    {
        familiarizationPanel.SetActive(true);
        familiarizationLabelText.text = step.familiarizationLabel;

        foreach (HighlightElement entry in highlightElements)
        {
            if (entry.element != null)
                entry.element.SetActive(entry.id == step.highlightElementId);
        }

        // Gated on voiceover finishing - HandleVoiceoverComplete calls UnlockNavigation().
        LockNavigation();
    }

    // ------------------------------------------------------------------
    // SECTOR SELECT
    // ------------------------------------------------------------------

    private void ShowSectorSelectStep(HoldingStepData step)
    {
        sectorSelectPanel.SetActive(true);

        // All three buttons stay visible; only the correct one is interactable.
        directButton.interactable = step.correctSector == HoldingEntryType.Direct;
        offsetButton.interactable = step.correctSector == HoldingEntryType.Offset;
        parallelButton.interactable = step.correctSector == HoldingEntryType.Parallel;

        // Selecting the sector auto-advances (see HoldingScenarioManager.SelectSector), so Next
        // is not needed on this page - keep it locked.
        LockNavigation();
    }

    private void HandleSectorSelected(HoldingStepData step, HoldingEntryType sector)
    {
        // SelectSector() already calls GoNext() itself - nothing to do here beyond
        // any visual acknowledgement you want to add later (e.g. a brief highlight flash).
    }

    // ------------------------------------------------------------------
    // SIMULATION
    // ------------------------------------------------------------------

    // Inside HoldingUIController.cs -> ShowSimulationStep()
    // Inside HoldingUIController.cs

    private void ShowSimulationStep(HoldingStepData step)
    {
        simulationPanel.SetActive(true);
        LockNavigation();

        activeSimStep = step;
        nextCheckpointIndex = 0;

        SplineContainer activeWorldSpline = ResolveSceneSpline(step.worldSpline);
        SplineContainer activeUiSpline = ResolveSceneSpline(step.uiSpline);

        if (aircraftMover != null && activeWorldSpline != null)
        {
            // Start flight at smooth cruise speed for turns
            aircraftMover.speed = step.defaultCruiseSpeed;
            aircraftMover.SetSpline(activeWorldSpline);
            aircraftMover.Play();
        }

        if (mapIconFollower != null && activeUiSpline != null)
            mapIconFollower.SetUiSpline(activeUiSpline);

        if (hsiFeeder != null)
        {
            hsiFeeder.approachCourse = step.approachCourse;
            hsiFeeder.holdingInboundCourse = step.holdingInboundCourse;
            hsiFeeder.ResetFixCrossing();
        }

        if (mapView != null)
        {
            mapView.ClearEntryPath();
            if (step.showDottedEntryPath && activeWorldSpline != null)
                mapView.ShowEntryPath(activeWorldSpline);
        }

        StartCoroutine(WatchSimulationCheckpoints());
    }

    private void HandleCheckpointReached(int checkpointIndex)
    {
        if (aircraftMover != null)
            aircraftMover.Pause();

        if (timerPopup != null && activeSimStep != null)
        {
            timerPopup.simulatedDuration = activeSimStep.simulatedTimerDuration;
            timerPopup.Show(OnTimerStarted, OnCheckpointTimerFinished);
        }
        else
        {
            OnTimerStarted();
        }
    }

    // Inside HoldingUIController.cs
    private void OnTimerStarted()
    {
        int currentLeg = nextCheckpointIndex;
        nextCheckpointIndex++;

        if (aircraftMover != null && activeSimStep != null)
        {
            if (activeSimStep.timingLegEndT != null && currentLeg < activeSimStep.timingLegEndT.Length)
            {
                float targetEndT = activeSimStep.timingLegEndT[currentLeg];
                float currentT = aircraftMover.NormalizedT;

                // Handle closed-loop wrap (if starting near 0.99/0.00)
                float deltaT = targetEndT - currentT;
                if (deltaT < 0f && currentT > 0.9f)
                    deltaT += 1.0f;

                deltaT = Mathf.Max(0.01f, deltaT);

                SplineContainer spline = ResolveSceneSpline(activeSimStep.worldSpline);
                float totalLength = SplineUtility.CalculateLength(spline.Spline, spline.transform.localToWorldMatrix);
                float legDistance = deltaT * totalLength;

                // Calculates the exact speed to reach targetEndT right at 00:00
                aircraftMover.speed = legDistance / activeSimStep.simulatedTimerDuration;
            }

            aircraftMover.Play();
        }

        StartCoroutine(WatchSimulationCheckpoints());
    }

    private void OnCheckpointTimerFinished()
    {
        // Timer reached 00:00: return to normal cruise speed for the curved turn
        if (aircraftMover != null && activeSimStep != null)
        {
            aircraftMover.speed = activeSimStep.defaultCruiseSpeed;
        }
    }

    // Helper: Finds the active scene GameObject that shares the prefab's name
    private SplineContainer ResolveSceneSpline(SplineContainer prefabOrSceneSpline)
    {
        if (prefabOrSceneSpline == null) return null;

        // If already a scene object, return it directly
        if (prefabOrSceneSpline.gameObject.scene.IsValid())
            return prefabOrSceneSpline;

        // If it's a prefab asset from the Project tab, find the active instance in the Hierarchy
        GameObject sceneObj = GameObject.Find(prefabOrSceneSpline.name);
        if (sceneObj != null && sceneObj.TryGetComponent(out SplineContainer sceneSpline))
        {
            return sceneSpline;
        }

        return prefabOrSceneSpline;
    }

    private IEnumerator WatchSimulationCheckpoints()
    {
        while (activeSimStep != null)
        {
            if (aircraftMover == null)
                yield break;

            if (nextCheckpointIndex < activeSimStep.pauseAtProgressT.Length)
            {
                float target = activeSimStep.pauseAtProgressT[nextCheckpointIndex];
                if (aircraftMover.NormalizedT >= target)
                {
                    scenarioManager.NotifyCheckpointReached(nextCheckpointIndex);
                    yield break; // HandleCheckpointReached restarts this loop after the timer
                }
            }
            else if (aircraftMover.ReachedEnd)
            {
                scenarioManager.NotifySimulationStepComplete();
                yield break;
            }

            yield return null;
        }
    }

    private void HandleSimulationStepComplete()
    {
        UnlockNavigation();
    }


    // ------------------------------------------------------------------
    // RECAP
    // ------------------------------------------------------------------

    private void ShowRecapStep(HoldingStepData step)
    {
        recapPanel.SetActive(true);
        recapText.text = step.recapText;
        UnlockNavigation();
    }

    // ------------------------------------------------------------------
    // VOICEOVER
    // ------------------------------------------------------------------

    private void HandleVoiceoverComplete()
    {
        UnlockNavigation();
    }

    // ------------------------------------------------------------------
    // FINISH
    // ------------------------------------------------------------------

    private void HandleAllStepsComplete()
    {
        Debug.Log("Holding Procedures training complete.");
    }

    // ------------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------------

    private void LockNavigation()
    {
        nextButton.interactable = false;
        previousButton.interactable = false;
    }

    /// <summary>Single gatekeeper for turning Next/Previous back on - mirrors ATC's UIController.UnlockNavigation(). Always call this instead of setting the buttons directly.</summary>
    private void UnlockNavigation()
    {
        HoldingStepData current = scenarioManager.CurrentStep;
        if (current == null)
            return;

        bool requiresCompletion =
            current.stepType == HoldingStepType.SectorSelect ||
            current.stepType == HoldingStepType.Simulation;

        bool allowNext = !requiresCompletion || scenarioManager.IsCompleted(scenarioManager.CurrentIndex);

        nextButton.interactable = allowNext;
        previousButton.interactable = scenarioManager.CurrentIndex > 0;
    }

    private void HideAllPanels()
    {
        familiarizationPanel.SetActive(false);
        sectorSelectPanel.SetActive(false);
        simulationPanel.SetActive(false);
        recapPanel.SetActive(false);

        activeSimStep = null;
    }
}