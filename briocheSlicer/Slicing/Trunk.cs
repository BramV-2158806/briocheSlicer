using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace briocheSlicer.Slicing
{
    internal class TrunkPath
    {
        // Grow direction
        private readonly Vector3D down = new Vector3D(0, 0, -1);

        private List<Point3D> points;
        private double touchAreaSize;
        private double trunkAreaSize;
        private bool isDoneGrowing;
        private Point3D? currentPosition;
        private double maxCollisionDetectionDistance = 5;
        private double modelDistance = 0.80;

        public TrunkPath(double touchAreaSize, List<Point3D>? points = null)
        {
            if (points == null)
            {
                this.points = new List<Point3D>();
                this.currentPosition = null;
            }
            else 
            { 
                this.points = points;
                this.currentPosition = points.Last();
            }

            this.touchAreaSize = touchAreaSize;
            this.trunkAreaSize = Math.Max(2, touchAreaSize / 2);
            this.isDoneGrowing = false;
        }

        public bool IsDoneGrowing() { return this.isDoneGrowing; }
        public void SetIsDoneGorwing(bool value) { this.isDoneGrowing = value; }
        public double GetTrunkAreaSize() { return this.trunkAreaSize; }
        public Point3D? GetCurrentPosition() { return this.currentPosition; }

        /// <summary>
        /// Grow one itteration down or away from the model.
        /// Checks if done growing and sets the variable.
        /// </summary>
        /// <param name="growthSpeed"></param>
        /// <param name="modelVisual"></param>
        public void Grow(double growthSpeed, ModelVisual3D modelVisual) 
        {
            if (currentPosition == null)
                return;

            // Ray origin = current trunk position
            Point3D origin = currentPosition.Value;
            Vector3D direction = down;
            direction.Normalize();

            var rayParams = new RayHitTestParameters(origin, direction);

            RayMeshGeometry3DHitTestResult? bestHit = null;

            HitTestResultCallback callback = result =>
            {
                var rayResult = result as RayHitTestResult;
                if (rayResult is RayMeshGeometry3DHitTestResult meshHit)
                {
                    // Optionally filter by distance from origin
                    double dist = (meshHit.PointHit - origin).Length;
                    if (dist <= maxCollisionDetectionDistance)
                    {
                        // Keep the nearest hit within the allowed distance
                        if (bestHit == null || dist < (bestHit.PointHit - origin).Length)
                            bestHit = meshHit;
                    }
                }
                return HitTestResultBehavior.Continue;
            };

            VisualTreeHelper.HitTest(modelVisual, null, callback, rayParams);

            if (bestHit != null)
            {
                HandleHit(bestHit, growthSpeed);
            }
            else
            {
                // Grow straight down
                Point3D newTop = currentPosition.Value + down * growthSpeed;
                points.Add(newTop);
                currentPosition = newTop;
            }

            if (currentPosition != null && currentPosition.Value.Z <= 0) 
            {
                isDoneGrowing = true;
            }
        }

        /// <summary>
        /// Handles a hit with the model.
        /// Growing away from the model to the point of the hit.
        /// Since we now nothin else wil colide till tis point.
        /// </summary>
        /// <param name="hit"></param>
        /// <param name="growthSpeed"></param>
        private void HandleHit(RayMeshGeometry3DHitTestResult hit, double growthSpeed) 
        {
            var mesh = hit.MeshHit;

            // Triangle vertices
            var p0 = mesh.Positions[hit.VertexIndex1];
            var p1 = mesh.Positions[hit.VertexIndex2];
            var p2 = mesh.Positions[hit.VertexIndex3];

            // triangle face normal (robust if vertex normals are not set nicely)
            Vector3D normal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
            normal.Normalize();

            Point3D hitPoint = hit.PointHit;
            Point3D nextPos = hitPoint + normal * modelDistance;

            points.Add(nextPos);
            currentPosition = nextPos;
        }

        /// <summary>
        /// Merge two paths together. The other path should stop growing
        /// and this one is the one that can keep growing.
        /// </summary>
        /// <param name="other"></param>
        public void Merge(TrunkPath other)
        {
            points.AddRange(other.points);
        }

        /// <summary>
        /// Creates a 3D model with a cone at the first point and cylinders connecting subsequent points.
        /// ** Disclaimer: this function is written by AI **
        /// </summary>
        /// <returns>A Model3D representing the thickened trunk path.</returns>
        public Model3D Thicken() 
        {
            if (points.Count == 0)
                return new Model3DGroup();

            var modelGroup = new Model3DGroup();
            var material = new DiffuseMaterial(new SolidColorBrush(Colors.Brown));

            // Create cone at the first point
            if (points.Count >= 2)
            {
                Point3D tipPoint = points[0];
                Point3D nextPoint = points[1];
                Vector3D direction = nextPoint - tipPoint;
                double coneHeight = direction.Length;
                
                var meshBuilder = new MeshBuilder(false, false);
                meshBuilder.AddCone(tipPoint, direction, trunkAreaSize, trunkAreaSize, coneHeight, true, true, 16);
                
                var coneGeometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
                modelGroup.Children.Add(coneGeometry);
            }
            else if (points.Count == 1)
            {
                // Just a single point - create a small sphere
                var meshBuilder = new MeshBuilder(false, false);
                meshBuilder.AddSphere(points[0], trunkAreaSize / 2, 16, 16);
                var sphereGeometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
                modelGroup.Children.Add(sphereGeometry);
                return modelGroup;
            }

            // Create cylinders connecting the rest of the points
            for (int i = 1; i < points.Count - 1; i++)
            {
                Point3D startPoint = points[i];
                Point3D endPoint = points[i + 1];
                Vector3D direction = endPoint - startPoint;
                double length = direction.Length;

                if (length > 0)
                {
                    var meshBuilder = new MeshBuilder(false, false);
                    meshBuilder.AddCylinder(startPoint, endPoint, trunkAreaSize, 16);
                    
                    var cylinderGeometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
                    modelGroup.Children.Add(cylinderGeometry);
                }
            }

            return modelGroup;
        }
    }
}
