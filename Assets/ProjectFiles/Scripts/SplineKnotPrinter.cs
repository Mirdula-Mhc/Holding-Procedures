// SplineKnotPrinter.cs
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteInEditMode]
public class SplineKnotPrinter : MonoBehaviour
{
    [ContextMenu("Print Knot T Values")]
    public void PrintKnots()
    {
        if (!TryGetComponent(out SplineContainer container) || container.Spline == null) return;

        var spline = container.Spline;
        int count = spline.Count;
        Debug.Log($"<color=cyan>=== ACCURATE KNOT T VALUES: {gameObject.name} ===</color>");

        float currentT = 0f;
        const int subSteps = 1000;

        for (int i = 0; i < count; i++)
        {
            Vector3 knotPos = (Vector3)spline[i].Position;

            if (i == 0)
            {
                currentT = 0f;
                Debug.Log($"<b>Knot [0]:</b> Normalized T = <color=yellow>0.0000</color>");
                continue;
            }

            // Search only in a window ahead of the previous knot (prevents loop-end snapping)
            float windowEnd = Mathf.Min(1f, currentT + 0.30f);
            float bestT = currentT;
            float closestSqrDist = float.MaxValue;

            for (int s = 0; s <= subSteps; s++)
            {
                float t = Mathf.Lerp(currentT, windowEnd, (float)s / subSteps);
                SplineUtility.Evaluate(spline, t, out float3 pos, out _, out _);
                float sqrDist = math.distancesq(pos, knotPos);

                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    bestT = t;
                }
            }

            currentT = bestT;
            Debug.Log($"<b>Knot [{i}]:</b> Normalized T = <color=yellow>{bestT:F4}</color>");
        }
    }
}