using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Runtime.CompilerServices;

namespace briocheSlicer.Slicing
{
    internal class Slice
    {
        private List<List<BriocheEdge>> polygons; // Store multiple polygons
        private const double EPSILON = 1e-6; // Tolerance for floating point comparison
        private readonly double slice_height;

        public Slice(List<BriocheEdge> edges, double z)
        {
            this.slice_height = z;
            this.polygons = Connect_Edges(edges);
        }

        public List<List<BriocheEdge>> getPolygons()
        {
            return polygons;
        }

        /// <summary>
        /// This function connects all the edges.
        /// Results in a ordered list of briocheEdges forming a closed loop.
        /// </summary>
        /// <returns>List of connected BriocheEdges.</returns>
        private List<List<BriocheEdge>> Connect_Edges(List<BriocheEdge> unfilteredEdges)
        {
            var result = new List<List<BriocheEdge>>();
            if (unfilteredEdges == null || unfilteredEdges.Count == 0)
                return result;

            double z = slice_height;

            // Pre-processing vertices to account for float rounding issues
            var snap = new Snapper(EPSILON);
            var edges = new List<BriocheEdge>(unfilteredEdges.Count);

            foreach(var edge in unfilteredEdges)
            {
                var start = snap.Norm_Vert(edge.Start.X, edge.Start.Y);
                var end = snap.Norm_Vert(edge.End.X, edge.End.Y);

                if (!Close_By(start.X, start.Y, end.X, end.Y, EPSILON))
                {
                    edges.Add(new BriocheEdge(new Point3D(start.X, start.Y, z), new Point3D(end.X, end.Y, z)));
                }
            }
            var edge_count = edges.Count();
            if (edges.Count() == 0) return result;

            // Look-up to quickly find the adjacent vertices
            var adjacencyList = new Dictionary<VertexKey, List<(int edgeIdx, bool atStart)>>();
            void AddAdjacent(Point3D p, int index, bool atStart)
            {
                var key = snap.Key(p.X, p.Y);
                if (!adjacencyList.TryGetValue(key, out var list)) adjacencyList[key] = list = new List<(int, bool)>();
                list.Add((index, atStart));
            }

            for (int i = 0; i < edge_count; i++)
            {
                AddAdjacent(edges[i].Start, i, true);
                AddAdjacent(edges[i].End, i, false);
            }

            var used = new bool[edges.Count];
            for (int start = 0; start < edge_count; start++)
            {
                if (used[start]) continue;

                var loop = new List<BriocheEdge>();
                var first = edges[start];
                used[start] = true;
                loop.Add(first);

                var start_V = first.Start;
                var current_V = first.End;

                var startKey = snap.Key(start_V.X, start_V.Y);
                var currentKey = snap.Key(current_V.X, current_V.Y);

                int avoid_infinite = edge_count * 4;

                while (avoid_infinite-- > 0)
                {
                    if (currentKey.Equals(startKey) || Close_By(current_V.X, current_V.Y, start_V.X, start_V.Y, EPSILON))
                    {
                        break;
                    }

                    if (!adjacencyList.TryGetValue(currentKey, out var inc))
                    {
                        loop.Clear();
                        break;
                    }

                    int pick = -1;
                    bool asStart = true;

                    for (int i = 0; i < inc.Count; i++)
                    {
                        var (index, atStart) = inc[i];
                        if (used[index]) continue;

                        var edge = edges[index];
                        var from = atStart ? edge.Start : edge.End;
                        if (Close_By(from.X, from.Y, current_V.X, current_V.Y, EPSILON))
                        {
                            pick = index;
                            asStart = atStart;
                            break;
                        }
                    }

                    if (pick < 0)
                    {
                        loop.Clear();
                        break;
                    }

                    used[pick] = true;
                    var raw = edges[pick];
                    var oriented = asStart ? new BriocheEdge(raw.Start, raw.End) : new BriocheEdge(raw.End, raw.Start);

                    loop.Add(oriented);
                    current_V = oriented.End;
                    currentKey = snap.Key(current_V.X, current_V.Y);
                }

                if (loop.Count == 0) continue;

                if (!Close_By(loop[^1].End.X, loop[^1].End.Y, loop[0].Start.X, loop[0].Start.Y, EPSILON))
                {
                    loop.Add(new BriocheEdge(loop[^1].End, loop[0].Start));
                }

                if (loop.Count >= 3)
                {
                    result.Add(loop);
                }
            }

            return result;
        }
        private static bool Close_By(double x1, double y1, double x2, double y2, double eps)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return dx * dx + dy * dy <= eps * eps;
        }

        private readonly struct VertexKey : IEquatable<VertexKey>
        {
            public readonly long Xq, Yq;
            public VertexKey(long xq, long yq) { Xq = xq; Yq = yq; }
            public bool Equals(VertexKey other)
            {
                if (Xq == other.Xq && Yq == other.Yq) return true;
                return false;
            }
            public override bool Equals(object? obj)
            {
                if (obj is VertexKey k && Equals(k)) return true;
                return false;
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        private sealed class Snapper
        {
            private readonly double _epsScale;
            private readonly Dictionary<VertexKey, (double x, double y)> _keyValuePairs = new();

            public Snapper(double eps) { _epsScale = 1.0 / eps; }
            public (double X, double Y) Norm_Vert(double x, double y)
            {
                var key = Key(x, y);
                if (_keyValuePairs.TryGetValue(key, out var r)) return r;
                _keyValuePairs[key] = (x, y);
                return (x, y);
            }

            public VertexKey Key(double x, double y)
            {
                return new VertexKey((long)Math.Round(x * _epsScale), (long)Math.Round(y * _epsScale));
            }
        }
    }
}
