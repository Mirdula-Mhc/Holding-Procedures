// HoldingScenarioData.cs
//
// Minimal ScriptableObject describing one holding-pattern sector-entry scenario, following the
// same data-driven pattern as the ATC sim's ScenarioData (see /areas/atc-signal-sim.md and
// /areas/holding-procedures.md: "scenario data/order handled via ScriptableObjects... so order
// can be changed without relying on index values").
//
// Deliberately minimal for now - this is just enough to identify a scenario and point at its
// spline/fix/course, so scenes can be data-driven from the start. Fields for video/audio/
// question hookups (matching ScenarioData's shape in the ATC sim) are NOT included yet, since
// Scene 1's specific UI/interaction requirements are still being worked out with the pilot - add
// those once that's settled, rather than guessing at their shape now.

using UnityEngine;
using UnityEngine.Splines;

[CreateAssetMenu(fileName = "HoldingScenario", menuName = "MH Cockpit/Holding Procedures/Scenario Data")]
public class HoldingScenarioData : ScriptableObject
{
    public enum EntryType { Direct, Parallel, Teardrop }

    [Header("Identity")]
    [Tooltip("Human-readable name for this scenario, shown in editor lists and any debug UI - not used for ordering (ordering comes from wherever this asset sits in an explicit scenario list/array, not from this field or this asset's file name).")]
    public string scenarioName = "New Holding Scenario";

    public EntryType entryType = EntryType.Direct;

    [Header("Geometry")]
    [Tooltip("The spline this scenario's aircraft follows once this entry type is selected/active.")]
    public SplineContainer entrySpline;

    [Tooltip("The holding fix / VOR station Transform for this scenario. Scene-specific, so this references a scene object - if you need this to survive scene reloads cleanly, consider resolving it by name/tag at load time instead of a direct scene reference.")]
    public Transform fix;

    [Tooltip("Inbound course TO the fix, degrees magnetic, for this scenario's holding pattern.")]
    [Range(0f, 359.99f)] public float inboundCourse = 270f;

    [Header("Nav Radio Identity")]
    public string vorIdent = "CLT";
    [Range(108f, 117.95f)] public float vorFreq = 115.00f;
}