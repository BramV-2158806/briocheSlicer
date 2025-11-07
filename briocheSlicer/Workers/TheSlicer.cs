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
                return HashCode.Combine(Xq, Yq);
            }
        }

        private sealed class Snap2D
        {
            private readonly double _scale;
            public Snap2D(double eps)
            {
                _scale = 1.0 / eps;
            }
            public VertexKey Key(Point3D point)
            {
                return new VertexKey((long)Math.Round(point.X * _scale), (long)Math.Round(point.Y * _scale));
            }
        }

        private readonly struct UEdge : IEquatable<UEdge>
        {
            public readonly VertexKey k1, k2;
            public UEdge(VertexKey key1, VertexKey key2)
            {
                if (key1.Xq < key2.Xq || (key1.Xq == key2.Xq && key1.Yq <= key2.Yq))
                {
                    k1 = key1;
                    k2 = key2;
                }
                else
                {
                    k1 = key2;
                    k2 = key1;
                }
            }

            public bool Equals(UEdge other)
            {
                return k1.Equals(other.k1) && k2.Equals(other.k2);
            }
            public override bool Equals(object? obj)
            {
                return obj is UEdge e && Equals(e);
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        private static bool Close_By(double x1, double y1, double x2, double y2, double eps)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return dx * dx + dy * dy <= eps * eps;
        }

        private static List<BriocheEdge> BuildUniqueEdges(List<BriocheTriangle> triangles, double planeZ, double eps = EDGE_EPS)
        {
            var snap = new Snap2D(eps);
            var uniq = new HashSet<UEdge>();
            var outedges = new List<BriocheEdge>();

            foreach (var t in triangles)
            {
                var edge = t.Calculate_intersection(planeZ);
                if (edge == null) continue;

                var p1 = new Point3D(edge.Start.X, edge.Start.Y, planeZ);
                var p2 = new Point3D(edge.End.X, edge.End.Y, planeZ);

                if (Close_By(p1.X, p1.Y, p2.X, p2.Y, eps)) continue;

                var key1 = snap.Key(p1);
                var key2 = snap.Key(p2);
                var uedge = new UEdge(key1, key2);

                if (uniq.Add(uedge))
                {
                    outedges.Add(new BriocheEdge(p1, p2));
                }
            }
            return outedges;
        }

        /// <summary>
        /// Slices the one plane and creates a slice.
        /// </summary>
        /// <param name="triangles"></param>
        /// <returns></returns>
        public Slice Slice_Plane(List<BriocheTriangle> triangles, double planeZ)
        {
            List<BriocheEdge> edges = BuildUniqueEdges(triangles, planeZ);
            edges = EdgeUtils.MergeCollinear(edges, EDGE_EPS);
            return new Slice(edges, planeZ);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pureModel"> The original STL model loaded in by the system</param>
        /// <returns></returns>
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
