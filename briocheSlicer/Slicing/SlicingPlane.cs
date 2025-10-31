using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing
{
    internal class SlicingPlane
    {
        private Point3D planeCenter;
        private Vector3D planeNormal;
        private int planeSize;

        private GeometryModel3D model;

        public SlicingPlane(Point3D center, int size = 100)
        {
            planeCenter = center;
            planeNormal = new Vector3D(0, 0, 1);
            planeSize = size;

            model = Update_Model();
        }

        private GeometryModel3D Update_Model()
        {
            // Create plane
            var plane = new Plane3D(planeCenter, planeNormal);

            // Create visual representation of the plane
            var rect = new RectangleVisual3D
            {
                Origin = plane.Position,
                Normal = plane.Normal,
                LengthDirection = new Vector3D(1, 0, 0),
                Width = planeSize,
                Length = planeSize,
                Fill = Brushes.OrangeRed,
                Material = MaterialHelper.CreateMaterial(Brushes.OrangeRed, 0.5, 100, true)
            };

            // Safe for later
            model = rect.Model;
            return rect.Model;
        }

        public GeometryModel3D Get_Model()
        {
            return model;
        }

        /// <summary>
        /// Updates the plane's center position and refreshes the model.
        /// </summary>
        /// <param name="newCenter"></param>
        public void Set_Center(Point3D newCenter)
        {
            planeCenter = newCenter;
            Update_Model();

        }
    }
}
