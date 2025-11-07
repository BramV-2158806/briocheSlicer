using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Slicing
{
    readonly struct VertexKey : IEquatable<VertexKey>
    {
        public readonly long Xq, Yq;
        public VertexKey(long xq, long yq) { Xq = xq; Yq = yq; }
        public bool Equals(VertexKey other)
        {
            if (Xq == other.Xq && Yq == other.Yq) return true;
            return false;
        }
        public override bool Equals(object? obj)
        {
            if (obj is VertexKey k && Equals(k)) return true;
            return false;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Xq, Yq);
        }
    }
}
