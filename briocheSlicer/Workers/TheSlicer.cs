using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using briocheSlicer.Slicing;

namespace briocheSlicer.Workers
{
    internal class TheSlicer
    {
        private SlicingPlane? slicingPlane;
        private int slicingPlaneOverhang;

        public TheSlicer()
        {
            slicingPlaneOverhang = 20; // mm
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
        /// Updates the Y position of the slicing plane.
        /// </summary>
        /// <param name="newY">The new Y coordinate for the plane's center</param>
        public void Update_Slicing_Plane_Y(double newZ)
        {
            if (slicingPlane != null)
            {
                var currentCenter = slicingPlane.GetCenter();
                var newCenter = new Point3D(currentCenter.X, currentCenter.Y, newZ);
                slicingPlane.Set_Center(newCenter);
            }
        }

    }
}
