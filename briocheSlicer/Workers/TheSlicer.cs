using briocheSlicer.Slicing;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Workers
{
    internal partial class TheSlicer
    {
        private const double EDGE_EPS = 1e-6;
        private SlicingPlane? slicingPlane;
        private int slicingPlaneOverhang;

        private double? layerHeight;
        private double? nozzleDiameter;

        public TheSlicer()
        {
            slicingPlaneOverhang = 20;
        }

        public void Set_Layer_Height(double height)
        {
            layerHeight = height;
        }
        public void Set_Nozzle_Diameter(double diameter)
        {
            nozzleDiameter = diameter;
        }

        /// <summary>
        /// Creates the slicing plane object.
        /// </summary>
        /// <param name="modelBounds">The cube bounding box of the 3D model.</param>
        /// <returns>The 3D visualisation of the slicing plane. A GeometryModel3D.</returns>
        public GeometryModel3D Create_Slicing_plane(Rect3D modelBounds)
        {
            // Plane should be centered on the model center.
            var modelCenter = new Point3D(
                modelBounds.X + modelBounds.SizeX / 2,
                modelBounds.Y + modelBounds.SizeY / 2,
                modelBounds.Z + modelBounds.SizeZ / 2);

            // Calculate the slicing plane size based on max model dimensions plus overhang
            double maxDimension = Math.Max(modelBounds.SizeX, modelBounds.SizeY);
            int planeSize = (int)(maxDimension + slicingPlaneOverhang);

            slicingPlane = new SlicingPlane(modelCenter, planeSize);
            return slicingPlane.Get_Model();
        }


        /// <summary>
        /// Gets the slicing plane object.
        /// </summary>
        /// <pre>
        /// The slicing plane should be created first using Create_Slicing_plane.
        /// </pre>
        /// <returns></returns>
        public SlicingPlane Get_Slicing_Plane()
        {
            return slicingPlane!;
        }

       
        private static bool Close_By(double x1, double y1, double x2, double y2, double eps)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return dx * dx + dy * dy <= eps * eps;
        }

        /// <summary>
        /// Takes a set of edges and creates a set of unique edges by removing duplicates
        /// and empty edges.
        /// </summary>
        /// <param name="triangles"></param>
        /// <param name="planeZ"></param>
        /// <param name="eps"></param>
        /// <returns></returns>
        private static List<BriocheEdge> Build_Unique_Edges(List<BriocheEdge> inEdges, double planeZ, double eps = EDGE_EPS)
        {
            var snap = new Snap2D(eps);
            var uniq = new HashSet<UnorderdEdge>();
            var outedges = new List<BriocheEdge>();

            foreach (var edge in inEdges)
            {
                // If the start end end point are close, this edge is useless.
                if (Close_By(edge.Start.X, edge.Start.Y, edge.End.X, edge.End.Y, eps)) continue;

                // Create the normalised representattio of the edge
                var key1 = snap.Key(edge.Start);
                var key2 = snap.Key(edge.End);
                var uedge = new UnorderdEdge(key1, key2);

                // Add if we have not seen this edge before.
                if (uniq.Add(uedge)) // returns true if was added, and only adds if not already present.
                {
                    outedges.Add(new BriocheEdge(edge.Start, edge.End));
                }
            }
            return outedges;
        }

        /// <summary>
        /// Collects the intersection edges from a set of traingles at a given plane Z.
        /// </summary>
        /// <param name="triangles"></param>
        /// <param name="planeZ"></param>
        /// <returns></returns>
        public static List<BriocheEdge> Intersections_Of_Plane(List<BriocheTriangle> triangles, double planeZ)
        {
            var edges = new List<BriocheEdge>();
            foreach (var tri in triangles)
            {
                var triEdges = tri.Calculate_Intersection(planeZ);
                edges.AddRange(triEdges);
            }
            return edges;
        }

        /// <summary>
        /// Slices the one plane and creates a slice.
        /// </summary>
        /// <param name="triangles"></param>
        /// <returns></returns>
        public Slice Slice_Plane(List<BriocheTriangle> triangles, double planeZ)
        {
            // Collect the edges from triangle intersections
            List<BriocheEdge> intersection_edges = Intersections_Of_Plane(triangles, planeZ);
            List<BriocheEdge> edges = Build_Unique_Edges(intersection_edges, planeZ);

            // Remove unnecessary edges.
            edges = EdgeUtils.Merge_Collinear(edges, EDGE_EPS);

            // Create the slice and return.
            return new Slice(edges, planeZ);
        }

        /// <summary>
        /// Slices the entire model.
        /// </summary>
        /// <param name="pureModel"> The original STL model loaded in by the system</param>
        /// <returns>Gives back the sliced BriocheModel</returns>
        public BriocheModel Slice_Model(Model3DGroup pureModel)
        {
            // callculate the amount of layers

            // call the slice current plane function for each layer
            // make sure no layers overlap (mid layer from the slides)

            // Add all the slices to form the brioche model.

            List<BriocheTriangle> triangels = BriocheTriangle.Get_Triangles_From_Model(pureModel);
            Slice slice = Slice_Plane(triangels, slicingPlane!.GetZ());
            return new BriocheModel(new List<Slice> { slice });
        }
    }
}
