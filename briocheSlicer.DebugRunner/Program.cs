// See https://aka.ms/new-console-template for more information

using briocheSlicer.Slicing;

namespace briocheSlicer.DebugRunner
{
    internal static class Program
    {
        static void Main()
        {
            try
            {
                Console.WriteLine("=== briocheSlicer Debug Runner ===");

                Test_SimpleSquare();
                // Add more quick tests here:
                // Test_TwoDisjointSquares();
                // Test_DanglingEdgeGetsDiscarded();
                // Test_NoiseTolerance();

                Console.WriteLine("\nAll tests finished. Press any key to exit...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[ERROR] " + ex);
            }
            Console.ReadKey();
        }

        static void Test_SimpleSquare()
        {
            Console.WriteLine("\n--- Test_SimpleSquare ---");

            double z = 0.20;
            // Unordered + one reversed edge, to mimic real intersections
            var edges = new List<BriocheEdge>
            {
                new BriocheEdge(new Point3D(0,0,z), new Point3D(1,0,z)),
                new BriocheEdge(new Point3D(1,1,z), new Point3D(0,1,z)), // reversed
                new BriocheEdge(new Point3D(1,0,z), new Point3D(1,1,z)),
                new BriocheEdge(new Point3D(0,1,z), new Point3D(0,0,z)),
            };

            var slice = new Slice(edges, z); // your constructor builds polygons

            PrintSlice(slice);

            // Quick sanity checks:
            if (slice.Polygons.Count != 1) throw new Exception("Expected exactly 1 polygon.");
            var loop = slice.Polygons[0];
            if (loop.Count < 4) throw new Exception("Expected a closed loop with >= 4 edges.");
            var first = loop[0].Start;
            var lastEnd = loop[^1].End;
            if (Math.Abs(first.X - lastEnd.X) > 1e-9 || Math.Abs(first.Y - lastEnd.Y) > 1e-9)
                throw new Exception("Loop is not closed.");
        }

        static void PrintSlice(Slice slice)
        {
            Console.WriteLine($"Polygons: {slice.Polygons.Count}");
            for (int p = 0; p < slice.Polygons.Count; p++)
            {
                var poly = slice.Polygons[p];
                Console.WriteLine($"  Polygon {p}: {poly.Count} edges");
                for (int i = 0; i < poly.Count; i++)
                {
                    var e = poly[i];
                    Console.WriteLine(
                        $"    {i:D2}: ({e.Start.X:F6}, {e.Start.Y:F6}, {e.Start.Z:F3}) -> " +
                        $"({e.End.X:F6}, {e.End.Y:F6}, {e.End.Z:F3})");
                }
            }
        }
    }
}
