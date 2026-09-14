// HoldingPathProjector.cs
//
// Single source of truth for converting a 3D world position/heading (aircraft, fix, spline
// sample point - anything on the horizontal XZ plane) into a 2D point on the holding-pattern map
// RectTransform. Both the live aircraft icon (HoldingMapView) and the static dotted entry-path
// preview use THIS SAME function to go from world space to map space, so they are geometrically
// guaranteed to agree - the aircraft icon can never visually drift off the dotted line it's
// supposed to be flying, because there is only one place that math is written.
//
// Not a MonoBehaviour - this is a plain static utility, instantiated nowhere, attached to
// nothing. Callers (HoldingMapView, any future path-preview renderer) hold their own reference
// to the map RectTransform and a world-to-map scale/origin, and call these methods every time
// they need to place something.

using UnityEngine;

public static class HoldingPathProjector
{
    /// <summary>
    /// Converts a world-space XZ position into a local anchoredPosition on the given map
    /// RectTransform, relative to a chosen world-space origin (typically the holding fix), using
    /// a uniform world-units-to-map-pixels scale.
    /// </summary>
    /// <param name="worldPosition">The 3D world position to project (Y/altitude is ignored - this is a top-down map).</param>
    /// <param name="worldOrigin">The world-space point that maps to the map RectTransform's local (0,0) - typically the holding fix's Transform.position.</param>
    /// <param name="worldUnitsToMapPixels">Scale factor: how many map-local units one world unit (meter) corresponds to. Tune this per map so the whole pattern fits the panel.</param>
    public static Vector2 WorldToMapPosition(Vector3 worldPosition, Vector3 worldOrigin, float worldUnitsToMapPixels)
    {
        Vector3 delta = worldPosition - worldOrigin;

        // World X -> map local X directly. World Z -> map local Y: this is the top-down
        // "north-up" convention (world +Z, usually "forward"/north, reads as map "up"), matching
        // how the ND/HSI itself treats heading 0 as pointing up.
        return new Vector2(delta.x * worldUnitsToMapPixels, delta.z * worldUnitsToMapPixels);
    }

    /// <summary>
    /// Converts a world-space Y-axis heading (degrees, 0 = facing world +Z / "north") into the
    /// Z rotation to apply to a map icon's RectTransform so it visually points the same
    /// direction on the 2D map that the aircraft is actually facing in 3D.
    /// </summary>
    public static float WorldHeadingToMapRotationZ(float worldEulerAnglesY)
    {
        // A UI RectTransform's unrotated "up" already matches the map's "north" convention used
        // by WorldToMapPosition above (+Z -> +Y). World yaw is left-handed/clockwise-positive
        // (Unity's Transform.eulerAngles.y), while a UI Z rotation is counter-clockwise-positive
        // - so the sign flips, with no extra offset needed since both conventions already treat
        // "0" as pointing the same way ("north"/"up").
        return -worldEulerAnglesY;
    }

    /// <summary>
    /// Convenience: projects both position and heading in one call and writes them directly onto
    /// a target RectTransform (an aircraft icon, or any other world-following map marker).
    /// </summary>
    public static void ApplyToRectTransform(RectTransform target, Vector3 worldPosition, float worldEulerAnglesY, Vector3 worldOrigin, float worldUnitsToMapPixels)
    {
        target.anchoredPosition = WorldToMapPosition(worldPosition, worldOrigin, worldUnitsToMapPixels);
        target.localRotation = Quaternion.Euler(0f, 0f, WorldHeadingToMapRotationZ(worldEulerAnglesY));
    }
}