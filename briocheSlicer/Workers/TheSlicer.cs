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

        public TheSlicer()
        {
        }

        public GeometryModel3D Create_Slicing_plane(Rect3D modelBounds)
        {
            // Plane should be centered on the model center.
            var modelCenter = new Point3D(
                modelBounds.X + modelBounds.SizeX / 2,
                modelBounds.Y + modelBounds.SizeY / 2,
                modelBounds.Z + modelBounds.SizeZ / 2);

            slicingPlane = new SlicingPlane(modelCenter);
            return slicingPlane.Get_Model();
        }
    }
}
