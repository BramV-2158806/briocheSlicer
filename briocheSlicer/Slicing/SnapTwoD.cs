using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing
{
    sealed class Snap2D
    {
        private readonly double _scale;
        public Snap2D(double eps)
        {
            _scale = 1.0 / eps;
        }
        public VertexKey Key(Point3D point)
        {
            return new VertexKey((long)Math.Round(point.X * _scale), (long)Math.Round(point.Y * _scale));
        }
    }
}
