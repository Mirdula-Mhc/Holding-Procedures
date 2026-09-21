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

    private float simulatedDuration = 4f;
    private int timerMinutes = 1;

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
    /// Opens the popup. timerMinutes is the displayed length (e.g. 2 shows 02:00 counting down);
    /// simulatedDuration is how many real seconds the countdown actually takes.
    /// onStart fires when the user presses Start Timer (aircraft resumes); onComplete fires at 00:00.
    /// </summary>
    public void Show(int timerMinutes, float simulatedDuration, Action onStart, Action onComplete = null)
    {
        this.timerMinutes = Mathf.Max(1, timerMinutes);
        this.simulatedDuration = Mathf.Max(0.5f, simulatedDuration);
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
            timerText.text = FormatTime(this.timerMinutes * 60);
    }

    /// <summary>Closes the popup and stops any running countdown.</summary>
    public void Hide()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        if (popupRoot != null)
            popupRoot.SetActive(false);
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
        int totalSeconds = timerMinutes * 60;
        float elapsed = 0f;

        while (elapsed < simulatedDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / simulatedDuration);

            int secondsRemaining = Mathf.CeilToInt(Mathf.Lerp(totalSeconds, 0f, progress));
            if (timerText != null)
                timerText.text = FormatTime(secondsRemaining);

            yield return null;
        }

        if (timerText != null)
            timerText.text = FormatTime(0);

        yield return new WaitForSeconds(0.4f);

        if (popupRoot != null)
            popupRoot.SetActive(false);

        countdownRoutine = null;
        onTimerFinished?.Invoke();
    }

    private static string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
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