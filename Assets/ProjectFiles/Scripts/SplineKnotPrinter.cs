// SplineKnotPrinter.cs
using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
public class SplineKnotPrinter : MonoBehaviour
{
    [ContextMenu("Print Knot T Values")]
    public void PrintKnots()
    {
        if (!TryGetComponent(out SplineContainer container) || container.Spline == null) return;

        var spline = container.Spline;
        Debug.Log($"<color=cyan>=== KNOT T VALUES FOR: {gameObject.name} ===</color>");

        for (int i = 0; i < spline.Count; i++)
        {
            SplineUtility.GetNearestPoint(spline, spline[i].Position, out _, out float t);
            Debug.Log($"<b>Knot [{i}]:</b> Normalized T = <color=yellow>{t:F4}</color>");
        }
    }
}