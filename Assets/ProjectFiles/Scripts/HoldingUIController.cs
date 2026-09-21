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

    [Header("Info Panel UI")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TextMeshProUGUI infoText;

    [Tooltip("Every highlightable element on the familiarization page, keyed by the id used in HoldingStepData.highlightElementId.")]
    [SerializeField] private List<HighlightElement> highlightElements;

    [Header("Sector Select Panel")]
    [SerializeField] private GameObject sectorSelectPanel;
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
    [SerializeField] private HoldingCourseKnob courseKnob;

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

    // ==================================================
    // STEP LOADING
    // ==================================================

    private void HandleStepLoaded(HoldingStepData step, int index)
    {
        StopAllCoroutines();

        // Leaving a step mid-knob or mid-timer must never leave anything stuck on screen.
        if (courseKnob != null) courseKnob.Hide();
        if (timerPopup != null) timerPopup.Hide();
        if (hsiFeeder != null) hsiFeeder.courseOverride = false;

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

        // Nav state is decided once, AFTER the step is fully set up.
        RefreshNavigation();
    }

    private void ShowFamiliarizationStep(HoldingStepData step)
    {
        familiarizationPanel.SetActive(true);
        familiarizationLabelText.text = step.familiarizationLabel;

        foreach (HighlightElement entry in highlightElements)
        {
            if (entry.element != null)
                entry.element.SetActive(entry.id == step.highlightElementId);
        }
    }

    private void ShowSectorSelectStep(HoldingStepData step)
    {
        sectorSelectPanel.SetActive(true);

        directButton.interactable = step.correctSector == HoldingEntryType.Direct;
        offsetButton.interactable = step.correctSector == HoldingEntryType.Offset;
        parallelButton.interactable = step.correctSector == HoldingEntryType.Parallel;
    }

    private void HandleSectorSelected(HoldingStepData step, HoldingEntryType sector)
    {
    }

    private void ShowSimulationStep(HoldingStepData step)
    {
        simulationPanel.SetActive(true);

        activeSimStep = step;
        nextCheckpointIndex = 0;

        UpdateInfoPanel(step);

        SplineContainer activeWorldSpline = ResolveSceneSpline(step.worldSpline);
        SplineContainer activeUiSpline = ResolveSceneSpline(step.uiSpline);

        bool alreadyCompleted = scenarioManager.IsCompleted(scenarioManager.CurrentIndex);

        if (hsiFeeder != null)
        {
            hsiFeeder.approachCourse = step.approachCourse;
            hsiFeeder.holdingInboundCourse = step.holdingInboundCourse;
            hsiFeeder.ResetFixCrossing();
        }

        if (aircraftMover != null && activeWorldSpline != null)
        {
            aircraftMover.speed = step.defaultCruiseSpeed;
            aircraftMover.SetSpline(activeWorldSpline);

            if (alreadyCompleted)
            {
                // Revisit: no flight, no knob, no timer. Park the aircraft at the end.
                aircraftMover.Pause();
                aircraftMover.SeekToNormalized(1f);
            }
            else
            {
                aircraftMover.Play();
            }
        }

        if (mapIconFollower != null && activeUiSpline != null)
            mapIconFollower.SetUiSpline(activeUiSpline);

        if (mapView != null)
        {
            mapView.ClearEntryPath();
            if (step.showDottedEntryPath && activeUiSpline != null)
                mapView.ShowEntryPath(activeUiSpline, step.entryPathEndT);
        }

        // Only watch checkpoints on a fresh run. On revisit nothing should trigger.
        if (!alreadyCompleted)
            StartCoroutine(WatchSimulationCheckpoints());
    }

    private void ShowRecapStep(HoldingStepData step)
    {
        recapPanel.SetActive(true);
        recapText.text = step.recapText;
    }

    private void UpdateInfoPanel(HoldingStepData step)
    {
        if (infoPanel == null) return;

        bool shouldShow = step != null && step.showInfoPanel;
        infoPanel.SetActive(shouldShow);

        if (shouldShow && infoText != null)
            infoText.text = step.infoPanelText;
    }

    // ==================================================
    // SIMULATION FLOW
    //
    // At every checkpoint:
    //   1. aircraft pauses
    //   2. course knob appears (HSI frozen except the course pointer)
    //   3. user sets the course and presses Set
    //   4. Start Timer popup appears
    //   5. user presses Start Timer -> aircraft flies the leg while the countdown runs
    // ==================================================

    private void HandleCheckpointReached(int checkpointIndex)
    {
        if (aircraftMover != null)
            aircraftMover.Pause();

        if (activeSimStep == null ||
            activeSimStep.checkpoints == null ||
            checkpointIndex < 0 ||
            checkpointIndex >= activeSimStep.checkpoints.Length)
        {
            Debug.LogWarning($"HoldingUIController: no checkpoint data for index {checkpointIndex}.");
            OnTimerStarted();
            return;
        }

        HoldingCheckpoint checkpoint = activeSimStep.checkpoints[checkpointIndex];

        if (courseKnob != null)
        {
            // Freeze the HSI: feeder stops updating everything, knob only drives the course pointer.
            if (hsiFeeder != null)
                hsiFeeder.courseOverride = true;

            courseKnob.Show(checkpoint.requiredCourse, OnCourseSet);
        }
        else
        {
            ShowTimerPopup();
        }
    }

    private void OnCourseSet()
    {
        // Hand the HSI back to the feeder, then show the timer popup.
        if (hsiFeeder != null)
            hsiFeeder.courseOverride = false;

        ShowTimerPopup();
    }

    private void ShowTimerPopup()
    {
        HoldingCheckpoint? checkpoint = GetCheckpoint(nextCheckpointIndex);

        if (timerPopup != null && checkpoint.HasValue)
        {
            timerPopup.Show(
                checkpoint.Value.timerMinutes,
                checkpoint.Value.simulatedDuration,
                OnTimerStarted,
                OnCheckpointTimerFinished);
        }
        else
        {
            OnTimerStarted();
        }
    }

    private void OnTimerStarted()
    {
        int currentLeg = nextCheckpointIndex;
        nextCheckpointIndex++;

        HoldingCheckpoint? checkpoint = GetCheckpoint(currentLeg);

        if (aircraftMover != null && activeSimStep != null)
        {
            if (checkpoint.HasValue)
            {
                float targetEndT = checkpoint.Value.legEndT;
                float currentT = aircraftMover.NormalizedT;

                float deltaT = targetEndT - currentT;
                if (deltaT < 0f && currentT > 0.9f)
                    deltaT += 1.0f;

                deltaT = Mathf.Max(0.01f, deltaT);

                SplineContainer spline = ResolveSceneSpline(activeSimStep.worldSpline);
                float totalLength = SplineUtility.CalculateLength(spline.Spline, spline.transform.localToWorldMatrix);
                float legDistance = deltaT * totalLength;

                // Auto-calculated leg speed: arrives at legEndT exactly as the countdown hits 00:00.
                aircraftMover.speed = legDistance / checkpoint.Value.simulatedDuration;
            }

            aircraftMover.Play();
        }

        StartCoroutine(WatchSimulationCheckpoints());
    }

    private void OnCheckpointTimerFinished()
    {
        if (aircraftMover != null && activeSimStep != null)
            aircraftMover.speed = activeSimStep.defaultCruiseSpeed;
    }

    private HoldingCheckpoint? GetCheckpoint(int index)
    {
        if (activeSimStep == null || activeSimStep.checkpoints == null)
            return null;

        if (index < 0 || index >= activeSimStep.checkpoints.Length)
            return null;

        return activeSimStep.checkpoints[index];
    }

    private SplineContainer ResolveSceneSpline(SplineContainer prefabOrSceneSpline)
    {
        if (prefabOrSceneSpline == null) return null;

        if (prefabOrSceneSpline.gameObject.scene.IsValid())
            return prefabOrSceneSpline;

        GameObject sceneObj = GameObject.Find(prefabOrSceneSpline.name);
        if (sceneObj != null && sceneObj.TryGetComponent(out SplineContainer sceneSpline))
            return sceneSpline;

        return prefabOrSceneSpline;
    }

    private IEnumerator WatchSimulationCheckpoints()
    {
        Debug.Log($"Watching. checkpoints={(activeSimStep.checkpoints == null ? -1 : activeSimStep.checkpoints.Length)} next={nextCheckpointIndex}");
        while (activeSimStep != null)
        {
            if (aircraftMover == null)
                yield break;

            HoldingCheckpoint[] checkpoints = activeSimStep.checkpoints;
            int count = checkpoints != null ? checkpoints.Length : 0;

            if (nextCheckpointIndex < count)
            {
                float target = checkpoints[nextCheckpointIndex].pauseAtT;
                if (aircraftMover.NormalizedT >= target)
                {
                    scenarioManager.NotifyCheckpointReached(nextCheckpointIndex);
                    yield break;
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
        RefreshNavigation();
    }

    private void HandleVoiceoverComplete()
    {
        RefreshNavigation();
    }

    private void HandleAllStepsComplete()
    {
        Debug.Log("Holding Procedures training complete.");
    }

    // ==================================================
    // NAVIGATION
    // ==================================================

    // Next: gated by the step's own completion rule.
    // Previous: enabled on every step except the first, but locked while a fresh sim is running.
    private void RefreshNavigation()
    {
        HoldingStepData current = scenarioManager.CurrentStep;
        if (current == null)
            return;

        int index = scenarioManager.CurrentIndex;
        bool allowNext;
        bool allowPrevious = index > 0;

        switch (current.stepType)
        {
            case HoldingStepType.SectorSelect:
                allowNext = scenarioManager.IsCompleted(index);
                break;

            case HoldingStepType.Simulation:
                allowNext = scenarioManager.IsCompleted(index);
                if (!allowNext)
                    allowPrevious = false;
                break;

            default: // Familiarization, Recap
                allowNext = scenarioManager.VoiceoverPlayed(index);
                break;
        }

        nextButton.interactable = allowNext;
        previousButton.interactable = allowPrevious;
    }

    private void HideAllPanels()
    {
        familiarizationPanel.SetActive(false);
        sectorSelectPanel.SetActive(false);
        simulationPanel.SetActive(false);
        recapPanel.SetActive(false);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        activeSimStep = null;
    }
}