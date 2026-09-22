// HoldingFamiliarizationController.cs
//
// Guides the user through a map/HSI page one part at a time: highlights a part (glow + scale),
// waits for a tap, shows an explanation panel with title/body/audio, and moves to the next part
// once the audio finishes. Fires OnSequenceComplete after the last part.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HoldingFamiliarizationController : MonoBehaviour
{
    [System.Serializable]
    public struct HighlightableSceneElement
    {
        [Tooltip("Must match a HoldingHighlightPart.id on the step asset.")]
        public string id;
        public RectTransform visual;
        public Image visualImage;
        public Button tapButton;
    }

    [Header("Scene Elements")]
    [SerializeField] private List<HighlightableSceneElement> elements;

    [Header("Dim Overlay")]
    [Tooltip("Flat semi-transparent panel covering the whole map. Sits above the background, below every highlightable element.")]
    [SerializeField] private GameObject dimOverlay;

    [Header("Explanation Panel")]
    [SerializeField] private GameObject explanationPanel;
    [SerializeField] private TextMeshProUGUI explanationTitleText;
    [SerializeField] private TextMeshProUGUI explanationBodyText;
    [SerializeField] private AudioSource explanationAudioSource;

    [Header("Glow / Pulse")]
    [Tooltip("Extra scale applied to the glow copy relative to the original element's scale.")]
    [SerializeField] private float glowScaleMultiplier = 1.2f;
    [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.3f, 0.6f);
    [Tooltip("How much the highlighted element itself scales up while active.")]
    [SerializeField] private float highlightScaleMultiplier = 1.15f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.15f;

    public event Action OnSequenceComplete;

    private HoldingHighlightPart[] parts;
    private int currentIndex;
    private Coroutine pulseRoutine;
    private Coroutine waitAudioRoutine;
    private GameObject activeGlow;
    private Vector3 activeElementOriginalScale;
    private RectTransform activeElementTransform;

    private void Awake()
    {
        if (explanationPanel != null)
            explanationPanel.SetActive(false);

        if (dimOverlay != null)
            dimOverlay.SetActive(false);

        foreach (var e in elements)
        {
            if (e.tapButton != null)
                e.tapButton.interactable = false;
        }
    }

    /// <summary>Starts the guided sequence for a step's highlightSequence list.</summary>
    public void BeginSequence(HoldingHighlightPart[] sequence)
    {
        StopSequence();

        parts = sequence;
        currentIndex = 0;

        if (parts == null || parts.Length == 0)
            return;

        if (dimOverlay != null)
            dimOverlay.SetActive(true);

        ActivateCurrent();
    }

    /// <summary>Stops the sequence and clears all highlight/panel state. Call when leaving the step.</summary>
    public void StopSequence()
    {
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
        if (waitAudioRoutine != null) { StopCoroutine(waitAudioRoutine); waitAudioRoutine = null; }

        ClearActiveHighlight();

        if (explanationPanel != null)
            explanationPanel.SetActive(false);

        if (explanationAudioSource != null && explanationAudioSource.isPlaying)
            explanationAudioSource.Stop();

        if (dimOverlay != null)
            dimOverlay.SetActive(false);

        foreach (var e in elements)
        {
            if (e.tapButton != null)
                e.tapButton.interactable = false;
        }

        parts = null;
        currentIndex = 0;
    }

    // ==================================================
    // SEQUENCE STEP
    // ==================================================

    private void ActivateCurrent()
    {
        if (parts == null || currentIndex >= parts.Length)
        {
            // Sequence finished.
            if (dimOverlay != null)
                dimOverlay.SetActive(false);

            OnSequenceComplete?.Invoke();
            return;
        }

        HoldingHighlightPart part = parts[currentIndex];
        int elementIndex = FindElement(part.id);

        if (elementIndex < 0)
        {
            Debug.LogWarning($"HoldingFamiliarizationController: no scene element with id '{part.id}'. Skipping.");
            currentIndex++;
            ActivateCurrent();
            return;
        }

        HighlightableSceneElement element = elements[elementIndex];

        // Only this element's button is tappable.
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].tapButton != null)
                elements[i].tapButton.interactable = (i == elementIndex);
        }

        if (element.tapButton != null)
        {
            element.tapButton.onClick.RemoveAllListeners();
            element.tapButton.onClick.AddListener(() => OnElementTapped(part));
        }

        ShowHighlight(element);
    }

    private void OnElementTapped(HoldingHighlightPart part)
    {
        // Stop the pulse and prevent re-tapping while the explanation is up.
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }

        int elementIndex = FindElement(part.id);
        if (elementIndex >= 0 && elements[elementIndex].tapButton != null)
            elements[elementIndex].tapButton.interactable = false;

        ShowExplanation(part);
    }

    private void ShowExplanation(HoldingHighlightPart part)
    {
        if (explanationPanel != null)
        {
            explanationPanel.SetActive(true);

            if (explanationTitleText != null)
                explanationTitleText.text = part.title;

            if (explanationBodyText != null)
                explanationBodyText.text = part.bodyText;
        }

        if (waitAudioRoutine != null)
            StopCoroutine(waitAudioRoutine);

        waitAudioRoutine = StartCoroutine(PlayExplanationAudio(part.explanationAudio));
    }

    private IEnumerator PlayExplanationAudio(AudioClip clip)
    {
        if (explanationAudioSource != null && clip != null)
        {
            explanationAudioSource.clip = clip;
            explanationAudioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
        else
        {
            // No clip assigned - don't block testing, give a short beat so the panel is readable.
            yield return new WaitForSeconds(1.5f);
        }

        waitAudioRoutine = null;

        if (explanationPanel != null)
            explanationPanel.SetActive(false);

        ClearActiveHighlight();

        currentIndex++;
        ActivateCurrent();
    }

    // ==================================================
    // HIGHLIGHT / GLOW
    // ==================================================

    private void ShowHighlight(HighlightableSceneElement element)
    {
        if (element.visual == null)
            return;

        // Reparent the dim overlay under this element's own panel, so it dims that panel's
        // background while sitting below the highlighted element in draw order.
        if (dimOverlay != null && element.visual.parent != null)
        {
            dimOverlay.transform.SetParent(element.visual.parent, false);
            dimOverlay.transform.SetAsFirstSibling(); // bottom of this panel, above the plain background
        }

        activeElementTransform = element.visual;
        activeElementOriginalScale = element.visual.localScale;
        element.visual.localScale = activeElementOriginalScale * highlightScaleMultiplier;

        element.visual.SetAsLastSibling();
        // Glow copy: same sprite, tinted, larger, placed directly behind the element.
        if (element.visualImage != null)
        {
            activeGlow = new GameObject($"{element.id}_Glow", typeof(RectTransform), typeof(Image));
            activeGlow.transform.SetParent(element.visual.parent, false);

            RectTransform glowRt = activeGlow.GetComponent<RectTransform>();
            glowRt.anchorMin = element.visual.anchorMin;
            glowRt.anchorMax = element.visual.anchorMax;
            glowRt.pivot = element.visual.pivot;
            glowRt.anchoredPosition = element.visual.anchoredPosition;
            glowRt.sizeDelta = element.visual.sizeDelta;
            glowRt.localScale = activeElementOriginalScale * glowScaleMultiplier;

            Image glowImg = activeGlow.GetComponent<Image>();
            glowImg.sprite = element.visualImage.sprite;
            glowImg.color = glowColor;
            glowImg.raycastTarget = false;

            // Behind the element, but still above the dim overlay/background.
            activeGlow.transform.SetSiblingIndex(element.visual.GetSiblingIndex());
        }

        pulseRoutine = StartCoroutine(PulseGlow());
    }

    private IEnumerator PulseGlow()
    {
        float t = 0f;

        while (true)
        {
            t += Time.deltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(t) * pulseAmount;

            if (activeGlow != null)
                activeGlow.transform.localScale = activeElementOriginalScale * glowScaleMultiplier * pulse;

            yield return null;
        }
    }

    private void ClearActiveHighlight()
    {
        if (activeElementTransform != null)
        {
            activeElementTransform.localScale = activeElementOriginalScale;
            activeElementTransform = null;
        }

        if (activeGlow != null)
        {
            Destroy(activeGlow);
            activeGlow = null;
        }
    }

    private int FindElement(string id)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].id == id)
                return i;
        }
        return -1;
    }
}