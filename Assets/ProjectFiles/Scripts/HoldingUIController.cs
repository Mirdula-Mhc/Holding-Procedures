using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HoldingUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HoldingScenarioManager scenarioManager;
    [SerializeField] private HoldingSimulationController simulationController;
    [SerializeField] private HoldingFamiliarizationController familiarizationController;

    [Header("Familiarization Panel")]
    [SerializeField] private GameObject familiarizationPanel;
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private GameObject hsiPanel;

    [Header("Info Panel UI")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TextMeshProUGUI infoText;

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

    [Header("Recap Panel")]
    [SerializeField] private GameObject recapPanel;
    [SerializeField] private TextMeshProUGUI recapText;

    [Header("Navigation")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;

    private bool guidedSequenceDone;
    private readonly HashSet<int> completedFamiliarizationSteps = new HashSet<int>();
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
        scenarioManager.OnSimulationStepComplete += HandleSimulationStepComplete;
        scenarioManager.OnAllStepsComplete += HandleAllStepsComplete;

        if (simulationController != null)
            simulationController.OnInfoTextChanged += HandleInfoTextChanged;

        if (familiarizationController != null)
            familiarizationController.OnSequenceComplete += HandleGuidedSequenceComplete;
    }

    private void OnDisable()
    {
        scenarioManager.OnStepLoaded -= HandleStepLoaded;
        scenarioManager.OnVoiceoverComplete -= HandleVoiceoverComplete;
        scenarioManager.OnSimulationStepComplete -= HandleSimulationStepComplete;
        scenarioManager.OnAllStepsComplete -= HandleAllStepsComplete;

        if (simulationController != null)
            simulationController.OnInfoTextChanged -= HandleInfoTextChanged;

        if (familiarizationController != null)
            familiarizationController.OnSequenceComplete -= HandleGuidedSequenceComplete;
    }

    // ==================================================
    // STEP LOADING
    // ==================================================

    private void HandleStepLoaded(HoldingStepData step, int index)
    {
        Debug.Log($"HandleStepLoaded: type={step.stepType}");
        if (simulationController != null)
            simulationController.StopEverything();

        if (familiarizationController != null)
            familiarizationController.StopSequence();

        HideAllPanels();

        switch (step.stepType)
        {
            case HoldingStepType.Familiarization:
                ShowFamiliarizationStep(step, index);
                break;
            case HoldingStepType.SectorSelect:
                ShowSectorSelectStep(step);
                break;
            case HoldingStepType.Simulation:
                ShowSimulationStep(step, index);
                break;
            case HoldingStepType.Recap:
                ShowRecapStep(step);
                break;
        }

        // Simulation steps get their info-panel state from the four beats (via
        // HandleInfoTextChanged). Every other step type uses the plain per-step text/flag.
        if (step.stepType != HoldingStepType.Simulation)
            UpdateInfoPanel(step);

        RefreshNavigation();
    }

    private void ShowFamiliarizationStep(HoldingStepData step, int index)
    {
        familiarizationPanel.SetActive(true);

        bool isMap = step.familiarizationPage == FamiliarizationPage.Map;
        if (mapPanel != null) mapPanel.SetActive(isMap);
        if (hsiPanel != null) hsiPanel.SetActive(!isMap);

        bool alreadyDone = completedFamiliarizationSteps.Contains(index);
        guidedSequenceDone = alreadyDone || step.highlightSequence == null || step.highlightSequence.Length == 0;

        if (familiarizationController != null && !guidedSequenceDone)
            familiarizationController.BeginSequence(step.highlightSequence);
    }

    private void ShowSectorSelectStep(HoldingStepData step)
    {
        sectorSelectPanel.SetActive(true);

        directButton.interactable = step.correctSector == HoldingEntryType.Direct;
        offsetButton.interactable = step.correctSector == HoldingEntryType.Offset;
        parallelButton.interactable = step.correctSector == HoldingEntryType.Parallel;
    }

    private void ShowSimulationStep(HoldingStepData step, int index)
    {
        simulationPanel.SetActive(true);

        bool alreadyCompleted = scenarioManager.IsCompleted(index);

        if (simulationController != null)
            simulationController.BeginStep(step, alreadyCompleted);
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
    // EVENTS
    // ==================================================

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

    private void HandleGuidedSequenceComplete()
    {
        guidedSequenceDone = true;
        completedFamiliarizationSteps.Add(scenarioManager.CurrentIndex);
        RefreshNavigation();
    }

    // Called by HoldingSimulationController at each beat (step start, pause, knob, timer, done).
    // Null means "hide the panel", a non-empty string updates the text and shows the panel
    // (only if this step actually has Show Info Panel enabled).
    private void HandleInfoTextChanged(string text)
    {
        if (infoPanel == null)
            return;

        HoldingStepData current = scenarioManager.CurrentStep;
        bool stepAllowsPanel = current != null && current.showInfoPanel;

        if (string.IsNullOrEmpty(text) || !stepAllowsPanel)
        {
            infoPanel.SetActive(false);
            return;
        }

        infoPanel.SetActive(true);
        if (infoText != null)
            infoText.text = text;
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

            case HoldingStepType.Familiarization:
                allowNext = guidedSequenceDone;
                break;

            default: // Recap
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
    }
}