using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Diagnostics;

namespace briocheSlicer.Slicing
{
    internal class BriocheEdge
    {
        public Point3D Start { get; set; }
        public Point3D End { get; set; }
        public BriocheEdge(Point3D start, Point3D end)
        {
            Start = start;
            End = end;
        }

        public Tuple<Point3D, Point3D> GetPoints() => Tuple.Create(Start, End);

        public String To_String()
        {
            return $"Edge(Start: ({Start.X}, {Start.Y}, {Start.Z}), End: ({End.X}, {End.Y}, {End.Z}))";
        }

        public void Print()
        {
            Debug.WriteLine(To_String());
        }
    }
}
