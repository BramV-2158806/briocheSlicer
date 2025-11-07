using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Slicing
{
    internal class Snapper
    {
        private readonly double _epsScale;
        private readonly Dictionary<VertexKey, (double x, double y)> _keyValuePairs = new();

        public Snapper(double eps) { _epsScale = 1.0 / eps; }
        public (double X, double Y) Norm_Vert(double x, double y)
        {
            var key = Key(x, y);
            if (_keyValuePairs.TryGetValue(key, out var r)) return r;
            _keyValuePairs[key] = (x, y);
            return (x, y);
        }

        public VertexKey Key(double x, double y)
        {
            return new VertexKey((long)Math.Round(x * _epsScale), (long)Math.Round(y * _epsScale));
        }
    }
}
