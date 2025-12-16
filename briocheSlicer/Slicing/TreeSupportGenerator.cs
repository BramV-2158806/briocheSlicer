using HdbscanSharp.Distance;
using HdbscanSharp.Hdbscanstar;
using HdbscanSharp.Runner;
using System.Windows.Media.Media3D;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing
{
    /// <summary>
    /// These classes implement a pre processing step to enable tree trunk
    /// support. We create a modified version of the pureModel that includes 
    /// tree trunks. In the main slicing step we slice this model including 
    /// the tree trunks but do this without support.
    /// </summary>
    /// 

    internal class SeedPoint
    {
        private Point3D point { get; }
        public double x { get; }
        public double y { get; }
        public double z { get; }

        public SeedPoint(double x, double y, double z)
        {
            this.point = new Point3D(x, y, z);
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public SeedPoint(Point3D point)
        {
            this.point = point;
            this.x = point.X;
            this.y = point.Y;
            this.z= point.Z;
        }

        public SeedPoint(Point3D v0, Point3D v1, Point3D v2)
        {
            this.point = new Point3D(
            (v0.X + v1.X + v2.X) / 3.0,
            (v0.Y + v1.Y + v2.Y) / 3.0,
            (v0.Z + v1.Z + v2.Z) / 3.0);
            this.x = point.X;
            this.y = point.Y;
            this.z = point.Z;
        }
    }

    internal class SeedCluster
    {
        private SeedPoint center;
        private double radius;
        private int clusterId;
        private int numPoints;

        public SeedCluster(SeedPoint center, double radius, int clusterId, int numPoints) 
        {
            this.center = center;
            this.radius = radius;
            this.clusterId = clusterId;
            this.numPoints = numPoints;
        }
    }

    internal class TreeSupportGenerator
    {
        // Directions
        private readonly Vector3D up = new Vector3D(0, 0, 1);

        // Clustering variables
        private readonly int minClusterPoints = 4;
        private readonly int minClusterSize = 4;

        // Path Generation variables
        private readonly float generationSpeed = 2.0f;

        public TreeSupportGenerator() { }

        public Model3DGroup LetTheForrestGrow(Model3DGroup pureModel, ModelVisual3D scene)
        {
            // Identify seeds
            List<SeedPoint> seeds = SearchForSeeds(pureModel);

            // Cluster seeds
            List<SeedCluster> clusters = ClusterSeeds(seeds);

            // Generate paths and trunk models
            Forrest forrest  = new Forrest(generationSpeed, clusters);
            Model3DGroup trunkModels = forrest.GrowAround(scene);

            // Add trunks to model
            pureModel.Children.Add(trunkModels);

            return pureModel;
        }

        private List<SeedPoint> SearchForSeeds(Model3D model)
        {
            List<SeedPoint> seeds = new List<SeedPoint>();

            // If it's a group, we call the function for each child
            // And collect all the seeds.
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    seeds.AddRange(SearchForSeeds(child));
            }

            else if (model is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
            {
                // Collect the mesh indices, which point to the vertices and there respected normals
                var indices = mesh.TriangleIndices;
                var normals = mesh.Normals;
                var vertices = mesh.Positions;

                // Normalise the up vector
                var upNormal = up;
                upNormal.Normalize();

                // We loop over the triangles. Each triangle consist of three vertices which are
                // defined in the indices array.
                for (int t = 0; t < indices.Count; t += 3) // TODO: Implement in CUDA
                {
                    // Collect the indices for this traingle
                    int i0 = indices[t];
                    int i1 = indices[t + 1];
                    int i2 = indices[t + 2];

                    // In the same way we collect the normals
                    Vector3D n0 = normals[i0];
                    Vector3D n1 = normals[i1];
                    Vector3D n2 = normals[i2];

                    // We calculate the average normal for this triangle
                    // AI helped with this idea.
                    Vector3D triNormal = n0 + n1 + n2;
                    triNormal.Normalize();

                    double angle = CalculateAngle(triNormal, upNormal);

                    // If the angle is bigger then 90 (perpendicular) + 45 (Self supporting)
                    // it needs support
                    if (angle >= 135)
                    {
                        var v0 = vertices[i0];
                        var v1 = vertices[i1];
                        var v2 = vertices[i2];

                        seeds.Add(new SeedPoint(v0, v1, v2));
                    } 
                }
            } 
            return seeds;
        }

        private List<SeedCluster> ClusterSeeds(List<SeedPoint> seeds)
        {
            // Use the HBDscan package to cluster the seed points
            var clusters = HdbscanRunner.Run(seeds, point => new float[] { (float)point.x, (float)point.y }, minClusterPoints, minClusterSize, GenericEuclideanDistance.GetFunc);

            // Post process the clusters to save to SeedClusters
            List<SeedCluster> seedClusters = new List<SeedCluster>();
            foreach (var cluster in clusters.Groups) // TODO: parallel with CUDA
            {
                // Extract the cluster ID (might be usefull)
                int clusterId = cluster.Key;

                // Extract the points belonging to this cluster.
                var clusterPoints = cluster.Value;

                // If the cluster is empty we skip it.
                if (clusterPoints.Count == 0) { continue; }

                // Calculate centriod position
                Point3D centeroidPosition = CalculateClusterCentroid(clusterPoints);

                // Calculate cluster size
                double clusterRadius = CalculateClusterRadius(clusterPoints, centeroidPosition);

                // Create the SeedCluster
                SeedCluster seedCluster = new SeedCluster(new SeedPoint(centeroidPosition), clusterRadius, clusterId, clusterPoints.Count);
            }
            return seedClusters;
        }

        private Point3D CalculateClusterCentroid(List<SeedPoint> clusterPoints)
        {
            double sumX = 0, sumY = 0, sumZ = 0;
            foreach (var p in clusterPoints)
            {
                sumX += p.x;
                sumY += p.y;
                sumZ += p.z;
            }

            double cx = sumX / clusterPoints.Count;
            double cy = sumY / clusterPoints.Count;
            double cz = sumZ / clusterPoints.Count;
            return new Point3D(cx, cy, cz);
        }

        private double CalculateClusterRadius(List<SeedPoint> clusterPoints, Point3D centeroid)
        {
            double maxDistSq = 0;
            foreach (var p in clusterPoints)
            {
                double dx = p.x - centeroid.X ;
                double dy = p.y - centeroid.Y;
                double dz = p.z - centeroid.Z;
                double d2 = dx * dx + dy * dy + dz * dz;
                if (d2 > maxDistSq) maxDistSq = d2;
            }

            double radius = Math.Sqrt(maxDistSq);
            return radius;
        }

        private double CalculateAngle(Vector3D v1, Vector3D v2)
        {
            double dot = Vector3D.DotProduct(v1, v2);
            dot = Math.Max(-1.0, Math.Min(1.0, dot));
            double angleRad = Math.Acos(dot);
            double angleDeg = angleRad * 180.0 / Math.PI;
            return angleDeg;
        }
    }
}
