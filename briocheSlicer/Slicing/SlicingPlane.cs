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
        private TranslateTransform3D translateTransform;

        public SlicingPlane(Point3D center, int size = 100)
        {
            planeCenter = center;
            planeNormal = new Vector3D(0, 0, 1);
            planeSize = size;

            // Initialize the transform, this defines the position of the plane.
            // So we can change the transform and position instead of creating a new slicing
            // plane model.
            translateTransform = new TranslateTransform3D(planeCenter.X, planeCenter.Y, planeCenter.Z);

            model = Create_Model();
        }

        /// <summary>
        /// Creates the model of the slicing plane.
        /// </summary>
        /// <returns>The model of the slicing plane.</returns>
        private GeometryModel3D Create_Model()
        {
            // Create visual representation of the plane
            var rect = new RectangleVisual3D
            {
                Origin = new Point3D(0, 0, 0),
                Normal = planeNormal,
                LengthDirection = new Vector3D(1, 0, 0),
                Width = planeSize,
                Length = planeSize,
                Fill = Brushes.OrangeRed,
                Material = MaterialHelper.CreateMaterial(Brushes.OrangeRed, 0.5, 100, true)
            };

            // Apply the translation transform to position the plane at the center point
            var geometryModel = rect.Model;
            geometryModel.Transform = translateTransform;

            return geometryModel;
        }

        /// <summary>
        /// Updates the translationTransformation of the slicingplane model.
        /// In practice, changing the position of the slicing plane.
        /// </summary>
        /// <param name="newCenter">The new center around which the slicingplane will be drawn.</param>
        private void Update_Model_Position(Point3D newCenter)
        {
            translateTransform.OffsetX = newCenter.X;
            translateTransform.OffsetY = newCenter.Y;
            translateTransform.OffsetZ = newCenter.Z;
        }

        /// <summary>
        /// Gets a reference to the slicing plane model.
        /// </summary>
        /// <returns>A reference to the slicing plane model.</returns>
        public GeometryModel3D Get_Model()
        {
            return model;
        }

        /// <summary>
        /// Updates the Y position of the slicing plane.
        /// </summary>
        /// <param name="newY">The new Y coordinate for the plane's center</param>
        public void Update_Slicing_Plane_Y(double newZ)
        {
            var newCenter = new Point3D(planeCenter.X, planeCenter.Y, newZ);
            Set_Center(newCenter);
        }

        /// <summary>
        /// Updates the plane's center position and refreshes the model.
        /// </summary>
        /// <param name="newCenter"></param>
        private void Set_Center(Point3D newCenter)
        {
            planeCenter = newCenter;
            Update_Model_Position(newCenter);
        }

        public Point3D GetCenter()
        {
            return planeCenter;
        }
    }
}
