using HelixToolkit.Wpf;
using System.Diagnostics;
using System.Security.Policy;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

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
        private double modelDistance = 2;

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
        public void Grow(double growthSpeed, Model3DGroup pureModel) 
        {
            if (currentPosition == null)
                return;

            Point3D origin = currentPosition.Value;
            Vector3D direction = down;
            direction.Normalize();

            (Point3D hitPoint, Vector3D normal, int v1, int v2, int v3)? bestHit = null;
            double closestDist = double.MaxValue;

            // Manually test against each geometry in the pure model
            foreach (var child in pureModel.Children)
            {
                if (child is GeometryModel3D geometryModel && geometryModel.Geometry is MeshGeometry3D mesh)
                {
                    var hit = RayMeshIntersection(origin, direction, mesh, geometryModel.Transform);
                    if (hit != null)
                    {
                        double dist = (hit.Value.hitPoint - origin).Length;
                        if (dist <= maxCollisionDetectionDistance && dist < closestDist)
                        {
                            closestDist = dist;
                            bestHit = hit;
                        }
                    }
                }
            }

            if (bestHit != null)
            {
                HandleHit(bestHit.Value, growthSpeed);
            }
            else
            {
                // Grow straight down
                Point3D newTop = currentPosition.Value + down * growthSpeed;
                newTop.Z = Math.Max(0, newTop.Z);
                points.Add(newTop);
                currentPosition = newTop;
            }

            Debug.WriteLineIf(true, "Current Z value: " + currentPosition.Value.Z);
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
        private void HandleHit((Point3D hitPoint, Vector3D normal, int v1, int v2, int v3) hit, double growthSpeed) 
        {
            var normal = hit.normal;
            normal.Normalize();

            Point3D nextPos;
            // Make sure it does not "keep bouncing off" the build plate
            if (normal == new Vector3D(0, 0, 1) && currentPosition != null) 
            {
                nextPos = currentPosition.Value + down * growthSpeed;
            }
            else
            {
                Point3D hitPoint = hit.hitPoint;
                nextPos = hitPoint + normal * modelDistance;
            }

            nextPos.Z = Math.Max(0, nextPos.Z);
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

                if (direction.Length <= 0.1f)
                {
                    return CreateSphere(tipPoint, material);
                }

                double coneHeight = direction.Length;
                
                var meshBuilder = new MeshBuilder(false, false);
                meshBuilder.AddCone(tipPoint, direction, touchAreaSize, trunkAreaSize, coneHeight, true, true, 16);
                
                var coneGeometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
                modelGroup.Children.Add(coneGeometry);
            }
            else if (points.Count == 1)
            {
                // Just a single point - create a small sphere
                return CreateSphere(points[0], material);
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

        private Model3DGroup CreateSphere(Point3D position, DiffuseMaterial material) 
        {
            var modelGroup = new Model3DGroup();
            var meshBuilder = new MeshBuilder(false, false);
            meshBuilder.AddSphere(position, trunkAreaSize / 2, 16, 16);
            var sphereGeometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
            modelGroup.Children.Add(sphereGeometry);
            return modelGroup;
        }

        /// <summary>
        /// Performs ray-mesh intersection test manually.
        /// Returns hit point, normal, and vertex indices if intersection found.
        /// ** Disclaimer: function written by AI**
        /// </summary>
        private (Point3D hitPoint, Vector3D normal, int v1, int v2, int v3)? RayMeshIntersection(
            Point3D origin, Vector3D direction, MeshGeometry3D mesh, Transform3D? transform)
        {
            var positions = mesh.Positions;
            var indices = mesh.TriangleIndices;

            (Point3D hitPoint, Vector3D normal, int v1, int v2, int v3)? closestHit = null;
            double closestDist = double.MaxValue;

            for (int i = 0; i < indices.Count; i += 3)
            {
                var p0 = positions[indices[i]];
                var p1 = positions[indices[i + 1]];
                var p2 = positions[indices[i + 2]];

                // Apply transform if present
                if (transform != null && !transform.Value.IsIdentity)
                {
                    p0 = transform.Transform(p0);
                    p1 = transform.Transform(p1);
                    p2 = transform.Transform(p2);
                }

                // Möller–Trumbore intersection algorithm
                var edge1 = p1 - p0;
                var edge2 = p2 - p0;
                var h = Vector3D.CrossProduct(direction, edge2);
                var a = Vector3D.DotProduct(edge1, h);

                if (a > -0.00001 && a < 0.00001)
                    continue; // Ray is parallel to triangle

                var f = 1.0 / a;
                var s = origin - p0;
                var u = f * Vector3D.DotProduct(s, h);

                if (u < 0.0 || u > 1.0)
                    continue;

                var q = Vector3D.CrossProduct(s, edge1);
                var v = f * Vector3D.DotProduct(direction, q);

                if (v < 0.0 || u + v > 1.0)
                    continue;

                var t = f * Vector3D.DotProduct(edge2, q);

                if (t > 0.00001 && t < closestDist)
                {
                    closestDist = t;
                    var hitPoint = origin + direction * t;
                    var normal = Vector3D.CrossProduct(edge1, edge2);
                    normal.Normalize();
                    closestHit = (hitPoint, normal, indices[i], indices[i + 1], indices[i + 2]);
                }
            }

            return closestHit;
        }

        /// <summary>
        /// Creates a RayMeshGeometry3DHitTestResult-like object from intersection data.
        /// ** Disclaimer: function written by AI**
        /// </summary>
        private RayMeshGeometry3DHitTestResult CreateHitTestResult(
            (Point3D hitPoint, Vector3D normal, int v1, int v2, int v3) hit, MeshGeometry3D mesh)
        {
            // We need to create a temporary visual for the hit test result
            var dummyVisual = new ModelVisual3D();
            var geometryModel = new GeometryModel3D(mesh, new DiffuseMaterial(Brushes.Transparent));
            dummyVisual.Content = geometryModel;

            // Create hit test parameters and perform a minimal hit test to get a valid result object
            var rayParams = new RayHitTestParameters(hit.hitPoint, new Vector3D(0, 0, 1));
            RayMeshGeometry3DHitTestResult? result = null;

            VisualTreeHelper.HitTest(dummyVisual, null, (r) =>
            {
                if (r is RayMeshGeometry3DHitTestResult meshResult)
                    result = meshResult;
                return HitTestResultBehavior.Stop;
            }, rayParams);

            return result!;
        }
        }
}
