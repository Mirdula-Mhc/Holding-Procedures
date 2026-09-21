// HoldingCourseKnob.cs
//
// Rotary course-select knob. Direct mapping: knob mark pointing up = 000, right = 090.
// Relative drag with a sensitivity multiplier, so it can be slowed down for touch.
//
// While active, ONLY the HSI course pointer follows the knob. Heading, deviation, flags and DME
// are left untouched (the feeder is told to freeze via courseOverride).
//
// The Submit button is always visible. Submitting the exact required course shows the "correct"
// panel for a few seconds, then reports success. Any other course shows the "wrong" panel for a
// few seconds and the knob stays for another try.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class HoldingCourseKnob : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("UI References")]
    [Tooltip("Parent object shown/hidden with the knob (contains dial, readout and Submit button).")]
    [SerializeField] private GameObject knobRoot;

    [Tooltip("The Image that visually rotates. Its pivot MUST be (0.5, 0.5). Put this script on the same object so it receives pointer events, with Raycast Target on.")]
    [SerializeField] private RectTransform knobDial;

    [Tooltip("Non-rotating RectTransform centered on the knob (e.g. the dial's parent). Pointer angles are measured against this so the reading doesn't spin with the dial.")]
    [SerializeField] private RectTransform pointerSpace;

    [SerializeField] private TextMeshProUGUI courseValueText;
    [SerializeField] private Button submitButton;

    [Header("Result Panels")]
    [Tooltip("Shown briefly after a correct submit. Not interactable, closes itself.")]
    [SerializeField] private GameObject correctPanel;

    [Tooltip("Shown briefly after a wrong submit. Closes itself, knob stays for a retry.")]
    [SerializeField] private GameObject wrongPanel;

    [Tooltip("Seconds the correct panel stays on screen.")]
    [SerializeField] private float correctPanelSeconds = 1.5f;

    [Tooltip("Seconds the wrong panel stays on screen.")]
    [SerializeField] private float wrongPanelSeconds = 1.5f;

    [Header("Target Instrument")]
    [SerializeField] private A320HSI hsi;

    [Header("Behaviour")]
    [Tooltip("How much the knob turns per degree the finger moves around it. 1 = follows the finger exactly, 0.3 = turns 30% as fast (finer control for touch).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float sensitivity = 0.35f;

    private float requiredCourse;
    private float currentAngle;      // continuous angle, 0..360
    private float lastPointerAngle;
    private bool dragging;
    private bool active;
    private bool resultShowing;
    private Action<float> onCorrect;
    private Coroutine panelRoutine;

    private void Awake()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitClicked);

        if (knobRoot != null)
            knobRoot.SetActive(false);

        if (correctPanel != null) correctPanel.SetActive(false);
        if (wrongPanel != null) wrongPanel.SetActive(false);
    }

    /// <summary>
    /// Shows the knob, starting at the HSI's current course. onCorrectCallback fires with the
    /// submitted course once the correct panel has finished showing.
    /// </summary>
    public void Show(float required, Action<float> onCorrectCallback)
    {
        requiredCourse = Wrap360(required);
        onCorrect = onCorrectCallback;
        active = true;
        dragging = false;
        resultShowing = false;

        currentAngle = hsi != null ? Wrap360(hsi.course) : 0f;
        ApplyVisuals();

        if (correctPanel != null) correctPanel.SetActive(false);
        if (wrongPanel != null) wrongPanel.SetActive(false);

        if (knobRoot != null)
            knobRoot.SetActive(true);
    }

    public void Hide()
    {
        active = false;
        dragging = false;
        resultShowing = false;
        onCorrect = null;

        if (panelRoutine != null)
        {
            StopCoroutine(panelRoutine);
            panelRoutine = null;
        }

        if (correctPanel != null) correctPanel.SetActive(false);
        if (wrongPanel != null) wrongPanel.SetActive(false);

        if (knobRoot != null)
            knobRoot.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!active || resultShowing) return;

        if (TryGetPointerAngle(eventData, out float angle))
        {
            lastPointerAngle = angle;
            dragging = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!active || !dragging || resultShowing) return;

        if (!TryGetPointerAngle(eventData, out float angle))
            return;

        // Relative drag: turn the knob by a fraction of how far the pointer moved around the center.
        float delta = Mathf.DeltaAngle(lastPointerAngle, angle);
        lastPointerAngle = angle;

        currentAngle = Wrap360(currentAngle + delta * sensitivity);
        ApplyVisuals();
    }

    private bool TryGetPointerAngle(PointerEventData eventData, out float angle)
    {
        angle = 0f;

        RectTransform space = pointerSpace != null ? pointerSpace : knobDial;
        if (space == null) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                space, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        if (local.sqrMagnitude < 1f)
            return false;

        // Compass convention: 0 = up, clockwise positive.
        angle = Wrap360(Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg);
        return true;
    }

    private void ApplyVisuals()
    {
        int shown = SnappedCourse();

        if (knobDial != null)
            knobDial.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);

        if (courseValueText != null)
            courseValueText.text = shown.ToString("000") + "°";

        // Only the course pointer follows the knob; nothing else on the HSI is touched.
        if (hsi != null)
            hsi.course = shown;
    }

    // Whole-degree course, 0..359.
    private int SnappedCourse()
    {
        return Mathf.RoundToInt(Wrap360(currentAngle)) % 360;
    }

    private void OnSubmitClicked()
    {
        if (!active || resultShowing)
            return;

        int submitted = SnappedCourse();
        int required = Mathf.RoundToInt(requiredCourse) % 360;

        if (submitted == required)
        {
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(CorrectRoutine(submitted));
        }
        else
        {
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(WrongRoutine());
        }
    }

    private IEnumerator CorrectRoutine(int submittedCourse)
    {
        resultShowing = true;
        dragging = false;

        if (correctPanel != null)
            correctPanel.SetActive(true);

        yield return new WaitForSeconds(correctPanelSeconds);

        if (correctPanel != null)
            correctPanel.SetActive(false);

        Action<float> done = onCorrect;
        Hide();
        done?.Invoke(submittedCourse);
    }

    private IEnumerator WrongRoutine()
    {
        resultShowing = true;
        dragging = false;

        if (wrongPanel != null)
            wrongPanel.SetActive(true);

        yield return new WaitForSeconds(wrongPanelSeconds);

        if (wrongPanel != null)
            wrongPanel.SetActive(false);

        resultShowing = false;
        panelRoutine = null;
    }

    private static float Wrap360(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped < 0f) wrapped += 360f;
        return wrapped;
    }
}