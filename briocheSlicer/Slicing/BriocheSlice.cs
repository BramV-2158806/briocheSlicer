using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Runtime.CompilerServices;
using Clipper2Lib;
using briocheSlicer.Gcode;
using System.ComponentModel;

namespace briocheSlicer.Slicing
{
    internal class BriocheSlice
    {
        private const double EPSILON = 1e-6;
        public readonly double slice_height;

        // The paths to be printed.
        // This is the meat of our brioche slice.
        private PathsD? outerLayer;
        private PathsD? infill;
        private PathsD? floor;
        private PathsD? roof;
        private PathsD? support;

        // Region so we can calculate the difference 
        // when generating infill.
        private PathsD? floorRegion;
        private PathsD? roofRegion;
        private PathsD? supportRegion;

        private List<PathsD> shells;

        private bool checkedRoof = false;
        private bool checkedFloor = false;

        private readonly GcodeSettings settings;

        public BriocheSlice(List<BriocheEdge> edges, double z, GcodeSettings settings)
        {
            this.slice_height = z;
            this.settings = settings;
            var polygons = Connect_Edges(edges);

            var cleanSlice = Convert_To_Clipper_Slice(polygons);

            // Execute the first phase of the slicing.
            // The first phase is done local for each slice
            cleanSlice = Erode_Perimiter(cleanSlice, settings);
            this.shells = Generate_Shells(cleanSlice, settings);
            PathsD outerLayer = new();
            foreach (var shell in shells) // Combine all shells into one PathsD
            {
                outerLayer.AddRange(shell);
            }
            this.outerLayer = outerLayer;
            this.outerLayer = Clipper.SimplifyPaths(this.outerLayer, 1e-9, true);
        }

        public PathsD? GetOuterLayer()
        {
            return outerLayer;
        }
        public PathsD? GetInfill()
        {
            return infill;
        }
        public PathsD? GetFloor()
        {
            return floor;
        }
        public PathsD? GetRoof()
        {
            return roof;
        }

        public PathsD? GetSupport()
        {
            return support;
        }

        public void SetSupport(PathsD sup) { support = sup; }

        public PathsD? GetSupportRegion()
        {
            return supportRegion;
        }

        public PathsD? GetOuterShell()
        {
            if (shells != null && shells.Count > 0)
            {
                return shells.First();
            }
            return null;
        }

        public PathsD? GetInnerShell()
        {
            if (shells != null && shells.Count > 0)
            {
                return shells.Last();
            }
            return null;
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

            // Normalize and filter edges to remove duplicates and degenerate cases
            var edges = NormalizeAndFilterEdges(unfilteredEdges, snap);
            if (edges.Count == 0) return result;

            // Build adjacency graph for efficient neighbor lookup
            var adjacencyList = BuildAdjacencyList(edges, snap);

            // Form closed loops from connected edges
            var loops = FormClosedLoops(edges, adjacencyList, snap);

            return loops;
        }

        private PathsD Convert_To_Clipper_Slice(List<List<BriocheEdge>> polygons)
        {
            // Convert the basic BriochePolygins to a clipper representation
            PathsD rawSlice = Convert_Polygon_To_Clipper(polygons);

            // Apply the fill in rule
            return Clipper.Union(rawSlice, FillRule.EvenOdd);
        }


        /// <summary>
        /// Generates multiple concentric shell perimeters by progressively offsetting inward.
        /// The returned list contains shells ordered from outermost to innermost:
        /// - shells[0] or shells.First() gives the outermost perimeter
        /// - shells[shells.Count - 1] or shells.Last() gives the innermost perimeter
        /// </summary>
        /// <param name="cleanSlice">The initial slice geometry to generate shells from</param>
        /// <param name="settings">Print settings including nozzle diameter and number of shells</param>
        /// <returns>List of shells ordered from outer to inner perimeter</returns>
        private List<PathsD> Generate_Shells(PathsD cleanSlice, GcodeSettings settings)
        {
            var shells = new List<PathsD>();

            // Add the outer perimeter (first shell - index 0)
            PathsD currentShell = cleanSlice;
            shells.Add(currentShell); 
            
            // Generate each additional shell by offsetting from the previous one
            for (int i = 1; i < settings.NumberShells; i++)
            {
                // Offset inward by one nozzle width
                double delta = -settings.NozzleDiameter;
                currentShell = Clipper.InflatePaths(currentShell, delta, JoinType.Round, EndType.Polygon);
                currentShell = Clipper.SimplifyPaths(currentShell, 1e-9, true);
                shells.Add(currentShell);
            }

            return shells;
        }

        /// <summary>
        /// Creates a solid inside the perimiter.
        /// The perimiter itself is not included in this solid.
        /// </summary>
        /// <param name="perimiter"></param>
        /// <returns></returns>
        private PathsD Generate_Solid(PathsD perimiter)
        {
            double delta = -settings.NozzleDiameter;

            var solid = new PathsD();

            // Already inflate once so we dont print the innner shell 2 
            var currentPath = Clipper.InflatePaths(perimiter, delta, JoinType.Round, EndType.Polygon);
            solid.AddRange(currentPath);
            while (true)
            {
                currentPath = Clipper.InflatePaths(currentPath, delta, JoinType.Round, EndType.Polygon);
                
                // If the new path is empty we can stop
                if (currentPath.Count == 0)
                {
                    break;
                }

                solid.AddRange(currentPath);
            }
            return solid;
        }

        private PathsD Erode_Perimiter(PathsD cleanSlice, GcodeSettings settings)
        {
            // Erode the permites
            // Negative delta (erosion) half of the size of the nozzle diameter
            double delta = -(settings.NozzleDiameter / 2.0);
            return Clipper.InflatePaths(cleanSlice, delta, JoinType.Round, EndType.Polygon);
        }

        public PathsD Generate_Floor(List<PathsD> innerPerimitersLower, bool isBaseLayer = false)
        {
            // Check that phase 1 was completed
            if (this.outerLayer == null || this.shells == null || GetInnerShell() == null)
            {
                throw new InvalidOperationException("Cannot generate floor: outer layer has not been generated. Ensure slicing and shell generation have completed.");
            }
            if (!isBaseLayer && (this.shells.Count == 0))
            {
                throw new InvalidOperationException("Cannot generate floor: no shells available for non-base layer. Ensure shell generation has completed.");
            }
            checkedFloor = true;

            // We Use the inner perimiter to define the slice
            // since we already know how the outer perimiter shoukd print.
            var currentPerim = GetInnerShell();

            if (isBaseLayer)
            {
                // For the base layer, we fill the entire area
                this.floorRegion = currentPerim;
                this.floor = Generate_Solid(currentPerim);
                return this.floor;
            }

            var intersectedLower = IntersectAll(innerPerimitersLower);

            // Solid_i - intersect(solid_i-1 ... solid_i-n)
            var floor = Clipper.Difference(currentPerim, intersectedLower, FillRule.NonZero);
            this.floorRegion = floor;

            // Fill in the floor
            this.floor = Generate_Solid(floor);
            this.floor = Clipper.SimplifyPaths(this.floor, 1e-9, true);

            return floor;
        } 

        public PathsD Generate_Roof(List<PathsD> innerPerimitersUpper, bool isTopLayer = false)
        {
            // Check that phase 1 was completed
            if (this.outerLayer == null || this.shells == null || GetInnerShell() == null)
            {
                throw new InvalidOperationException("Cannot generate roof: outer layer has not been generated. Ensure slicing and shell generation have completed.");
            }
            if (!isTopLayer && (this.shells.Count == 0))
            {
                throw new InvalidOperationException("Cannot generate roof: no shells available for non-top layer. Ensure shell generation has completed.");
            }
            checkedRoof = true;

            var currentPerim = GetInnerShell();
            if (isTopLayer)
            {
                // For the top layer, we fill the entire area
                this.roofRegion = currentPerim;
                this.roof = Generate_Solid(currentPerim);
                return this.roof;
            }
            var intersectedUpper = IntersectAll(innerPerimitersUpper);

            // Solid_i - intersect(solid_i-1 ... solid_i-n)
            var roof = Clipper.Difference(currentPerim, intersectedUpper, FillRule.NonZero);
            this.roofRegion = roof;

            // Fill in the roof
            this.roof = Generate_Solid(roof);
            this.roof = Clipper.SimplifyPaths(this.roof, 1e-9, true);
            return roof;
        }

        public PathsD Generate_Support(PathsD perimeterAndSupportUpper, int layerIndex,bool isTopLayer = false)
        {
            // Check that phase 1 was completed
            if (this.outerLayer == null || this.shells == null || GetInnerShell() == null)
            {
                throw new InvalidOperationException("Cannot generate roof: outer layer has not been generated. Ensure slicing and shell generation have completed.");
            }
            if (!isTopLayer && (this.shells.Count == 0))
            {
                throw new InvalidOperationException("Cannot generate roof: no shells available for non-top layer. Ensure shell generation has completed.");
            }

            if (isTopLayer)
            {
                return new PathsD();
            }

            // Define the self supporting area of the current layer
            var outershell = GetOuterShell();
            double selfSupportDelta = Math.Min(settings.NozzleDiameter / 2.0, settings.LayerHeight);
            var selfSupportingArea = Clipper.InflatePaths(outershell!, selfSupportDelta, JoinType.Round, EndType.Polygon);

            // Calculate the support region of the current layer: perimiter upper layer - self supporting area
            this.supportRegion = Clipper.Difference(perimeterAndSupportUpper, selfSupportingArea, FillRule.NonZero);

            // We also want to add another little ofset to the support region.
            // But for nows we have a scale issue bug
            double negativeOffset = -settings.NozzleDiameter;
            var offsettedSupportRegion = Clipper.InflatePaths(this.supportRegion, negativeOffset, JoinType.Round, EndType.Polygon);


            this.support = GenerateBoundedPattern(offsettedSupportRegion, InfillPattern.Cross, layerIndex);
            this.support = Clipper.SimplifyPaths(this.support, 1e-9, false);
            return this.support;

        }

        /// <summary>
        /// 
        /// This function is generated by AI.
        /// </summary>
        /// <param name="inputSets"></param>
        /// <param name="fillRule"></param>
        /// <returns></returns>
        private PathsD IntersectAll(List<PathsD> inputSets, FillRule fillRule = FillRule.NonZero)
        {
            // 1. specific checks
            if (inputSets == null || inputSets.Count == 0)
                return new PathsD();

            // 2. Start with the first set of paths as the "Accumulator"
            PathsD result = inputSets[0];

            // 3. Iterate through the rest of the list
            for (int i = 1; i < inputSets.Count; i++)
            {
                // If at any point the result becomes empty, there is no common intersection.
                // We can stop early to save processing time.
                if (result.Count == 0) break; // Note self: Intersection of empty with full is empty.

                // 4. Intersect the current result with the next set in the list
                // resulting in a smaller and smaller common area.
                result = Clipper.Intersect(result, inputSets[i], fillRule);
            }

            return result;
        }

        public enum InfillPattern { Horizontal, Cross}

        private PathsD GenerateBoundedPattern(PathsD infillRegion, InfillPattern pattern, int layerIndex = -1)
        {
            // Create the infill bounding box that minimally cover the infill region
            var spacing = settings.NozzleDiameter;
            if (layerIndex == -1)
            {
                spacing = settings.InfillSparsity;
            }
            else
            {
                spacing = settings.SupportSparsity;
            }
            var bounds = Clipper.GetBounds(infillRegion);
            double minx = bounds.left;
            double maxx = bounds.right;
            double miny = bounds.top;
            double maxy = bounds.bottom;

            // Create basic infill lines
            PathsD grid = new PathsD();

            if (pattern == InfillPattern.Horizontal)
            {
                for (double y = miny; y <= maxy; y += spacing)
                {
                    // Horizontal line spanning bounding box
                    var p0 = new PointD(minx, y);
                    var p1 = new PointD(maxx, y);

                    var line = new PathD { p0, p1 };
                    grid.Add(line);
                }
            }
            else if (pattern == InfillPattern.Cross)
            {
                PointD center = new PointD((minx + maxx) / 2.0, (miny + maxy) / 2.0);

                // 1. Create horizontal lines (unrotated before clipping)
                if (layerIndex != -1)
                {
                    if (layerIndex % 2 == 0)
                    {
                        grid = addHorizontalLinesToGrid(grid, minx, maxx, miny, maxy, spacing, layerIndex);
                    }
                    else
                    {
                        grid = addVerticalLinesToGrid(grid, minx, maxx, miny, maxy, spacing, layerIndex);
                    }
                }
                else
                {
                    grid = addHorizontalLinesToGrid(grid, minx, maxx, miny, maxy, spacing, layerIndex);
                    grid = addVerticalLinesToGrid(grid, minx, maxx, miny, maxy, spacing, layerIndex);
                }
            }

            // Clip the infill lines to the infill region
            var clipper = new ClipperD();
            clipper.AddOpenSubject(grid);
            clipper.AddClip(infillRegion);

            PathsD insideClosed = new PathsD(); // represent polygons: not intersted here
            PathsD insideOpen = new PathsD(); // represent open lines: we want to clip to this
            clipper.Execute(ClipType.Intersection, FillRule.EvenOdd, insideClosed, insideOpen);

            // Remove degenerate lines and points and return the infill
            var boundedPattern = Clipper.SimplifyPaths(insideOpen, 1e-9, isClosedPath: false);
            return boundedPattern;
        }


        private PathsD addHorizontalLinesToGrid(PathsD grid, double minx, double maxx, double miny, double maxy, double spacing, int layerIndex)
        {
            for (double y = miny; y <= maxy; y += spacing)
            {
                var p0 = new PointD(minx, y);
                var p1 = new PointD(maxx, y);
                grid.Add(new PathD { p0, p1 });
            }
            return grid;
        }

        private PathsD addVerticalLinesToGrid(PathsD grid, double minx, double maxx, double miny, double maxy, double spacing, int layerIndex)
        {
            // 2. Create vertical lines
            for (double x = minx; x <= maxx; x += spacing)
            {
                var p0 = new PointD(x, miny);
                var p1 = new PointD(x, maxy);
                grid.Add(new PathD { p0, p1 });
            }
            return grid;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public PathsD Generate_Infill()
        {
            // Create the infill region
            PathsD innerMost = GetInnerShell();
            double infillOverlap = 0.10 * settings.NozzleDiameter; // overlap with outer wall
            double shrink = (settings.NozzleDiameter / 2.0) - infillOverlap;
            PathsD infillRegion = Clipper.InflatePaths(innerMost, -shrink, JoinType.Round, EndType.Polygon);

            // Exclude roof/floor regions if they exist
            // The filled floors are a set of concentric (sluitende)
            // regions. The calculate the difference it needs to find a
            // concentric region. When using the filled floors because all the
            // filling is also concetric it takes the smal hole inside the filling as the region.
            if (floorRegion != null && floorRegion.Count > 0)
                infillRegion = Clipper.Difference(infillRegion, floorRegion, FillRule.NonZero);
            if (roofRegion != null && roofRegion.Count > 0)
                infillRegion = Clipper.Difference(infillRegion, roofRegion, FillRule.NonZero);

            // Remove degenerate lines and points and return the infill
            this.infill = GenerateBoundedPattern(infillRegion, InfillPattern.Cross, -1);
            return this.infill;
        }

        public bool Is_Ready_To_Print()
        {
            return outerLayer != null && infill != null && checkedFloor && checkedRoof;
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
            Clipper.SimplifyPaths(rawSlice, 1e-9, isClosedPath: true);
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
