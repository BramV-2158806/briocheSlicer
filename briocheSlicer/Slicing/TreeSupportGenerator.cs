using briocheSlicer.Gcode;
using briocheSlicer.Workers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public SeedPoint(double x, double y, double z)
        {
            this.point = new Point3D(x, y, z);
        }

        public SeedPoint(Point3D v0, Point3D v1, Point3D v2)
        {
            this.point = new Point3D(
            (v0.X + v1.X + v2.X) / 3.0,
            (v0.Y + v1.Y + v2.Y) / 3.0,
            (v0.Z + v1.Z + v2.Z) / 3.0);
        }
    }

    internal class SeedCluster
    {
        private SeedPoint center;
        private double radius;

        public SeedCluster(SeedPoint center, double radius) 
        {
            this.center = center;
            this.radius = radius;
        }
    }

    internal class TreeSupportGenerator
    {
        public TreeSupportGenerator() { }

        public Model3DGroup LetTheForrestGrow(Model3DGroup pureModel)
        {
            // Identify seeds
            List<SeedPoint> seeds = SearchForSeeds(pureModel, new Vector3D(0, 0, 1));

            // Cluster seeds
            

            // Generate paths

            // Generate trunks from paths

            // Add trunks to model
        }

        private List<SeedPoint> SearchForSeeds(Model3D model, Vector3D up)
        {
            List<SeedPoint> seeds = new List<SeedPoint>();

            // If it's a group, we call the function for each child
            // And collect all the seeds.
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    seeds.AddRange(SearchForSeeds(child, up));
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
