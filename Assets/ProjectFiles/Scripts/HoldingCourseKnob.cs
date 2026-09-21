// HoldingCourseKnob.cs
//
// Rotary course-select knob. Direct mapping: knob mark pointing up = 000, right = 090.
// Relative drag: grabbing the knob anywhere keeps its current angle, and it only turns by how
// far the pointer moves around its center.
//
// While active, ONLY the HSI course pointer follows the knob. Heading, deviation, flags and DME
// are left untouched (the feeder is told to stop overwriting course via courseOverride).
// The Set button is hidden until the dialed course is within tolerance of the required course.

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class HoldingCourseKnob : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("UI References")]
    [Tooltip("Parent object shown/hidden with the knob (contains dial, readout and Set button).")]
    [SerializeField] private GameObject knobRoot;

    [Tooltip("The Image that visually rotates. Its pivot MUST be (0.5, 0.5). Put this script on the same object so it receives pointer events, with Raycast Target on.")]
    [SerializeField] private RectTransform knobDial;

    [SerializeField] private TextMeshProUGUI courseValueText;
    [SerializeField] private Button setButton;

    [Header("Target Instrument")]
    [SerializeField] private A320HSI hsi;

    [Header("Behaviour")]
    [Tooltip("Set button appears when the dialed course is within this many degrees of the required course.")]
    [SerializeField] private float toleranceDegrees = 5f;

    [Tooltip("Course snaps to this many degrees. 1 = whole-degree readout.")]
    [SerializeField] private int stepDegrees = 1;

    private float requiredCourse;
    private float currentAngle;      // continuous angle, 0..360
    private float lastPointerAngle;
    private bool dragging;
    private bool active;
    private Action onSet;

    private void Awake()
    {
        if (setButton != null)
            setButton.onClick.AddListener(OnSetClicked);

        if (knobRoot != null)
            knobRoot.SetActive(false);
    }

    /// <summary>Shows the knob, starting at the HSI's current course. onSetCallback fires when the user presses Set.</summary>
    public void Show(float required, Action onSetCallback)
    {
        requiredCourse = Wrap360(required);
        onSet = onSetCallback;
        active = true;
        dragging = false;

        currentAngle = hsi != null ? Wrap360(hsi.course) : 0f;
        ApplyVisuals();

        if (knobRoot != null)
            knobRoot.SetActive(true);
    }

    public void Hide()
    {
        active = false;
        dragging = false;
        onSet = null;

        if (knobRoot != null)
            knobRoot.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!active) return;

        if (TryGetPointerAngle(eventData, out float angle))
        {
            lastPointerAngle = angle;
            dragging = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!active || !dragging) return;

        if (!TryGetPointerAngle(eventData, out float angle))
            return;

        // Relative drag: turn the knob by how far the pointer moved around the center.
        float delta = Mathf.DeltaAngle(lastPointerAngle, angle);
        lastPointerAngle = angle;

        currentAngle = Wrap360(currentAngle + delta);
        ApplyVisuals();
    }

    private bool TryGetPointerAngle(PointerEventData eventData, out float angle)
    {
        angle = 0f;
        if (knobDial == null) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                knobDial, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        // Pointer sitting on the exact center has no defined angle.
        if (local.sqrMagnitude < 1f)
            return false;

        // Compass convention: 0 = up, clockwise positive.
        angle = Wrap360(Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg);
        return true;
    }

    private void ApplyVisuals()
    {
        float shown = SnappedCourse();

        if (knobDial != null)
            knobDial.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);

        if (courseValueText != null)
            courseValueText.text = Mathf.RoundToInt(shown).ToString("000") + "°";

        // Only the course pointer follows the knob; nothing else on the HSI is touched.
        if (hsi != null)
            hsi.course = shown;

        if (setButton != null)
            setButton.gameObject.SetActive(IsWithinTolerance(shown));
    }

    private float SnappedCourse()
    {
        if (stepDegrees <= 1)
            return Wrap360(Mathf.Round(currentAngle));

        return Wrap360(Mathf.Round(currentAngle / stepDegrees) * stepDegrees);
    }

    private bool IsWithinTolerance(float course)
    {
        return Mathf.Abs(Mathf.DeltaAngle(course, requiredCourse)) <= toleranceDegrees;
    }

    private void OnSetClicked()
    {
        if (!active) return;

        // Safety: Set is only visible in tolerance, but re-check in case of a stray click.
        if (!IsWithinTolerance(SnappedCourse()))
            return;

        Action done = onSet;
        Hide();
        done?.Invoke();
    }

    private static float Wrap360(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped < 0f) wrapped += 360f;
        return wrapped;
    }
}