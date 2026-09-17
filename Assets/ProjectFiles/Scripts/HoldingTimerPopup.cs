// HoldingTimerPopup.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class HoldingTimerPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Button startTimerButton;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Timing")]
    [Tooltip("Real-time seconds it takes to simulate the 1-minute timer (e.g. 3 to 5 seconds).")]
    public float simulatedDuration = 4f;

    private Action onTimerStarted;
    private Action onTimerFinished;
    private Coroutine countdownRoutine;

    private void Awake()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (startTimerButton != null)
            startTimerButton.onClick.AddListener(OnStartTimerClicked);
    }

    /// <summary>
    /// Opens the popup and sets up the countdown.
    /// If only one action is passed, it fires immediately on button click so flight resumes while the timer ticks down.
    /// </summary>
    public void Show(Action onStart, Action onComplete = null)
    {
        onTimerStarted = onStart;
        onTimerFinished = onComplete;

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (startTimerButton != null)
        {
            startTimerButton.gameObject.SetActive(true);
            startTimerButton.interactable = true;
        }

        if (timerText != null)
            timerText.text = "01:00";
    }

    private void OnStartTimerClicked()
    {
        if (startTimerButton != null)
            startTimerButton.gameObject.SetActive(false);

        // 1. Resume aircraft motion and advance checkpoint index immediately
        onTimerStarted?.Invoke();

        // 2. Run countdown visuals in parallel
        countdownRoutine = StartCoroutine(RunSimulatedCountdown());
    }

    private IEnumerator RunSimulatedCountdown()
    {
        float elapsed = 0f;

        while (elapsed < simulatedDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / simulatedDuration);

            int simulatedSecondsRemaining = Mathf.CeilToInt(Mathf.Lerp(60f, 0f, progress));
            if (timerText != null)
                timerText.text = $"00:{simulatedSecondsRemaining:D2}";

            yield return null;
        }

        if (timerText != null)
            timerText.text = "00:00";

        yield return new WaitForSeconds(0.4f);

        if (popupRoot != null)
            popupRoot.SetActive(false);

        countdownRoutine = null;
        onTimerFinished?.Invoke();
    }

    private void OnDisable()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
    }
}