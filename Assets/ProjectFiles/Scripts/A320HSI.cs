// A320HSI.cs
//
// Generates a complete ROSE-VOR-style Navigation Display (HSI) entirely at runtime - no manually
// created UI objects required. Attach to any empty GameObject and press Play.
//
// This is a 1:1 geometric port of the reference "ROSE VOR Simulator" HTML/SVG page: every
// coordinate below is taken directly from that page's 300x300 SVG viewBox (center at 150,150,
// outer tick radius 122, dashed range ring radius 60) and mapped into Unity UI space at a fixed
// scale, so ring radii, dot positions, arrow geometry, and the aircraft symbol match the source
// exactly rather than being visually approximated.
//
// Everything (Canvas, fixed dashed/solid range rings, rotating compass card with ticks/numbers,
// rotating course pointer with an open TO chevron + filled FROM arrow + deviation bar + 4
// reference dots, fixed lubber line, fixed aircraft symbol, heading box, VOR1/VOR2 idents/DME,
// wind, mode/range text, and the red NAV flag) is built once and then driven every frame purely
// by updating cached RectTransform/Text/Image references - no hierarchy searches, no per-frame
// instantiation.
//
// Mirrors the architecture of A320PFD.cs in this project: same Awake()/Update() split, same
// SmoothDamp-based needle easing (the reference page snaps instantly; this adds smoothing to
// feel native to a moving simulation, exactly like A320PFD does over the raw slider inputs),
// same CreateRect/CreateText/SetRect helpers, same Editor-only "Bake HSI To Scene" context menu
// for turning the runtime-generated preview into a permanent, hand-editable part of the scene.
//
// PERSISTING THE GENERATED UI: pressing Play and letting Awake() generate it is a temporary
// PREVIEW only - Unity always discards anything created during Play Mode the moment Play stops,
// and no script can override that. To make the generated HSI a permanent part of the scene that
// you can hand-edit afterward, use this component's context menu (gear icon in the Inspector, or
// right-click the component header) -> "Bake HSI To Scene", while NOT in Play Mode. That builds
// the exact same hierarchy directly into the Edit Mode scene (fully Undo-able), marks the scene
// dirty, and sets Is Baked so this component will never regenerate or touch it again - from then
// on the generated Canvas/Text/Image objects are ordinary scene objects you can reposition,
// resize, or restyle by hand like anything else.
//
// ROSE CARD CONVENTION (per the reference page): the compass card rotates by -Heading and the
// course pointer rotates by (Course - Heading), exactly matching the reference's
// `rotate(${-state.heading} 150 150)` / `rotate(${state.course - state.heading} 150 150)`
// transforms. At certain headings the rose numbers legitimately render upside-down under
// rotation - that matches the real instrument (and the reference page says so explicitly in its
// footer) and is not something this script corrects for.

using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class A320HSI : MonoBehaviour
{
    // ==================================================
    // Reference SVG space -> Unity UI space
    // ==================================================
    // The source page's SVG viewBox is 0..300 on both axes, center (150,150), +Y pointing DOWN
    // (SVG convention). Unity's UI anchoredPosition has +Y pointing UP. Every constant below is
    // copied verbatim from the SVG markup as an offset FROM the 150,150 center, then converted
    // with Y negated - so, e.g., the SVG's cx=129/cy=150 reference dot becomes (-21, 0) here
    // (129-150 = -21 on X, 150-150 = 0 on Y, no sign flip needed since it's already 0), while the
    // lubber line's y=14..26 span (above center, smaller Y = more "up" in SVG) becomes a positive
    // Unity Y (further up), matching the same visual position.
    private const float SvgCenter = 150f;

    // ==================================================
    // Inspector - Aircraft
    // ==================================================
    [Header("Aircraft")]
    [Tooltip("Magnetic heading, degrees. Drives compass card rotation and the heading box readout.")]
    [Range(0f, 359.99f)] public float heading = 270f;

    [Tooltip("How quickly the displayed compass card/pointers/bars settle toward their target values. The reference HTML page snaps instantly; this adds SmoothDamp easing (same role as A320PFD's Smooth Duration) so it reads naturally as a live simulation rather than a slider demo. Set very low (e.g. 0.02) for an instant-snap feel identical to the reference page.")]
    [Min(0.01f)] public float smoothDuration = 0.35f;

    [Header("Wind (display only)")]
    [Tooltip("Reference page hardcodes this HUD readout to 245°/14 regardless of any other control - kept as an editable field here since a simulator will usually want it live, but defaults to match the reference exactly.")]
    public float windDirection = 245f;
    public float windSpeedKts = 14f;

    // ==================================================
    // Inspector - Nav Radio 1 (Course Deviation Indicator)
    // ==================================================
    public enum NavFlag { TO, OFF, FROM }

    [Header("Nav Radio 1 - CDI")]
    [Tooltip("Selected course, degrees. Drives the rotating course pointer.")]
    [Range(0f, 359.99f)] public float course = 93f;

    [Tooltip("Manual deviation offset (degrees left/right of course) for training purposes - this is a direct offset, not a computed radial, so any course/deviation/flag combination can be demonstrated on demand. Matches the reference page's DEV slider exactly (range -10..+10).")]
    [Range(-10f, 10f)] public float deviation = 3.0f;

    [Tooltip("Station-relative-to-nose flag. TO shows an open chevron at the top of the course line, FROM shows a filled arrow at the bottom, OFF dims the deviation bar to 15% and raises the red NAV flag - exactly matching the reference page's three-way segmented control.")]
    public NavFlag navFlag = NavFlag.TO;

    [Tooltip("Deviation range mapped to the bar's full-scale travel. Fixed at 10 in the reference page (its slider's own min/max) - the bar's pixel travel (maxPx=42 in SVG units) is a separate, fixed constant below, matching the source exactly.")]
    [Min(0.01f)] public float maxDeviationDegrees = 10f;

    public string ident1 = "CLT";
    [Range(108f, 117.95f)] public float freq1 = 115.00f;

    [Tooltip("Slant range, nautical miles.")]
    [Range(0f, 300f)] public float dme1 = 26.4f;

    // ==================================================
    // Inspector - Nav Radio 2
    // ==================================================
    [Header("Nav Radio 2")]
    public string ident2 = "SPA";
    [Range(108f, 117.95f)] public float freq2 = 112.30f;
    [Range(0f, 300f)] public float dme2 = 31.2f;

    // ==================================================
    // Inspector - Display Range
    // ==================================================
    public enum DisplayRange { NM10 = 10, NM20 = 20, NM40 = 40, NM80 = 80 }

    [Header("Display Range")]
    public DisplayRange displayRange = DisplayRange.NM20;

    // ==================================================
    // Inspector - Display Settings
    // ==================================================
    [Header("Parent / Container")]
    [Tooltip("Leave empty to generate a full-screen overlay Canvas (default). Assign a RectTransform (e.g. a panel already under an existing Canvas) to generate the HSI as a child of it instead - no new Canvas is created, and everything is clipped strictly to its own bounds so it can never draw outside that parent.")]
    [SerializeField] private RectTransform parentContainer;

    [Header("Display Settings")]
    [Tooltip("On-screen pixel size of the square instrument face. The internal geometry is authored in the reference page's 300x300 SVG units and scaled to fit this - so changing this only scales the whole instrument uniformly, it never re-lays-out anything.")]
    public Vector2 hsiSize = new Vector2(700f, 700f);

    // Colors - copied from the reference page's CSS custom properties (:root tokens).
    public Color screenColor = new Color(0.012f, 0.039f, 0.024f);       // --screen #030a06
    public Color ndGreen = new Color(0.180f, 0.910f, 0.471f);           // --nd-green #2ee878
    public Color ndCyan = new Color(0.345f, 0.843f, 0.925f);            // --nd-cyan #58d7ec
    public Color ndWhite = new Color(0.933f, 0.957f, 0.949f);           // --nd-white #eef4f2
    public Color ndYellow = new Color(1f, 0.835f, 0.290f);              // --nd-yellow #ffd54a
    public Color warnRed = new Color(1f, 0.302f, 0.302f);               // --warn-red #ff4d4d
    public Color inkTwo = new Color(0.392f, 0.451f, 0.447f);            // --ink-2 #647372
    public Color inkOne = new Color(0.624f, 0.690f, 0.682f);            // --ink-1 #9fb0ae

    [Header("Text Sizes")]
    [Min(1)] public int roseNumberFontSize = 15;
    [Min(1)] public int headingBoxFontSize = 20;
    [Min(1)] public int hudLabelFontSize = 12;
    [Min(1)] public int hudValueFontSize = 15;
    [Min(1)] public int identFontSize = 18;
    [Min(1)] public int dmeFontSize = 20;
    [Min(1)] public int flagFontSize = 13;

    // ==================================================
    // Bake state
    // ==================================================
    [Header("Bake")]
    [Tooltip("True once this HSI has been baked into permanent scene objects (via the 'Bake HSI To Scene' context menu action, in Edit Mode). While true, this component will never regenerate/rebuild the hierarchy - it only drives the existing baked objects using the reference fields below, which Unity serializes normally like any other Inspector reference.")]
    [SerializeField] private bool isBaked = false;

    // ==================================================
    // Runtime-generated references (cached, never searched for)
    // ==================================================
    [HideInInspector][SerializeField] private Canvas canvas;
    [HideInInspector][SerializeField] private RectTransform hsiRoot;
    [HideInInspector][SerializeField] private RectTransform screenArea;
    [HideInInspector][SerializeField] private float scale; // pixels per SVG unit - hsiSize.x / 300

    // Rotating compass card
    [HideInInspector][SerializeField] private RectTransform rosePivot;

    // Rotating course pointer group
    [HideInInspector][SerializeField] private RectTransform coursePivot;
    [HideInInspector][SerializeField] private RectTransform devBar;
    [HideInInspector][SerializeField] private RectTransform toChevron; // always-visible open tip chevron, never toggled
    [HideInInspector][SerializeField] private RectTransform toArrowFilled; // opacity-toggled filled TO arrowhead
    [HideInInspector][SerializeField] private RectTransform fromArrow;

    // Fixed overlays
    [HideInInspector][SerializeField] private RectTransform rangeRingOuter;
    [HideInInspector][SerializeField] private RectTransform rangeRingInner;

    // HUD text
    [HideInInspector][SerializeField] private Text windValueText;
    [HideInInspector][SerializeField] private Text modeRangeText;
    [HideInInspector][SerializeField] private Text headingBoxText;
    [HideInInspector][SerializeField] private Text navSelFreqText;
    [HideInInspector][SerializeField] private Text navSelCrsText;
    [HideInInspector][SerializeField] private Text ident1Text;
    [HideInInspector][SerializeField] private Text dme1Text;
    [HideInInspector][SerializeField] private Text ident2Text;
    [HideInInspector][SerializeField] private Text dme2Text;
    [HideInInspector][SerializeField] private Text rangeLabelText;
    [HideInInspector][SerializeField] private RectTransform offFlagGroup;

    // Smoothing state - serialized too, so a baked HSI's needles don't reset to 0 and jump on
    // the next domain reload; harmless to serialize even for non-baked runtime-only use.
    [HideInInspector][SerializeField] private float displayHeading;
    [HideInInspector][SerializeField] private float headingVelocity;
    [HideInInspector][SerializeField] private float displayCourse;
    [HideInInspector][SerializeField] private float courseVelocity;
    [HideInInspector][SerializeField] private float displayDeviation;
    [HideInInspector][SerializeField] private float devVelocity;

    private static Font cachedFont;

    // ==================================================
    // Lifecycle
    // ==================================================
    private void Awake()
    {
        if (isBaked)
        {
            // Already baked - every reference field above already points at the existing,
            // permanent scene hierarchy via normal Unity serialization. Nothing to
            // (re)generate - Update() drives it directly.
            return;
        }

        displayHeading = heading;
        displayCourse = course;
        displayDeviation = deviation;

        BuildHSI();

        // NOTE: this only marks the in-memory Play Mode instance as built, so repeated Awake()
        // calls within the same session don't double-generate. It does NOT persist past Stop -
        // Unity always discards anything created during Play when Play Mode ends, with no
        // script-level way around that. Real persistence requires baking in Edit Mode - see
        // BakeToScene() below.
        isBaked = true;
    }

    private void Update()
    {
        if (hsiRoot == null)
            return;

        UpdateRose();
        UpdateCoursePointer();
        UpdateDeviationBar();
        UpdateFlag();
        UpdateHud();
    }

#if UNITY_EDITOR
    [ContextMenu("Bake HSI To Scene")]
    private void BakeToScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[A320HSI] Bake must be run in Edit Mode, not Play Mode - Unity always discards anything created during Play when you stop, with no way around that from a script. Exit Play Mode, then use 'Bake HSI To Scene' again from this component's context menu.", this);
            return;
        }

        if (isBaked)
        {
            Debug.LogWarning("[A320HSI] Already baked - this GameObject's HSI is already a permanent part of the scene. If you want to regenerate from scratch, delete the generated hierarchy under it by hand first, then untick Is Baked before baking again.", this);
            return;
        }

        displayHeading = heading;
        displayCourse = course;
        displayDeviation = deviation;

        BuildHSI();
        isBaked = true;

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log("[A320HSI] Baked - the generated HSI is now a permanent part of the scene (Ctrl+S to save it). You can freely edit/reposition/resize any of the generated objects by hand from now on; this component will not regenerate or touch them again.", this);
    }
#endif

    private void OnDestroy()
    {
        if (isBaked)
            return; // baked objects are a permanent, independent part of the scene now - this
                    // component no longer owns their lifecycle, even if it's removed/deleted.

        // Own-Canvas mode: destroying the Canvas takes hsiRoot with it. Parented mode: no Canvas
        // was created, so hsiRoot itself is the thing to clean up (without touching the parent).
        if (canvas != null)
            Destroy(canvas.gameObject);
        else if (hsiRoot != null)
            Destroy(hsiRoot.gameObject);
    }

    // ==================================================
    // Build
    // ==================================================
    private void BuildHSI()
    {
        scale = hsiSize.x / 300f;

        BuildCanvas();
        BuildRootPanel();
        BuildRangeRings();
        BuildCompassCard();
        BuildCoursePointer();
        BuildLubberLineAndAircraft();
        BuildHud();
    }

    private void BuildCanvas()
    {
        // If a parent container was assigned, the HSI builds as a child of it instead - no new
        // Canvas is created (the parent is assumed to already live under one), and canvas stays
        // null so OnDestroy() knows to clean up hsiRoot directly instead.
        if (parentContainer != null)
            return;

        GameObject canvasGO = new GameObject("A320HSI_Canvas");
        RegisterCreatedObjectForUndo(canvasGO);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildRootPanel()
    {
        Transform parent = parentContainer != null ? (Transform)parentContainer : canvas.transform;

        hsiRoot = CreateRect("HSI_Root", parent, hsiSize, Vector2.zero);

        // Guarantees nothing generated below can ever render outside hsiRoot's own rect,
        // regardless of whether it ended up under a full-screen Canvas or a small parent panel.
        hsiRoot.gameObject.AddComponent<RectMask2D>();

        // Circular CRT-style screen face. The reference page's .screen-frame is a rounded
        // SQUARE (border-radius:16px), not a circle - the round look comes entirely from the
        // instrument geometry (rings/rose) sitting inside it, so this background is a plain
        // square panel, not a masked circle.
        screenArea = CreateRect("Screen_Area", hsiRoot, hsiSize, Vector2.zero);
        Image screenBg = screenArea.gameObject.AddComponent<Image>();
        screenBg.color = screenColor;
        screenBg.raycastTarget = false;
    }

    // Converts an SVG-space point (as authored in the reference markup, e.g. cx="129" cy="150")
    // into a Unity UI anchoredPosition relative to the 150,150 center, honoring the Y-flip
    // between SVG (+Y down) and Unity UI (+Y up), then scaling to the instrument's actual size.
    private Vector2 Svg(float svgX, float svgY)
    {
        return new Vector2((svgX - SvgCenter) * scale, (SvgCenter - svgY) * scale);
    }

    // Same conversion for a length/size (no center subtraction, no axis flip needed since sizes
    // are always positive magnitudes in both spaces).
    private float SvgLen(float len) => len * scale;

    // -------------------- Fixed range rings --------------------
    // <circle cx="150" cy="150" r="60" .../> dashed, and <circle cx="150" cy="150" r="122" .../>
    // solid faint - both distance-based, do not rotate with heading.
    private void BuildRangeRings()
    {
        float innerR = SvgLen(60f);
        float outerR = SvgLen(122f);

        rangeRingInner = CreateRect("Range_Ring_Inner", screenArea, new Vector2(innerR * 2f, innerR * 2f), Vector2.zero);
        Image innerImg = rangeRingInner.gameObject.AddComponent<Image>();
        innerImg.sprite = GetDashedRingSprite();
        innerImg.color = new Color(ndCyan.r, ndCyan.g, ndCyan.b, 0.55f);
        innerImg.raycastTarget = false;

        rangeRingOuter = CreateRect("Range_Ring_Outer", screenArea, new Vector2(outerR * 2f, outerR * 2f), Vector2.zero);
        Image outerImg = rangeRingOuter.gameObject.AddComponent<Image>();
        outerImg.sprite = GetRingSprite();
        outerImg.color = new Color(ndCyan.r, ndCyan.g, ndCyan.b, 0.35f);
        outerImg.raycastTarget = false;
    }

    // -------------------- Rotating compass card --------------------
    // Reference: rOuter=122, rInner = major?104:(mid?110:115), rad=(deg-90)*PI/180 (0deg = up),
    // major tick stroke-width 1.6 opacity 1, minor 1 opacity .6. Numbers at rText=96, rotated by
    // `deg` about their own anchor point (i.e. they rotate WITH the card).
    private void BuildCompassCard()
    {
        rosePivot = CreateRect("Rose_Pivot", screenArea, hsiSize, Vector2.zero);

        for (int deg = 0; deg < 360; deg += 5)
        {
            bool major = deg % 30 == 0;
            bool mid = deg % 10 == 0;

            float rOuter = 122f;
            float rInner = major ? 104f : (mid ? 110f : 115f);
            float rad = (deg - 90f) * Mathf.Deg2Rad;

            // SVG: x = cx + r*cos(rad), y = cy + r*sin(rad). Convert both endpoints, then this
            // script's own Svg() helper handles the +Y-down -> +Y-up flip consistently.
            Vector2 p1 = Svg(SvgCenter + rOuter * Mathf.Cos(rad), SvgCenter + rOuter * Mathf.Sin(rad));
            Vector2 p2 = Svg(SvgCenter + rInner * Mathf.Cos(rad), SvgCenter + rInner * Mathf.Sin(rad));
            Vector2 mid_ = (p1 + p2) * 0.5f;
            float tickLen = Vector2.Distance(p1, p2);

            // The tick's own local rotation must point it from p2 to p1. Unity UI angle: 0 deg
            // is "up" (+Y) for a rect's un-rotated height axis, matching -deg exactly the way
            // the reference's rotate(deg) does for a shape drawn pointing along +Y-up (SVG's
            // radial direction at angle (deg-90) already is that direction), so a plain -deg
            // z-rotation reproduces it.
            RectTransform tick = CreateRect($"Tick_{deg}", rosePivot, new Vector2(SvgLen(major ? 1.6f : 1f), tickLen), mid_);
            tick.localRotation = Quaternion.Euler(0f, 0f, -deg);
            Image ti = tick.gameObject.AddComponent<Image>();
            ti.color = new Color(ndWhite.r, ndWhite.g, ndWhite.b, major ? 1f : 0.6f);
            ti.raycastTarget = false;

            if (major)
            {
                string label = deg == 0 ? "36" : (deg / 10).ToString("00");
                float rText = 96f;
                Vector2 textPos = Svg(SvgCenter + rText * Mathf.Cos(rad), SvgCenter + rText * Mathf.Sin(rad));

                Text t = CreateText($"RoseNum_{deg}", rosePivot, label, roseNumberFontSize, ndWhite, TextAnchor.MiddleCenter, FontStyle.Normal);
                SetRect(t.rectTransform, new Vector2(SvgLen(30f), SvgLen(20f)), textPos);
                // Reference: transform="rotate(${deg} ${tx} ${ty})" - rotated about its OWN
                // anchor point, by +deg in SVG's clockwise-positive space, which is -deg in
                // Unity's counter-clockwise-positive space.
                t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -deg);
            }
        }
    }

    // -------------------- Rotating course pointer / CDI --------------------
    // Reference geometry (all in SVG units, center 150,150):
    //   Top halo/line:    x=150, y1=34  y2=90,  widths 7(.22 alpha)/2
    //   Bottom halo/line: x=150, y1=210 y2=266, widths 7(.22 alpha)/2
    //   TO chevron (open, unfilled): polyline 141,49 150,31 159,49 - stroke width 2, round caps
    //   FROM arrow (filled): polygon 150,227 143,210 157,210
    //   Reference dots (r=2.6, stroke only): (129,150) (171,150) (108,150) (192,150)
    //   Dev bar: halo width 9 (.25 alpha) + core width 4, both x=150 y1=90 y2=210 (120 units long)
    //   TO arrow shown in the reference's <polygon id="toArrow"> is actually points
    //   "150,73 143,90 157,90" (filled) - the OPEN chevron above is a separate, always-visible
    //   decoration at the very tip. Both exist in the source; reproduced faithfully below as two
    //   separate elements: the always-visible open chevron near y=31-49, and the opacity-toggled
    //   filled TO arrowhead at y=73-90 (mirrouring the filled FROM arrowhead at y=210-227).
    private void BuildCoursePointer()
    {
        coursePivot = CreateRect("Course_Pivot", screenArea, hsiSize, Vector2.zero);

        // Top halo + line (150,34)-(150,90)
        BuildCourseLineSegment("Top", 150f, 34f, 150f, 90f);
        // Bottom halo + line (150,210)-(150,266)
        BuildCourseLineSegment("Bottom", 150f, 210f, 150f, 266f);

        // Open, unfilled chevron at the very tip - always visible regardless of TO/FROM/OFF,
        // matching the reference's separate always-drawn <polyline>.
        toChevron = BuildOpenChevron("Course_Tip_Chevron", 141f, 49f, 150f, 31f, 159f, 49f);

        // Reference dots - exact SVG coordinates, no scaling approximation.
        BuildReferenceDot("RefDot_L1", 129f, 150f);
        BuildReferenceDot("RefDot_R1", 171f, 150f);
        BuildReferenceDot("RefDot_L2", 108f, 150f);
        BuildReferenceDot("RefDot_R2", 192f, 150f);

        // Deviation bar: halo (width 9, alpha .25) + core (width 4), fixed 120-unit length from
        // y=90 to y=210, slides horizontally as one group.
        Vector2 devCenter = Svg(150f, (90f + 210f) * 0.5f);
        float devLen = SvgLen(210f - 90f);
        devBar = CreateRect("Dev_Bar", coursePivot, new Vector2(SvgLen(9f), devLen), devCenter);
        Image devHalo = devBar.gameObject.AddComponent<Image>();
        devHalo.color = new Color(ndCyan.r, ndCyan.g, ndCyan.b, 0.25f);
        devHalo.raycastTarget = false;
        RectTransform devCore = CreateRect("Dev_Bar_Core", devBar, new Vector2(SvgLen(4f), devLen), Vector2.zero);
        Image devCoreImg = devCore.gameObject.AddComponent<Image>();
        devCoreImg.color = ndCyan;
        devCoreImg.raycastTarget = false;

        // Filled TO arrowhead: polygon 150,73 143,90 157,90 (apex up at y=73, base at y=90).
        toArrowFilled = BuildFilledTriangle("To_Arrow_Filled", coursePivot, 150f, 73f, 143f, 90f, 157f, 90f, ndCyan);
        toArrowFilled.localRotation = Quaternion.Euler(0f, 0f, 180f);

        // Filled FROM arrowhead: polygon 150,227 143,210 157,210 (apex down at y=227, base at y=210).
        fromArrow = BuildFilledTriangle("From_Arrow", coursePivot, 150f, 227f, 143f, 210f, 157f, 210f, ndCyan);
        fromArrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
    }

    private void BuildCourseLineSegment(string name, float x1, float y1, float x2, float y2)
    {
        Vector2 center = Svg(x1, (y1 + y2) * 0.5f);
        float len = SvgLen(Mathf.Abs(y2 - y1));

        RectTransform halo = CreateRect($"Course_Line_{name}_Halo", coursePivot, new Vector2(SvgLen(7f), len), center);
        Image haloImg = halo.gameObject.AddComponent<Image>();
        haloImg.color = new Color(ndCyan.r, ndCyan.g, ndCyan.b, 0.22f);
        haloImg.raycastTarget = false;

        RectTransform line = CreateRect($"Course_Line_{name}", coursePivot, new Vector2(SvgLen(2f), len), center);
        Image lineImg = line.gameObject.AddComponent<Image>();
        lineImg.color = ndCyan;
        lineImg.raycastTarget = false;
    }

    private void BuildReferenceDot(string name, float svgX, float svgY)
    {
        Vector2 pos = Svg(svgX, svgY);
        float d = SvgLen(2.6f) * 2f;
        RectTransform dot = CreateRect(name, coursePivot, new Vector2(d, d), pos);
        Image dotImg = dot.gameObject.AddComponent<Image>();
        dotImg.sprite = GetReferenceDotRingSprite();
        dotImg.color = new Color(ndWhite.r * 0.75f, ndWhite.g * 0.75f, ndWhite.b * 0.75f, 1f);
        dotImg.raycastTarget = false;
    }

    // An open, unfilled 2-segment chevron (polyline of 3 points, round-capped stroke) -
    // reproduced as two thin rotated bars meeting at the apex, since Unity UI has no native
    // polyline primitive.
    private RectTransform BuildOpenChevron(string name, float x1, float y1, float apexX, float apexY, float x3, float y3)
    {
        RectTransform group = CreateRect(name, coursePivot, hsiSize, Vector2.zero);

        Vector2 p1 = Svg(x1, y1);
        Vector2 apex = Svg(apexX, apexY);
        Vector2 p3 = Svg(x3, y3);
        float strokeW = SvgLen(2f);

        BuildChevronArm($"{name}_L", p1, apex, strokeW);
        BuildChevronArm($"{name}_R", p3, apex, strokeW);

        return group;

        void BuildChevronArm(string armName, Vector2 from, Vector2 to, float width)
        {
            // Plain stretched bar for each of the two chevron arms - visually matches the
            // reference's straight stroke segments; the polyline's round line-join/cap at this
            // stroke width is a minor cosmetic detail a plain bar already reads as, so no
            // separate cap sprite is needed.
            Vector2 mid = (from + to) * 0.5f;
            float len = Vector2.Distance(from, to);
            float angle = Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;

            RectTransform arm = CreateRect(armName, group, new Vector2(width, len), mid);
            arm.localRotation = Quaternion.Euler(0f, 0f, -angle);
            Image armImg = arm.gameObject.AddComponent<Image>();
            armImg.color = ndCyan;
            armImg.raycastTarget = false;
        }
    }

    private RectTransform BuildFilledTriangle(string name, Transform parent, float x1, float y1, float x2, float y2, float x3, float y3, Color color)
    {
        Vector2 p1 = Svg(x1, y1);
        Vector2 p2 = Svg(x2, y2);
        Vector2 p3 = Svg(x3, y3);

        Vector2 min = Vector2.Min(p1, Vector2.Min(p2, p3));
        Vector2 max = Vector2.Max(p1, Vector2.Max(p2, p3));
        Vector2 size = max - min;
        Vector2 center = (min + max) * 0.5f;

        RectTransform rt = CreateRect(name, parent, size, center);
        Image img = rt.gameObject.AddComponent<Image>();

        // p1 is the apex (narrow end); determine whether it points up or down within this rect
        // to pick the matching procedural triangle sprite.
        bool apexIsTop = p1.y > center.y;
        img.sprite = apexIsTop ? GetTriangleUpSprite() : GetTriangleDownSprite();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // -------------------- Fixed lubber line + aircraft symbol --------------------
    // Reference: lubber = polygon 150,26 144,14 156,14 (filled triangle, apex down at y=26,
    // base at y=14 - i.e. pointing DOWN toward the rose, at the top of the screen).
    // Aircraft: cross at center - vertical line y139-161 (22 long), horizontal line x129-171
    // (42 long), stroke-width 2.4, plus a filled circle r=3.4 at the very center.
    private void BuildLubberLineAndAircraft()
    {
        RectTransform lubberLine = BuildFilledTriangle("Lubber_Line", screenArea, 150f, 26f, 144f, 14f, 156f, 14f, ndYellow);
        lubberLine.localRotation = Quaternion.Euler(0f, 0f, 180f);

        RectTransform aircraft = CreateRect("Aircraft_Symbol", screenArea, hsiSize, Vector2.zero);

        Vector2 vTop = Svg(150f, 139f);
        Vector2 vBot = Svg(150f, 161f);
        RectTransform vLine = CreateRect("AC_Vertical", aircraft, new Vector2(SvgLen(2.4f), Vector2.Distance(vTop, vBot)), (vTop + vBot) * 0.5f);
        Image vImg = vLine.gameObject.AddComponent<Image>();
        vImg.color = ndYellow;
        vImg.raycastTarget = false;

        Vector2 hLeft = Svg(129f, 150f);
        Vector2 hRight = Svg(171f, 150f);
        RectTransform hLine = CreateRect("AC_Horizontal", aircraft, new Vector2(Vector2.Distance(hLeft, hRight), SvgLen(2.4f)), (hLeft + hRight) * 0.5f);
        Image hImg = hLine.gameObject.AddComponent<Image>();
        hImg.color = ndYellow;
        hImg.raycastTarget = false;

        float centerD = SvgLen(3.4f) * 2f;
        RectTransform center = CreateRect("AC_Center", aircraft, new Vector2(centerD, centerD), Svg(150f, 150f));
        Image centerImg = center.gameObject.AddComponent<Image>();
        centerImg.sprite = GetCircleSprite();
        centerImg.color = ndYellow;
        centerImg.raycastTarget = false;
    }

    // -------------------- HUD overlays --------------------
    // Reference .hud positions are CSS percentages of the (square) screen-frame box:
    //   .wind        top:5%   left:6%
    //   .mode        top:9.5% left:6%
    //   .navsel      top:5%   right:5%   (text-align right)
    //   .hdgbox      top:2.2% centered horizontally
    //   .navbox.left bottom:6% left:4%   width:30%
    //   .navbox.right bottom:6% right:4% width:30% (text-align right)
    //   .rangelabel  bottom:2.5% centered horizontally
    //   .offflag     top:38% centered both axes
    // Converted below to Unity anchoredPosition offsets from hsiRoot's center, using
    // top/left/bottom/right percentages of hsiSize the same way the CSS percentages work
    // against the square screen-frame's own box.
    private void BuildHud()
    {
        float w = hsiSize.x;
        float h = hsiSize.y;
        float halfW = w * 0.5f;
        float halfH = h * 0.5f;

        // .wind: top 5%, left 6% -> anchored from top-left corner
        RectTransform windBlock = CreateRect("Hud_Wind", hsiRoot, new Vector2(140f, 20f), TopLeft(w, h, 0.06f, 0.05f, 140f, 20f));
        windValueText = CreateText("Hud_WindText", windBlock, "245°/14", hudValueFontSize, ndGreen, TextAnchor.MiddleLeft);
        SetRect(windValueText.rectTransform, new Vector2(140f, 20f), Vector2.zero);

        // .mode: top 9.5%, left 6%
        modeRangeText = CreateText("Hud_ModeRange", hsiRoot, "ROSE VOR  RNG 20", hudLabelFontSize, ndWhite, TextAnchor.MiddleLeft);
        SetRect(modeRangeText.rectTransform, new Vector2(180f, 18f), TopLeft(w, h, 0.06f, 0.095f, 180f, 18f));

        // .hdgbox: top 2.2%, centered horizontally, black box with white border
        Vector2 hdgPos = new Vector2(0f, halfH - h * 0.022f - 14f);
        RectTransform hdgBoxBg = CreateRect("Hud_HdgBox_Bg", hsiRoot, new Vector2(90f, 26f), hdgPos);
        Image hdgBg = hdgBoxBg.gameObject.AddComponent<Image>();
        hdgBg.color = Color.black;
        hdgBg.raycastTarget = false;
        Outline hdgOutline = hdgBoxBg.gameObject.AddComponent<Outline>();
        hdgOutline.effectColor = ndWhite;
        hdgOutline.effectDistance = new Vector2(1f, 1f);
        headingBoxText = CreateText("Hud_HdgBoxText", hdgBoxBg, "HDG 270", headingBoxFontSize, ndWhite, TextAnchor.MiddleCenter);
        SetRect(headingBoxText.rectTransform, new Vector2(86f, 22f), Vector2.zero);

        // .navsel: top 5%, right 5%, text-align right
        RectTransform navSelBlock = CreateRect("Hud_NavSel", hsiRoot, new Vector2(160f, 40f), TopRight(w, h, 0.05f, 0.05f, 160f, 40f));
        navSelFreqText = CreateText("Hud_NavSelFreq", navSelBlock, "VOR1 115.00", hudValueFontSize, ndWhite, TextAnchor.UpperRight);
        SetRect(navSelFreqText.rectTransform, new Vector2(160f, 18f), new Vector2(0f, 10f));
        navSelCrsText = CreateText("Hud_NavSelCrs", navSelBlock, "CRS 093°", hudValueFontSize, ndCyan, TextAnchor.UpperRight);
        SetRect(navSelCrsText.rectTransform, new Vector2(160f, 18f), new Vector2(0f, -10f));

        // .navbox.left: bottom 6%, left 4%, width 30%
        RectTransform navBox1 = CreateRect("Hud_NavBox1", hsiRoot, new Vector2(w * 0.30f, 60f), BottomLeft(w, h, 0.04f, 0.06f, w * 0.30f, 60f));
        Text navLabel1 = CreateText("Hud_NavLabel1", navBox1, "VOR1", hudLabelFontSize, inkTwo, TextAnchor.UpperLeft);
        SetRect(navLabel1.rectTransform, new Vector2(w * 0.30f, 16f), new Vector2(0f, 22f));
        ident1Text = CreateText("Hud_Ident1", navBox1, ident1, identFontSize, ndGreen, TextAnchor.UpperLeft);
        SetRect(ident1Text.rectTransform, new Vector2(w * 0.30f, 20f), new Vector2(0f, 0f));
        dme1Text = CreateText("Hud_Dme1", navBox1, "26.4 NM", dmeFontSize, ndGreen, TextAnchor.UpperLeft);
        SetRect(dme1Text.rectTransform, new Vector2(w * 0.30f, 22f), new Vector2(0f, -22f));

        // .navbox.right: bottom 6%, right 4%, width 30%, text-align right
        RectTransform navBox2 = CreateRect("Hud_NavBox2", hsiRoot, new Vector2(w * 0.30f, 60f), BottomRight(w, h, 0.04f, 0.06f, w * 0.30f, 60f));
        Text navLabel2 = CreateText("Hud_NavLabel2", navBox2, "VOR2", hudLabelFontSize, inkTwo, TextAnchor.UpperRight);
        SetRect(navLabel2.rectTransform, new Vector2(w * 0.30f, 16f), new Vector2(0f, 22f));
        ident2Text = CreateText("Hud_Ident2", navBox2, ident2, identFontSize, ndGreen, TextAnchor.UpperRight);
        SetRect(ident2Text.rectTransform, new Vector2(w * 0.30f, 20f), new Vector2(0f, 0f));
        dme2Text = CreateText("Hud_Dme2", navBox2, "31.2 NM", dmeFontSize, ndGreen, TextAnchor.UpperRight);
        SetRect(dme2Text.rectTransform, new Vector2(w * 0.30f, 22f), new Vector2(0f, -22f));

        // .rangelabel: bottom 2.5%, centered horizontally
        rangeLabelText = CreateText("Hud_RangeLabel", hsiRoot, "20", hudValueFontSize, ndCyan, TextAnchor.MiddleCenter);
        SetRect(rangeLabelText.rectTransform, new Vector2(60f, 18f), new Vector2(0f, -halfH + h * 0.025f + 9f));

        // .offflag: top 38%, centered both axes - red diagonal-striped block, hidden unless OFF.
        Vector2 flagPos = new Vector2(0f, halfH - h * 0.38f);
        offFlagGroup = CreateRect("Hud_OffFlag", hsiRoot, new Vector2(110f, 24f), flagPos);
        Image flagBg = offFlagGroup.gameObject.AddComponent<Image>();
        flagBg.color = warnRed;
        flagBg.raycastTarget = false;
        Text offFlagText = CreateText("Hud_OffFlagText", offFlagGroup, "NAV FLAG", flagFontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(offFlagText.rectTransform, new Vector2(106f, 20f), Vector2.zero);
    }

    // CSS-percentage-of-box -> Unity anchoredPosition-from-center helpers, matching how
    // top/left/right/bottom percentages position an absolutely-positioned child within the
    // reference page's square .screen-frame box.
    private static Vector2 TopLeft(float w, float h, float leftPct, float topPct, float blockW, float blockH)
        => new Vector2(-w * 0.5f + w * leftPct + blockW * 0.5f, h * 0.5f - h * topPct - blockH * 0.5f);

    private static Vector2 TopRight(float w, float h, float rightPct, float topPct, float blockW, float blockH)
        => new Vector2(w * 0.5f - w * rightPct - blockW * 0.5f, h * 0.5f - h * topPct - blockH * 0.5f);

    private static Vector2 BottomLeft(float w, float h, float leftPct, float bottomPct, float blockW, float blockH)
        => new Vector2(-w * 0.5f + w * leftPct + blockW * 0.5f, -h * 0.5f + h * bottomPct + blockH * 0.5f);

    private static Vector2 BottomRight(float w, float h, float rightPct, float bottomPct, float blockW, float blockH)
        => new Vector2(w * 0.5f - w * rightPct - blockW * 0.5f, -h * 0.5f + h * bottomPct + blockH * 0.5f);

    // ==================================================
    // Update
    // ==================================================
    private void UpdateRose()
    {
        displayHeading = Mathf.SmoothDampAngle(displayHeading, heading, ref headingVelocity, smoothDuration);
        rosePivot.localRotation = Quaternion.Euler(0f, 0f, displayHeading);
    }

    private void UpdateCoursePointer()
    {
        displayCourse = Mathf.SmoothDampAngle(displayCourse, course, ref courseVelocity, smoothDuration);

        // Reference: rotate(${state.course - state.heading} 150 150) in SVG's clockwise-positive
        // space -> negate for Unity's counter-clockwise-positive space.
        float relativeAngle = displayCourse - displayHeading;
        coursePivot.localRotation = Quaternion.Euler(0f, 0f, -relativeAngle);
    }

    private void UpdateDeviationBar()
    {
        displayDeviation = Mathf.SmoothDamp(displayDeviation, deviation, ref devVelocity, smoothDuration);

        // Reference: maxDeg=10, maxPx=42 (fixed SVG-unit constant, independent of maxDeviationDegrees
        // being adjustable) - off = clamp(dev,-10,10)/10 * 42, translate(off, 0).
        float clamped = Mathf.Clamp(displayDeviation, -maxDeviationDegrees, maxDeviationDegrees);
        float offsetSvgUnits = (clamped / maxDeviationDegrees) * 42f;
        float offsetPx = SvgLen(offsetSvgUnits);
        devBar.anchoredPosition = new Vector2(offsetPx, devBar.anchoredPosition.y);
    }

    private void UpdateFlag()
    {
        bool showTo = navFlag == NavFlag.TO;
        bool showFrom = navFlag == NavFlag.FROM;
        bool showOff = navFlag == NavFlag.OFF;

        SetImageAlpha(toArrowFilled, showTo ? 1f : 0f);
        SetImageAlpha(fromArrow, showFrom ? 1f : 0f);

        CanvasGroup devGroup = devBar.GetComponent<CanvasGroup>();
        if (devGroup == null)
            devGroup = devBar.gameObject.AddComponent<CanvasGroup>();
        devGroup.alpha = showOff ? 0.15f : 1f;

        offFlagGroup.gameObject.SetActive(showOff);
    }

    private void UpdateHud()
    {
        int roundedHeading = Mathf.RoundToInt(Wrap360(heading)) % 360;
        int roundedCourse = Mathf.RoundToInt(Wrap360(course)) % 360;
        int range = (int)displayRange;

        string hdgStr = "HDG " + roundedHeading.ToString("000");
        if (headingBoxText.text != hdgStr)
            headingBoxText.text = hdgStr;

        string windStr = Mathf.RoundToInt(Wrap360(windDirection)).ToString("000") + "°/" + Mathf.RoundToInt(windSpeedKts);
        if (windValueText.text != windStr)
            windValueText.text = windStr;

        string modeStr = "ROSE VOR  RNG " + range;
        if (modeRangeText.text != modeStr)
            modeRangeText.text = modeStr;

        string freqStr = "VOR1 " + freq1.ToString("0.00");
        if (navSelFreqText.text != freqStr)
            navSelFreqText.text = freqStr;

        string crsStr = "CRS " + roundedCourse.ToString("000") + "°";
        if (navSelCrsText.text != crsStr)
            navSelCrsText.text = crsStr;

        string id1 = string.IsNullOrEmpty(ident1) ? "---" : ident1.ToUpperInvariant();
        if (ident1Text.text != id1)
            ident1Text.text = id1;

        string dme1Str = dme1.ToString("0.0") + " NM";
        if (dme1Text.text != dme1Str)
            dme1Text.text = dme1Str;

        string id2 = string.IsNullOrEmpty(ident2) ? "---" : ident2.ToUpperInvariant();
        if (ident2Text.text != id2)
            ident2Text.text = id2;

        string dme2Str = dme2.ToString("0.0") + " NM";
        if (dme2Text.text != dme2Str)
            dme2Text.text = dme2Str;

        string rangeStr = range.ToString();
        if (rangeLabelText.text != rangeStr)
            rangeLabelText.text = rangeStr;
    }

    // ==================================================
    // Helpers
    // ==================================================
    private static float Wrap360(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped < 0f)
            wrapped += 360f;
        return wrapped;
    }

    private static void SetImageAlpha(RectTransform rt, float alpha)
    {
        if (rt == null)
            return;
        Image img = rt.GetComponent<Image>();
        if (img == null)
            return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RegisterCreatedObjectForUndo(go);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        SetRect(rt, size, anchoredPos);
        return rt;
    }

    // In Edit Mode (i.e. during an Editor Bake), every generated object is registered with the
    // Undo system as it's created, so the entire bake is a single Ctrl+Z-able action instead of
    // leaving behind objects Undo doesn't know about. No-op during Play Mode (Undo doesn't apply
    // there, and UnityEditor isn't compiled into device builds at all).
    private void RegisterCreatedObjectForUndo(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Generate A320 HSI");
#endif
    }

    private void SetRect(RectTransform rt, Vector2 size, Vector2 anchoredPos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RegisterCreatedObjectForUndo(go);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = GetDefaultFont();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Procedurally generates a single filled circle sprite (once, cached/reused for any size via
    // RectTransform scaling - Image stretches it to fill whatever rect it's on). A ~1.5px soft
    // alpha edge keeps it from looking jagged/pixelated when scaled up to the instrument's size.
    private static Sprite cachedCircleSprite;
    private static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null)
            return cachedCircleSprite;

        const int diameter = 256;
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float radiusPx = diameter * 0.5f;
        Vector2 center = new Vector2(radiusPx, radiusPx);

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radiusPx - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
        return cachedCircleSprite;
    }

    // Procedurally generates a thin unfilled ring sprite (solid stroke, no dashes, stroke-width
    // 1 SVG unit scaled to this sprite's fixed 256px working resolution) - used for the outer
    // fixed range ring.
    private static Sprite cachedRingSprite;
    private static Sprite GetRingSprite()
    {
        if (cachedRingSprite != null)
            return cachedRingSprite;

        const int diameter = 256;
        const float strokeWidthPx = 3f;
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float outerR = diameter * 0.5f;
        float innerR = outerR - strokeWidthPx;
        Vector2 center = new Vector2(outerR, outerR);

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float outerAlpha = Mathf.Clamp01(outerR - dist + 0.5f);
                float innerAlpha = Mathf.Clamp01(dist - innerR + 0.5f);
                float alpha = Mathf.Min(outerAlpha, innerAlpha);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedRingSprite = Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
        return cachedRingSprite;
    }

    // Procedurally generates a much thicker unfilled ring sprite (proportionally to its own
    // small on-screen size) than GetRingSprite() - the course pointer's reference dots are only
    // ~5 SVG units across, so a stroke sized for the large outer range ring reads as an almost
    // invisible hairline at that scale. A thicker relative stroke (25% of the sprite's own
    // radius, not a fixed pixel width) keeps the ring visibly solid no matter how small the dot
    // is finally rendered.
    private static Sprite cachedReferenceDotRingSprite;
    private static Sprite GetReferenceDotRingSprite()
    {
        if (cachedReferenceDotRingSprite != null)
            return cachedReferenceDotRingSprite;

        const int diameter = 256;
        const float strokeWidthPx = diameter * 0.25f;
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float outerR = diameter * 0.5f;
        float innerR = outerR - strokeWidthPx;
        Vector2 center = new Vector2(outerR, outerR);

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float outerAlpha = Mathf.Clamp01(outerR - dist + 0.5f);
                float innerAlpha = Mathf.Clamp01(dist - innerR + 0.5f);
                float alpha = Mathf.Min(outerAlpha, innerAlpha);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedReferenceDotRingSprite = Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
        return cachedReferenceDotRingSprite;
    }

    // Procedurally generates a dashed ring sprite - reference: stroke-dasharray="3 4" (dash 3,
    // gap 4, ~43% duty cycle) on a r=60 circle (circumference ~377 SVG units -> ~54 dash+gap
    // repeats). dashCount below approximates that repeat count and duty cycle at this sprite's
    // fixed 256px working resolution.
    private static Sprite cachedDashedRingSprite;
    private static Sprite GetDashedRingSprite()
    {
        if (cachedDashedRingSprite != null)
            return cachedDashedRingSprite;

        const int diameter = 256;
        const float strokeWidthPx = 3f;
        const int dashCount = 54;
        const float dutyCycle = 3f / 7f; // dash 3 / (dash 3 + gap 4)
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float outerR = diameter * 0.5f;
        float innerR = outerR - strokeWidthPx;
        Vector2 center = new Vector2(outerR, outerR);
        float degPerDash = 360f / dashCount;

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float dist = p.magnitude;
                float outerAlpha = Mathf.Clamp01(outerR - dist + 0.5f);
                float innerAlpha = Mathf.Clamp01(dist - innerR + 0.5f);
                float ringAlpha = Mathf.Min(outerAlpha, innerAlpha);

                if (ringAlpha > 0f)
                {
                    float angleDeg = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    if (angleDeg < 0f) angleDeg += 360f;
                    float withinDash = angleDeg % degPerDash;
                    bool isDash = withinDash < degPerDash * dutyCycle;
                    if (!isDash)
                        ringAlpha = 0f;
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ringAlpha));
            }
        }

        tex.Apply();
        cachedDashedRingSprite = Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
        return cachedDashedRingSprite;
    }

    // Procedurally generates an upward-pointing solid triangle/wedge sprite - used for filled
    // arrowheads/lubber line whose apex sits at the top of their bounding box. Texture2D.SetPixel
    // treats y=0 as the BOTTOM of the image, and a UI Image's sprite renders with V=0 at the
    // bottom of its RectTransform - so putting the apex at the TOP (high y) here makes it point
    // up once rendered.
    private static Sprite cachedTriangleUpSprite;
    private static Sprite GetTriangleUpSprite()
    {
        if (cachedTriangleUpSprite != null)
            return cachedTriangleUpSprite;

        cachedTriangleUpSprite = BuildTriangleSprite(pointUp: true);
        return cachedTriangleUpSprite;
    }

    // Downward-pointing counterpart, used for arrowheads whose apex sits at the bottom of their
    // bounding box.
    private static Sprite cachedTriangleDownSprite;
    private static Sprite GetTriangleDownSprite()
    {
        if (cachedTriangleDownSprite != null)
            return cachedTriangleDownSprite;

        cachedTriangleDownSprite = BuildTriangleSprite(pointUp: false);
        return cachedTriangleDownSprite;
    }

    private static Sprite BuildTriangleSprite(bool pointUp)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            // For pointUp: t=0 at the bottom (full width base), t=1 at the top (apex, zero
            // width). For pointDown: mirrored, so the apex is at the bottom instead.
            float t = (float)y / (size - 1);
            float shapeT = pointUp ? t : (1f - t);
            float halfWidth = (size * 0.5f) * shapeT;

            for (int x = 0; x < size; x++)
            {
                float distOutsideEdge = Mathf.Abs(x + 0.5f - size * 0.5f) - halfWidth;
                float alpha = Mathf.Clamp01(0.5f - distOutsideEdge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Font GetDefaultFont()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (cachedFont == null)
            cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

        return cachedFont;
    }
}