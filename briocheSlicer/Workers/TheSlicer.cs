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
    internal class TheSlicer
    {
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

        /// <summary>
        /// Slices the one plane and creates a slice.
        /// </summary>
        /// <param name="triangles"></param>
        /// <returns></returns>
        private Slice Slice_Plane(List<BriocheTriangle> triangles, double planeZ)
        {
            List<BriocheEdge> edges = new List<BriocheEdge>();
            foreach (var triangle in triangles)
            {
                BriocheEdge? intersectionLine = triangle.Calculate_intersection(planeZ);
                if (intersectionLine != null)
                {
                    edges.Add(intersectionLine);
                }
            }

            return new Slice(edges);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pureModel"> The original STL model loaded in by the system</param>
        /// <returns></returns>
        public BriocheModel Slice_Model(Model3DGroup pureModel)
        {
            // callculate the amount of layers

            // call the scice current plane function for each layer
            // make sure no layers overlap (mid layer from the slides)

            // Add all the slices to form the brioche model.

            List<BriocheTriangle> triangels = BriocheTriangle.Get_Triangles_From_Model(pureModel);
            Slice slice = Slice_Plane(triangels, slicingPlane!.GetZ());
            return new BriocheModel(new List<Slice> { slice });
        }
    }
}
