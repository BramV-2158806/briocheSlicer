using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing.TreeSupport
{
    internal class SeedPoint
    {
        public Point3D point { get; }
        public double x { get; }
        public double y { get; }
        public double z { get; }
        public double faceSize { get; }

        public SeedPoint(double x, double y, double z, double size)
        {
            this.point = new Point3D(x, y, z);
            this.x = x;
            this.y = y;
            this.z = z;
            this.faceSize = size;
        }

        public SeedPoint(Point3D point, double size)
        {
            this.point = point;
            this.x = point.X;
            this.y = point.Y;
            this.z = point.Z;
            this.faceSize = size;
        }

        public SeedPoint(Point3D v0, Point3D v1, Point3D v2, double size)
        {
            this.point = new Point3D(
            (v0.X + v1.X + v2.X) / 3.0,
            (v0.Y + v1.Y + v2.Y) / 3.0,
            (v0.Z + v1.Z + v2.Z) / 3.0);
            this.x = point.X;
            this.y = point.Y;
            this.z = point.Z;

            this.faceSize = size;
        }

        public static double CalculateTriangleSize(Vector3D v1, Vector3D v2, Vector3D v3)
        {
            Vector3D edge1 = v2 - v1;
            Vector3D edge2 = v3 - v1;
            Vector3D crossProduct = Vector3D.CrossProduct(edge1, edge2);
            return crossProduct.Length / 2.0;
        }

        public static double CalculateTriangleSize(Point3D p1, Point3D p2, Point3D p3)
        {
            Vector3D edge1 = p2 - p1;
            Vector3D edge2 = p3 - p1;
            Vector3D crossProduct = Vector3D.CrossProduct(edge1, edge2);
            return crossProduct.Length / 2.0;
        }

}
}
