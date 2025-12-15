using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

/// DISCLDAIMER
/// This class is mainly written by AI
/// Then reviewed and updated by human (me)
/// It represents the visual builplate.

namespace briocheSlicer.Slicing
{
    internal class BuildPlate
    {
        private GeometryModel3D model;
        private TranslateTransform3D translateTransform;
        private Point3D plateCenter;
        private int plateSize;

        public BuildPlate(Rect3D modelbounds, int size = 256)
        {
            plateCenter = CalculateBoundsCenter(modelbounds);
            plateSize = size;

            translateTransform = new TranslateTransform3D(plateCenter.X, plateCenter.Y, plateCenter.Z);
            model = CreateModel();
        }

        public static Point3D CalculateBoundsCenter(Rect3D modelbounds)
        {
            return new Point3D(
                modelbounds.X + modelbounds.SizeX / 2,
                modelbounds.Y + modelbounds.SizeY / 2,
                modelbounds.Z); // Bottom of the model
            
        }

        private GeometryModel3D CreateModel()
        {
            var rect = new RectangleVisual3D
            {
                Origin = new Point3D(0, 0, 0),
                Normal = new Vector3D(0, 0, 1), // Flat horizontal plane
                LengthDirection = new Vector3D(1, 0, 0),
                Width = plateSize,
                Length = plateSize,
                Fill = Brushes.DarkGray,
                Material = MaterialHelper.CreateMaterial(Brushes.DarkGray, 0.3, 50, false)
            };

            var geometryModel = rect.Model;
            geometryModel.Transform = translateTransform;

            return geometryModel;
        }

        /// <summary>
        /// Updates the build plate position to be at the bottom of the model.
        /// </summary>
        /// <param name="modelBounds">Bounding box of the current model</param>
        public void UpdatePosition(Rect3D modelBounds)
        {
            var newCenter = CalculateBoundsCenter(modelBounds);
            SetCenter(newCenter);
        }

        private void SetCenter(Point3D newCenter)
        {
            plateCenter = newCenter;
            translateTransform.OffsetX = newCenter.X;
            translateTransform.OffsetY = newCenter.Y;
            translateTransform.OffsetZ = newCenter.Z;
        }

        public GeometryModel3D GetModel()
        {
            return model;
        }

        public Point3D GetCenter()
        {
            return plateCenter;
        }
    }
}
