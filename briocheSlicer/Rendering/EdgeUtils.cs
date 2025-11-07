using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing
{
    internal static class EdgeUtils
    {
        public static List<BriocheEdge> Merge_Collinear(List<BriocheEdge> edges, double eps = 1e-6)
        {
            if (edges == null || edges.Count < 2) return edges ?? new();

            // snap keys
            long KeyX(double x) => (long)Math.Round(x / eps);
            long KeyY(double y) => (long)Math.Round(y / eps);

            var list = new List<BriocheEdge>(edges);
            bool changed;

            do
            {
                changed = false;

                // build adjacency by endpoint
                var byEnd = new Dictionary<(long, long), List<(int idx, bool isStart)>>();
                void Add(Point3D p, int idx, bool isStart)
                {
                    var k = (KeyX(p.X), KeyY(p.Y));
                    if (!byEnd.TryGetValue(k, out var l)) byEnd[k] = l = new();
                    l.Add((idx, isStart));
                }

                for (int i = 0; i < list.Count; i++)
                {
                    Add(list[i].Start, i, true);
                    Add(list[i].End, i, false);
                }

                // try merge at each vertex with exactly 2 incident edges
                foreach (var kv in byEnd.ToList())
                {
                    var inc = kv.Value;
                    if (inc.Count != 2) continue;

                    var (iIdx, iAtStart) = inc[0];
                    var (jIdx, jAtStart) = inc[1];
                    if (iIdx == jIdx) continue;

                    var ei = list[iIdx];
                    var ej = list[jIdx];

                    // orient so both go through the shared point: i: A->V, j: V->B
                    var V = iAtStart ? ei.Start : ei.End;
                    var A = iAtStart ? ei.End : ei.Start; // edge i points into V
                    var B = jAtStart ? ej.End : ej.Start; // edge j points out of V

                    // vectors AV and VB (same straight line means cross ~ 0 and dot negative/positive appropriate)
                    var v1 = new Vector3D(V.X - A.X, V.Y - A.Y, 0);
                    var v2 = new Vector3D(B.X - V.X, B.Y - V.Y, 0);

                    // collinear check: cross z ~ 0
                    double cross = v1.X * v2.Y - v1.Y * v2.X;
                    if (Math.Abs(cross) > eps) continue;

                    // they must be aligned (not turning back and forth):
                    if (v1.X * v2.X + v1.Y * v2.Y < -eps) continue; // opposite directions (would form a straight but backward)

                    // OK: merge A -> B
                    var merged = new BriocheEdge(A, B);

                    // replace the two edges with the merged one
                    // mark higher index first to remove cleanly
                    int a = Math.Max(iIdx, jIdx), b = Math.Min(iIdx, jIdx);
                    list.RemoveAt(a);
                    list.RemoveAt(b);
                    list.Add(merged);

                    changed = true;
                    break; // restart adjacency after mutation
                }

            } while (changed);

            return list;
        }
    }
}
