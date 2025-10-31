using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Workers
{
    internal class TheSlicer
    {
        private Vector3D slicingPlaneNormal;

        public TheSlicer()
        {
            slicingPlaneNormal = new Vector3D(0, 0, 1);
        }

        public GeometryModel3D Create_Slicing_plane(Rect3D modelBounds)
        {
            // Point and normal define plane
            var modelCenter = new Point3D(
                modelBounds.X + modelBounds.SizeX / 2,
                modelBounds.Y + modelBounds.SizeY / 2,
                modelBounds.Z + modelBounds.SizeZ / 2);
            var pointOnPlane = modelCenter;

            // Define plane
            var plane = new Plane3D(pointOnPlane, slicingPlaneNormal);

            // Create visual representation of the plane
            var rect = new RectangleVisual3D
            {
                Origin = plane.Position,
                Normal = plane.Normal,
                LengthDirection = new Vector3D(1, 0, 0),
                Width = 100,
                Length = 100,
                Fill = Brushes.OrangeRed,
                Material = MaterialHelper.CreateMaterial(Brushes.OrangeRed, 0.5, 100, true)
            };
            return rect.Model;
        }
    }
}
