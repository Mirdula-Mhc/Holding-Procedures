// HoldingTimerPopup.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HoldingTimerPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Button startTimerButton;
    [SerializeField] private TextMeshProUGUI timerText; // or regular Text if not using TMP

    [Header("Timing")]
    [Tooltip("Real-time seconds it takes to simulate the 1-minute timer (e.g. 3 to 5 seconds).")]
    public float simulatedDuration = 4f;

    private Action onTimerFinished;

    private void Awake()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (startTimerButton != null)
            startTimerButton.onClick.AddListener(OnStartTimerClicked);
    }

    public void Show(Action onComplete)
    {
        onTimerFinished = onComplete;

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

        StartCoroutine(RunSimulatedCountdown());
    }

    private IEnumerator RunSimulatedCountdown()
    {
        float elapsed = 0f;

        while (elapsed < simulatedDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / simulatedDuration);

            // Simulates counting down from 60 seconds to 0 seconds
            int simulatedSecondsRemaining = Mathf.CeilToInt(Mathf.Lerp(60f, 0f, progress));
            if (timerText != null)
                timerText.text = $"00:{simulatedSecondsRemaining:D2}";

            yield return null;
        }

        if (timerText != null)
            timerText.text = "00:00";

        yield return new WaitForSeconds(0.5f);

        if (popupRoot != null)
            popupRoot.SetActive(false);

        onTimerFinished?.Invoke();
    }
}