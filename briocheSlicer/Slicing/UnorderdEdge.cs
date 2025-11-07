using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Slicing
{
    readonly struct UnorderdEdge : IEquatable<UnorderdEdge>
    {
        public readonly VertexKey k1, k2;
        public UnorderdEdge(VertexKey key1, VertexKey key2)
        {
            if (key1.Xq < key2.Xq || (key1.Xq == key2.Xq && key1.Yq <= key2.Yq))
            {
                k1 = key1;
                k2 = key2;
            }
            else
            {
                k1 = key2;
                k2 = key1;
            }
        }

        public bool Equals(UnorderdEdge other)
        {
            return k1.Equals(other.k1) && k2.Equals(other.k2);
        }
        public override bool Equals(object? obj)
        {
            return obj is UnorderdEdge e && Equals(e);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
