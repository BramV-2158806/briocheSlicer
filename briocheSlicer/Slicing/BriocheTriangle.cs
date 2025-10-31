using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <param name="triangle"> The triangle for wich it has to be calculated</param>
        /// <returns>A briocheEdge representing the intersection.</returns>
        public BriocheEdge? Calculate_intersection(double slicingPlaneZ)
        {
            // Get current plane z position
            double zPlane = slicingPlaneZ + 0.00000001; // ofsett to handle edge case of slides.

            // Get triangle vertices
            var vertices = GetVertices();

            // Determine which vertices are above and below the slicing plane
            List<BriocheEdge> aboveAndBelow = new List<BriocheEdge>();
            foreach (var vertex1 in vertices)
            {
                foreach (var vertex2 in vertices)
                {
                    if (vertex1 != vertex2)
                    {
                        // Make sure they are not both above or below
                        bool v1Above = vertex1.Z > zPlane;
                        bool v2Above = vertex2.Z > zPlane;
                        if (v1Above != v2Above) // if both false or true, skip
                        {
                            aboveAndBelow.Add(new BriocheEdge(vertex1, vertex2));
                        }
                    }
                }
            }

            Point3D intersection1, intersection2;
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
