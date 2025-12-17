using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing.TreeSupport
{
    internal class SeedCluster
    {
        private SeedPoint center;
        private double size;
        private int clusterId;
        private int numPoints;

        public SeedCluster(SeedPoint center, double size, int clusterId, int numPoints)
        {
            this.center = center;
            this.size = size;
            this.clusterId = clusterId;
            this.numPoints = numPoints;
        }

        public double GetSize() { return size; }
        public Point3D GetCentroidPoint()
        {
            return center.point;
        }
    }
}
