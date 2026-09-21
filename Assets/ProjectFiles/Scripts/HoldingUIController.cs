using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HoldingUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HoldingScenarioManager scenarioManager;
    [SerializeField] private HoldingSimulationController simulationController;

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
    }

    private void OnDisable()
    {
        scenarioManager.OnStepLoaded -= HandleStepLoaded;
        scenarioManager.OnVoiceoverComplete -= HandleVoiceoverComplete;
        scenarioManager.OnSimulationStepComplete -= HandleSimulationStepComplete;
        scenarioManager.OnAllStepsComplete -= HandleAllStepsComplete;
    }

    // ==================================================
    // STEP LOADING
    // ==================================================

    private void HandleStepLoaded(HoldingStepData step, int index)
    {
        // Leaving any step must stop the simulation, its audio, knob and timer.
        if (simulationController != null)
            simulationController.StopEverything();

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
                ShowSimulationStep(step, index);
                break;
            case HoldingStepType.Recap:
                ShowRecapStep(step);
                break;
        }

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

    private void ShowSimulationStep(HoldingStepData step, int index)
    {
        simulationPanel.SetActive(true);
        UpdateInfoPanel(step);

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
    }
}