using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Runtime.CompilerServices;
using Clipper2Lib;

namespace briocheSlicer.Slicing
{
    internal class BriocheSlice
    {
        private List<List<BriocheEdge>> polygons; // Store multiple polygons
        private const double EPSILON = 1e-6; // Tolerance for floating point comparison
        private readonly double slice_height;
        private PathsD? slice;

        public BriocheSlice(List<BriocheEdge> edges, double z)
        {
            this.slice_height = z;
            this.polygons = Connect_Edges(edges);
            this.slice = Convert_To_Clipper_Slice();
        }

        public List<List<BriocheEdge>> getPolygons()
        {
            return polygons;
        }

        /// <summary>
        /// Connects edges to form closed polygon loops.
        /// Handles vertex snapping, adjacency building, and loop formation.
        /// </summary>
        /// <param name="unfilteredEdges">Raw edges from mesh intersection</param>
        /// <returns>List of closed polygon loops, each represented as a list of connected edges</returns>
        private List<List<BriocheEdge>> Connect_Edges(List<BriocheEdge> unfilteredEdges)
        {
            var result = new List<List<BriocheEdge>>();
            if (unfilteredEdges == null || unfilteredEdges.Count == 0)
                return result;

            var snap = new Snapper(EPSILON);

            // Step 1: Normalize and filter edges to remove duplicates and degenerate cases
            var edges = NormalizeAndFilterEdges(unfilteredEdges, snap);
            if (edges.Count == 0) return result;

            // Step 2: Build adjacency graph for efficient neighbor lookup
            var adjacencyList = BuildAdjacencyList(edges, snap);

            // Step 3: Form closed loops from connected edges
            var loops = FormClosedLoops(edges, adjacencyList, snap);

            return loops;
        }

        private PathsD Convert_To_Clipper_Slice()
        {
            PathsD rawSlice = Convert_Polygon_To_Clipper(polygons);

            return Clipper.Union(rawSlice, FillRule.EvenOdd);
        }

        private static PathsD Convert_Polygon_To_Clipper(List<List<BriocheEdge>> polies)
        {
            PathsD rawSlice = new PathsD();
            foreach (var polygon in polies)
            {
                var path = new PathD();
                foreach (var edge in polygon)
                {
                    path.Add(new PointD(edge.Start.X, edge.Start.Y));
                }
                rawSlice.Add(path);
            }
            return rawSlice;
        }

        /// <summary>
        /// Normalizes vertex coordinates using snapping and filters out degenerate edges.
        /// This solves floating-point precision issues by snapping nearby vertices together.
        /// </summary>
        /// <param name="unfilteredEdges">Raw edges that may have precision issues</param>
        /// <param name="snap">Snapper instance for consistent vertex normalization</param>
        /// <returns>List of valid edges with normalized vertices</returns>
        private List<BriocheEdge> NormalizeAndFilterEdges(List<BriocheEdge> unfilteredEdges, Snapper snap)
        {
            var edges = new List<BriocheEdge>(unfilteredEdges.Count);

            foreach (var edge in unfilteredEdges)
            {
                // Snap vertices to a consistent grid to handle floating-point precision
                var start = snap.Norm_Vert(edge.Start.X, edge.Start.Y);
                var end = snap.Norm_Vert(edge.End.X, edge.End.Y);

                // Filter out degenerate edges (where start and end are the same point)
                if (!Close_By(start.X, start.Y, end.X, end.Y, EPSILON))
                {
                    edges.Add(new BriocheEdge(
                         new Point3D(start.X, start.Y, slice_height),
                       new Point3D(end.X, end.Y, slice_height)));
                }
            }

            return edges;
        }

        /// <summary>
        /// Builds an adjacency list mapping each vertex to all edges connected to it.
        /// Enables O(1) lookup of neighboring edges during loop formation.
        /// </summary>
        /// <param name="edges">Normalized edges</param>
        /// <param name="snap">Snapper for consistent vertex key generation</param>
        /// <returns>Dictionary mapping vertex keys to lists of incident edges</returns>
        private Dictionary<VertexKey, List<(int edgeIdx, bool atStart)>> BuildAdjacencyList
        (
            List<BriocheEdge> edges, Snapper snap
        )
        {
            var adjacencyList = new Dictionary<VertexKey, List<(int edgeIdx, bool atStart)>>();

            for (int i = 0; i < edges.Count; i++)
            {
                // Record that this edge starts at edges[i].Start
                AddToAdjacencyList(adjacencyList, snap, edges[i].Start, i, atStart: true);

                // Record that this edge ends at edges[i].End
                AddToAdjacencyList(adjacencyList, snap, edges[i].End, i, atStart: false);
            }

            return adjacencyList;
        }

        /// <summary>
        /// Helper to add an edge endpoint to the adjacency list.
        /// </summary>
        private void AddToAdjacencyList
        (
            Dictionary<VertexKey, List<(int edgeIdx, bool atStart)>> adjacencyList,
            Snapper snap, Point3D point, int edgeIndex, bool atStart
        )
        {
            var key = snap.Key(point.X, point.Y);
            if (!adjacencyList.TryGetValue(key, out var list))
            {
                list = new List<(int, bool)>();
                adjacencyList[key] = list;
            }
            list.Add((edgeIndex, atStart));
        }

        /// <summary>
        /// Forms closed polygon loops by traversing connected edges.
        /// Uses a greedy approach: pick an unused edge, follow connections until returning to start.
        /// </summary>
        /// <param name="edges">Normalized edges</param>
        /// <param name="adjacencyList">Vertex-to-edge mapping</param>
        /// <param name="snap">Snapper for vertex comparison</param>
        /// <returns>List of closed polygon loops</returns>
        private List<List<BriocheEdge>> FormClosedLoops
        (
            List<BriocheEdge> edges,
            Dictionary<VertexKey, List<(int edgeIdx, bool atStart)>> adjacencyList,
            Snapper snap
        )
        {
            var result = new List<List<BriocheEdge>>();
            var used = new bool[edges.Count];

            // Try to form a loop starting from each unused edge
            for (int startEdgeIdx = 0; startEdgeIdx < edges.Count; startEdgeIdx++)
            {
                if (used[startEdgeIdx]) continue;

                var loop = TraceLoop(edges, adjacencyList, snap, used, startEdgeIdx);

                // Only keep valid loops with at least 3 edges (minimum for a polygon)
                if (loop.Count >= 3)
                {
                    result.Add(loop);
                }
            }

            return result;
        }

        /// <summary>
        /// Traces a single closed loop starting from a given edge.
        /// Follows edge-to-edge connections until returning to the start vertex or encountering an error.
        /// </summary>
        /// <param name="edges">All edges</param>
        /// <param name="adjacencyList">Vertex-to-edge mapping</param>
        /// <param name="snap">Snapper for vertex comparison</param>
        /// <param name="used">Tracks which edges have been used</param>
        /// <param name="startEdgeIdx">Index of the starting edge</param>
        /// <returns>List of edges forming a closed loop, or empty if loop cannot be closed</returns>
        private List<BriocheEdge> TraceLoop
        (
            List<BriocheEdge> edges,
            Dictionary<VertexKey, List<(int edgeIdx, bool atStart)>> adjacencyList,
            Snapper snap,
            bool[] used,
            int startEdgeIdx
        )
        {
            var loop = new List<BriocheEdge>();
            var firstEdge = edges[startEdgeIdx];

            used[startEdgeIdx] = true;
            loop.Add(firstEdge);

            var startVertex = firstEdge.Start;
            var currentVertex = firstEdge.End;

            var startKey = snap.Key(startVertex.X, startVertex.Y);
            var currentKey = snap.Key(currentVertex.X, currentVertex.Y);

            // Safety limit to prevent infinite loops in case of malformed data
            int maxIterations = edges.Count * 4;

            while (maxIterations-- > 0)
            {
                // Check if we've closed the loop
                if (currentKey.Equals(startKey) ||
                      Close_By(currentVertex.X, currentVertex.Y, startVertex.X, startVertex.Y, EPSILON))
                {
                    break;
                }

                // Find the next edge to continue the loop
                int nextEdgeIdx = FindNextEdge(adjacencyList, currentKey, currentVertex, edges, used);

                if (nextEdgeIdx < 0)
                {
                    // Cannot continue loop - invalid mesh topology
                    loop.Clear();
                    break;
                }

                // Add the next edge and advance to its endpoint
                var nextEdge = AddOrientedEdge(edges[nextEdgeIdx], currentVertex, loop);
                used[nextEdgeIdx] = true;

                currentVertex = nextEdge.End;
                currentKey = snap.Key(currentVertex.X, currentVertex.Y);
            }

            // If loop is empty, it failed to close properly
            if (loop.Count == 0) return loop;

            // Explicitly close the loop if there's a small gap
            if (!Close_By(loop[^1].End.X, loop[^1].End.Y, loop[0].Start.X, loop[0].Start.Y, EPSILON))
            {
                loop.Add(new BriocheEdge(loop[^1].End, loop[0].Start));
            }

            return loop;
        }

        /// <summary>
        /// Finds the next unused edge connected to the current vertex.
        /// </summary>
        /// <param name="adjacencyList">Vertex-to-edge mapping</param>
        /// <param name="currentKey">Key of current vertex</param>
        /// <param name="currentVertex">Current vertex position</param>
        /// <param name="edges">All edges</param>
        /// <param name="used">Tracks used edges</param>
        /// <returns>Index of next edge, or -1 if not found</returns>
        private int FindNextEdge
        (
            Dictionary<VertexKey, List<(int edgeIdx, bool atStart)>> adjacencyList,
            VertexKey currentKey,
            Point3D currentVertex,
            List<BriocheEdge> edges,
            bool[] used
        )
        {
            if (!adjacencyList.TryGetValue(currentKey, out var incidentEdges))
            {
                return -1; // No edges at this vertex
            }

            // Search for an unused edge that starts at the current vertex
            foreach (var (edgeIdx, atStart) in incidentEdges)
            {
                if (used[edgeIdx]) continue;

                var edge = edges[edgeIdx];
                var edgeStart = atStart ? edge.Start : edge.End;

                // Verify the edge actually connects to our current position
                if (Close_By(edgeStart.X, edgeStart.Y, currentVertex.X, currentVertex.Y, EPSILON))
                {
                    return edgeIdx;
                }
            }

            return -1; // No valid next edge found
        }

        /// <summary>
        /// Adds an edge to the loop with correct orientation (starting from currentVertex).
        /// The raw edge might be stored backwards, so we flip it if needed.
        /// </summary>
        /// <param name="rawEdge">Edge as stored in the edges list</param>
        /// <param name="currentVertex">The vertex where this edge should start</param>
        /// <param name="loop">The loop being built</param>
        /// <returns>The correctly oriented edge</returns>
        private BriocheEdge AddOrientedEdge(BriocheEdge rawEdge, Point3D currentVertex, List<BriocheEdge> loop)
        {
            // Determine if edge needs to be flipped based on which end matches current vertex
            bool startsAtCurrent = Close_By(rawEdge.Start.X, rawEdge.Start.Y, currentVertex.X, currentVertex.Y, EPSILON);

            var orientedEdge = startsAtCurrent
              ? new BriocheEdge(rawEdge.Start, rawEdge.End)  // Use as-is
             : new BriocheEdge(rawEdge.End, rawEdge.Start); // Flip direction

            loop.Add(orientedEdge);
            return orientedEdge;
        }

        private static bool Close_By(double x1, double y1, double x2, double y2, double eps)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return dx * dx + dy * dy <= eps * eps;
        }
    }
}
