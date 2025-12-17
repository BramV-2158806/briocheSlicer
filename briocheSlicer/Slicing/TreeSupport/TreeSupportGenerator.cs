using HdbscanSharp.Distance;
using HdbscanSharp.Runner;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing.TreeSupport
{
    /// <summary>
    /// These classes implement a pre processing step to enable tree trunk
    /// support. We create a modified version of the pureModel that includes 
    /// tree trunks. In the main slicing step we slice this model including 
    /// the tree trunks but do this without support.
    /// </summary>
    /// 
    internal class TreeSupportGenerator
    {
        // Clustering variables
        private readonly int minClusterPoints = 1;
        private readonly int minClusterSize = 2;

        // Path Generation variables
        private readonly float generationSpeed = 2.0f;

        private readonly float connectionToModelDistance = -0.2f;

        public TreeSupportGenerator() { }

        /// <summary>
        /// Main tree support function.
        /// </summary>
        /// <param name="pureModel"></param>
        /// <returns></returns>
        public Model3DGroup LetTheForrestGrow(Model3DGroup pureModel)
        {
            // Identify seeds
            List<SeedPoint> seeds = SearchForSeeds(pureModel);

            // Cluster seeds
            List<SeedCluster> clusters = ClusterSeeds(seeds);

            // Generate paths and trunk models
            Forrest forrest  = new Forrest(generationSpeed, clusters);
            Model3DGroup trunkModels = forrest.GrowAround(pureModel);

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
                var downNormal = new Vector3D(0,0,-1);
                downNormal.Normalize();

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

                    double angle = CalculateAngle(triNormal, downNormal);

                    // If the angle with of the face normal with the downNormal is less then
                    // 45 degrees, this item needs support. In this way we try to mitigate the
                    // ambiguity between top and bottom facing surfaces.
                    if (angle < 45)
                    {
                        var v0 = vertices[i0];
                        var v1 = vertices[i1];
                        var v2 = vertices[i2];

                        double faceSize = SeedPoint.CalculateTriangleSize(v0, v1, v2);

                        // Calculate centroid of triangle
                        // This well be the seedpoint position
                        Point3D centroid = new Point3D(
                            (v0.X + v1.X + v2.X) / 3.0,
                            (v0.Y + v1.Y + v2.Y) / 3.0,
                            (v0.Z + v1.Z + v2.Z) / 3.0);

                        // We move the centroid a little in the direction
                        // Of the triangle/face normal. Making support easier to remove.
                        Point3D offsetCentroid = new Point3D(
                            centroid.X - triNormal.X * connectionToModelDistance,
                            centroid.Y - triNormal.Y * connectionToModelDistance,
                            centroid.Z - triNormal.Z * connectionToModelDistance);

                        seeds.Add(new SeedPoint(offsetCentroid, faceSize));
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
                List<SeedPoint> clusterPoints = cluster.Value;

                // If the cluster is empty we skip it.
                if (clusterPoints.Count == 0) { continue; }

                // Calculate centriod position
                Point3D centeroidPosition = CalculateClusterCentroid(clusterPoints);

                // Calculate cluster size
                double clusterSize = CalculateClusterSize(clusterPoints);

                // Create the SeedCluster
                SeedCluster seedCluster = new SeedCluster(new SeedPoint(centeroidPosition, clusterSize), clusterSize, clusterId, clusterPoints.Count);

                seedClusters.Add(seedCluster);
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

        /// <summary>
        /// Cluster size is the sife of all the face of the cluster points
        /// sizes combined.
        /// </summary>
        /// <param name="clusterPoints"></param>
        /// <returns></returns>
        private double CalculateClusterSize(List<SeedPoint> clusterPoints)
        {
            double size = 0;
            foreach (var cluster in clusterPoints)
            {
                size += cluster.faceSize;
            }
            return size;
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
