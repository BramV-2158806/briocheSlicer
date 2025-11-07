using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing
{
    /// <summary>
    /// Represents a triangle in 3D space with three vertices.
    /// The HelixToolkit version is a 2D triangle and omits Z coordinate.
    /// </summary>
    internal class BriocheTriangle
    {
        public Point3D V1 { get; set; }
        public Point3D V2 { get; set; }
        public Point3D V3 { get; set; }

        public BriocheTriangle(Point3D v1, Point3D v2, Point3D v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
        }

        /// <summary>
        /// Gets all three vertices of the triangle as a list.
        /// </summary>
        public List<Point3D> GetVertices() => [V1, V2, V3];

        /// <summary>
        /// Calculates the intersection of a triangle with the slicing plane.
        /// </summary>
        /// <param name="triangle"> The triangle for which it has to be calculated</param>
        /// <returns>A briocheEdge representing the intersection.</returns>
        public BriocheEdge? Calculate_Intersection(double slicingPlaneZ)
        {
            // Get current plane z position
            double zPlane = slicingPlaneZ + 0.00000001; // offset to handle edge case of slides.

            // Get triangle vertices
            var vertices = GetVertices();

            // Determine which vertices are above and below the slicing plane
            List<BriocheEdge> aboveAndBelow = new List<BriocheEdge>();

            // We check each combination of vertices, but only once. (v1,V2) = (v2,v1)
            for (int i = 0; i < vertices.Count; i++)
            {
                for (int j = i + 1; j < vertices.Count; j++)
                {
                    bool v1Above = vertices[i].Z > zPlane;
                    bool v2Above = vertices[j].Z > zPlane;
                    if (v1Above != v2Above) // if both false or true, skip
                    {
                        aboveAndBelow.Add(new BriocheEdge(vertices[i], vertices[j]));
                    }
                }
            }

            Point3D intersection1 = default, intersection2 = default;
            // We need exactly two pairs
            if (aboveAndBelow.Count == 2)
            {
                for (int i = 0; i < aboveAndBelow.Count; i++)
                {
                    // Extract edge
                    var edge = aboveAndBelow[i];

                    // Extract the vertices for easy copying of calculations.
                    var zi = zPlane;
                    var z1 = edge.Start.Z;
                    var z2 = edge.End.Z;
                    var x1 = edge.Start.X;
                    var x2 = edge.End.X;
                    var y1 = edge.Start.Y;
                    var y2 = edge.End.Y;

                    // Execute calculations.
                    var intersectionX = x1 + ((zi - z1) * (x2 - x1)) / (z2 - z1);
                    var intersectionY = y1 + ((zi - z1) * (y2 - y1)) / (z2 - z1);

                    if (i == 0)
                    {
                        intersection1 = new Point3D(intersectionX, intersectionY, zi);
                    }
                    else
                    {
                        intersection2 = new Point3D(intersectionX, intersectionY, zi);
                    }
                }

                return new BriocheEdge(intersection1, intersection2);
            }
            return null;
        }

        private static void TryAddIntersection(Point3D p1, Point3D p2, double zPlane, double eps, List<Point3D> outpoints)
        {
            double zP1 = p1.Z - zPlane;
            double zP2 = p2.Z - zPlane;

            if (Math.Abs(zP1) < eps && Math.Abs(zP2) >= eps)
                return;
            if (Math.Abs(zP1) < eps && Math.Abs(zP2) >= eps)
            {
                outpoints.Add(new Point3D(p1.X, p1.Y, zPlane));
                return;
            }
            if (Math.Abs(zP2) < eps && Math.Abs(zP1) >= eps)
            {
                outpoints.Add(new Point3D(p2.X, p2.Y, zPlane));
                return;
            }

            if ((zP1 > 0 && zP2 < 0) || (zP1 < 0 && zP2 > 0))
            {
                double t = zP1 / (zP1 - zP2);
                double x = p1.X + t * (p2.X - p1.X);
                double y = p1.Y + t * (p2.Y - p2.Y);
                outpoints.Add(new Point3D(x, y, zPlane));
            }
        }

        private static List<Point3D> removeDup(List<Point3D> points, double eps)
        {
            var result = new List<Point3D>(points.Count);

            foreach (var p in points)
            {
                bool dup = false;
                for (int i = 0; i < result.Count; i++)
                {
                    if (SqDistXY(p, result[i]) <= eps * eps)
                    {
                    dup = true;
                    break;
                    } 
                }
                if (!dup) result.Add(p);
            }
            return result;
        }

        private static double SqDistXY(in Point3D a, in Point3D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Extracts all triangles from a Model3DGroup.
        /// This function is mainly written with AI.
        /// </summary>
        /// <param name="modelGroup">The 3D model group to extract triangles from.</param>
        /// <returns>A list of all triangles in the model.</returns>
        public static List<BriocheTriangle> Get_Triangles_From_Model(Model3DGroup modelGroup)
        {
            var triangles = new List<BriocheTriangle>();

            foreach (var model in modelGroup.Children)
            {
                if (model is GeometryModel3D geometryModel)
                {
                    if (geometryModel.Geometry is MeshGeometry3D mesh)
                    {
                        var positions = mesh.Positions;
                        var indices = mesh.TriangleIndices;

                        // If no indices are defined, vertices are in sequential order (every 3 vertices = 1 triangle)
                        if (indices.Count == 0)
                        {
                            for (int i = 0; i < positions.Count; i += 3)
                            {
                                if (i + 2 < positions.Count)
                                {
                                    triangles.Add(new BriocheTriangle(positions[i], positions[i + 1], positions[i + 2]));
                                }
                            }
                        }
                        else
                        {
                            // Use the index buffer to construct triangles
                            for (int i = 0; i < indices.Count; i += 3)
                            {
                                if (i + 2 < indices.Count)
                                {
                                    triangles.Add(new BriocheTriangle(
                                        positions[indices[i]],
                                        positions[indices[i + 1]],
                                        positions[indices[i + 2]]
                                    ));
                                }
                            }
                        }
                    }
                }
                else if (model is Model3DGroup childGroup)
                {
                    // Recursively process nested groups
                    triangles.AddRange(Get_Triangles_From_Model(childGroup));
                }
            }

            return triangles;
        }
    }
}
