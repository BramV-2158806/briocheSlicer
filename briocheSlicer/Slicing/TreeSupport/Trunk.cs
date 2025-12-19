using HelixToolkit.Wpf;
using System.Diagnostics;
using System.Security.Policy;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing.TreeSupport
{
    internal class TrunkPath
    {
        // Grow direction
        private readonly Vector3D down = new Vector3D(0, 0, -1);

        private List<Point3D> points;
        private double touchAreaRadius;
        private double trunkRadius;
        private bool isDoneGrowing;
        private Point3D? currentPosition;
        private double maxCollisionDetectionDistance = 5;
        private double modelDistance = 2;

        public TrunkPath(double ClusterFaceSize, List<Point3D>? points = null)
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

            this.touchAreaRadius = AreaToRadius(ClusterFaceSize);
            this.trunkRadius = Math.Max(2, touchAreaRadius / 2);
            this.isDoneGrowing = false;
        }

        public bool IsDoneGrowing() { return this.isDoneGrowing; }
        public void SetIsDoneGorwing(bool value) { this.isDoneGrowing = value; }
        public double GetTrunkAreaSize() { return this.trunkRadius; }
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
        /// </summary>
        /// <returns>A Model3D representing the thickened trunk path.</returns>
        public Model3D Thicken() 
        {
            var material = new DiffuseMaterial(new SolidColorBrush(Colors.Brown));

            // Create cone at the first point
            if (points.Count >= 2)
            {
                return CreateTrunkGroup(material);
            }
            return new Model3DGroup();
        }

        private Model3DGroup CreateTrunkGroup(DiffuseMaterial material)
        {
            var modelGroup = new Model3DGroup();
            // 1. Calculate total branch length first
            double totalLength = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalLength += (points[i + 1] - points[i]).Length;
            }

            // 2. Calculate a constant taper rate
            // This distributes the radius change evenly from top to bottom
            double totalRadiusChange = touchAreaRadius - trunkRadius;
            double taperPerUnit = totalRadiusChange / totalLength;

            double currentRadius = touchAreaRadius;
            double distFromStart = 0;

            var meshBuilder = new MeshBuilder(false, false);

            // 3. Build geometry
            for (int i = 0; i < points.Count - 1; i++)
            {
                Point3D startPoint = points[i];
                Point3D endPoint = points[i + 1];
                Vector3D direction = endPoint - startPoint;
                double segmentHeight = direction.Length;

                if (segmentHeight > 0.001)
                {
                    // Linear interpolation for the next radius
                    double nextRadius = touchAreaRadius - ((distFromStart + segmentHeight) * taperPerUnit);

                    // Safety clamp
                    if (nextRadius < trunkRadius) nextRadius = trunkRadius;

                    meshBuilder.AddCone(startPoint, direction, currentRadius, nextRadius, segmentHeight, true, true, 16);

                    currentRadius = nextRadius;
                    distFromStart += segmentHeight;
                }
            }

            var geometry = new GeometryModel3D(meshBuilder.ToMesh(), material);
            modelGroup.Children.Add(geometry);

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
        /// Converts a touch area size to a radius that fits within that area.
        /// Assumes circular area where Area = π × r²
        /// </summary>
        /// <param name="areaSize">The area size to convert</param>
        /// <returns>The radius that would fit in the given area</returns>
        private double AreaToRadius(double areaSize)
        {
            return Math.Sqrt(areaSize / Math.PI);
        }
    }
}
