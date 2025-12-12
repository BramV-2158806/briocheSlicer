using briocheSlicer.Gcode;
using briocheSlicer.Slicing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Clipper2Lib;
using static System.FormattableString; // comma to point string representation

namespace briocheSlicer.Workers
{
    internal class TheCodeGenerator
    {
        double currentExtrusion = 0.0;
        private bool lastSupport = false;

        public TheCodeGenerator() {}

        /// <summary>
        /// Loads gcode from the specified file into a string.
        /// It searches for the file in a Resource folder.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        private string LoadGcodeFromFile(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = $"briocheSlicer.Resources.{filename}";

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Adds the gcode for a single layer to the gcode StringBuilder.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice">
        /// The current slice to be printed.
        /// </param>
        /// <param name="layerIndex">
        /// The index of the layer being processed (starting from 1).
        /// Since we are printing the first layer at Z = layerHeight.
        /// </param>
        /// <param name="settings"></param>
        private void AddLayerCode(StringBuilder gcode, BriocheSlice slice, int layerIndex, GcodeSettings settings, BriocheModel model)
        {
            // Add some debug messaging in the gcode
            gcode.AppendLine($"; Layer {layerIndex}");

            // Move to layer height - keep in mind the mid layer slicing
            // remove thi mis layer slicing so we start from the bottom but we sliced 
            // in the middle of the layer
            double extrusionHeight = settings.LayerHeight * layerIndex; // saw that cure starts at 2 + layerHeight
            gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{extrusionHeight}"));
            gcode.AppendLine(Invariant($"G92 E0"));
            currentExtrusion = 0;

            // Print perimiter
            AddShellCode(gcode, slice, settings, model.offset_x, model.offset_y);

            // Print roofs and floors
            AddFloorAndRoofCode(gcode, slice, settings, model.offset_x, model.offset_y);

            // Print infill
            AddInfillCode(gcode, slice, settings, model.offset_x, model.offset_y);

            AddSupportCode(gcode, slice, settings, model, layerIndex);
        }

        /// <summary>
        /// Adds the G-code for floor and roof paths to the G-code StringBuilder.
        /// Floor and roof paths are closed paths that need to be traced with extrusion.
        /// </summary>
        /// <param name="gcode">The StringBuilder to append G-code to</param>
        /// <param name="slice">The current slice containing floor and roof paths</param>
        /// <param name="settings">G-code generation settings</param>
        private void AddFloorAndRoofCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double offset_x, double offset_y)
        {
            // Print floor paths
            PathsD? floorPaths = slice.GetFloor();
            if (floorPaths != null && floorPaths.Count > 0)
            {
                gcode.AppendLine("; Floor");
                foreach (var path in floorPaths)
                {
                    if (path == null || path.Count < 2) continue;

                    // Move to start position (travel move, no extrusion)
                    var firstPoint = path[0];
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                    // Extrude along the floor path
                    for (int i = 1; i < path.Count; i++)
                    {
                        var currentPoint = path[i];
                        var previousPoint = path[i - 1];
                        double extrusion = currentExtrusion + CalculateExtrusion(previousPoint, currentPoint, settings);

                        gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + offset_x:F3} Y{currentPoint.y + offset_y:F3} E{extrusion:F5}"));
                        currentExtrusion = extrusion;
                    }

                    // Close the loop by returning to the start point
                    var startPoint = path[0];
                    var lastPoint = path[path.Count - 1];
                    double distance = Math.Sqrt(
                        Math.Pow(startPoint.x - lastPoint.x, 2) +
                        Math.Pow(startPoint.y - lastPoint.y, 2)
                    );

                    // Only close if there's a gap (not already closed)
                    if (distance > 0.001)
                    {
                        double closingExtrusion = currentExtrusion + CalculateExtrusion(lastPoint, startPoint, settings);
                        gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{startPoint.x + offset_x:F3} Y{startPoint.y + offset_y:F3} E{closingExtrusion:F5}"));
                        currentExtrusion = closingExtrusion;
                    }
                }
            }

            // Print roof paths
            PathsD? roofPaths = slice.GetRoof();
            if (roofPaths != null && roofPaths.Count > 0)
            {
                gcode.AppendLine("; Roof");
                foreach (var path in roofPaths)
                {
                    if (path == null || path.Count < 2) continue;

                    // Move to start position (travel move, no extrusion)
                    var firstPoint = path[0];
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                    // Extrude along the roof path
                    for (int i = 1; i < path.Count; i++)
                    {
                        var currentPoint = path[i];
                        var previousPoint = path[i - 1];
                        double extrusion = currentExtrusion + CalculateExtrusion(previousPoint, currentPoint, settings);

                        gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + offset_x:F3} Y{currentPoint.y + offset_y:F3} E{extrusion:F5}"));
                        currentExtrusion = extrusion;
                    }

                    // Close the loop by returning to the start point
                    var startPoint = path[0];
                    var lastPoint = path[path.Count - 1];
                    double distance = Math.Sqrt(
                        Math.Pow(startPoint.x - lastPoint.x, 2) +
                        Math.Pow(startPoint.y - lastPoint.y, 2)
                    );

                    // Only close if there's a gap (not already closed)
                    if (distance > 0.001)
                    {
                        double closingExtrusion = currentExtrusion + CalculateExtrusion(lastPoint, startPoint, settings);
                        gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{startPoint.x + offset_x:F3} Y{startPoint.y + offset_y:F3} E{closingExtrusion:F5}"));
                        currentExtrusion = closingExtrusion;
                    }
                }
            }
        }

        private void AddSupportCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, BriocheModel model, int layerIndex)
        {
            PathsD? supportPaths = slice.GetSupport();
            if (supportPaths == null || supportPaths.Count == 0) return;

            BriocheSlice? nextSlice = model.GetSlice(layerIndex + 1);
            PathsD? nextSupport = nextSlice?.GetSupport();

            if (nextSupport == null || nextSupport.Count == 0)
            {
                gcode.AppendLine("; Last Support Layer Dropped! ");
                return;
            }

            // Process each infill line (these are open paths)
            foreach (var path in supportPaths)
            {
                if (path == null || path.Count < 2) continue;

                //bool hasOverlapAbove = false;
                //if (nextSupport != null && nextSupport.Count > 0)
                //{
                //    hasOverlapAbove = PathIntersectsAny(path, nextSupport);
                //}

                //if (!hasOverlapAbove)
                //{
                //    continue; // Skip this support path as it has no overlap above
                //}

                // Move to start position of infill line (travel move, no extrusion)
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + model.offset_x:F3} Y{firstPoint.y + model.offset_y:F3}"));

                // Start hop
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{slice.slice_height + 0.2}"));


                // Extrude along the infill line
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + CalculateExtrusion(previousPoint, currentPoint, settings);

                    // Stop hop
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{slice.slice_height}"));

                    // Print line
                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + model.offset_x:F3} Y{currentPoint.y + model.offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;

                    // Start hop
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{slice.slice_height + 0.2}"));
                }

            }

        }

        /// <summary>
        /// True if any vertex of pathA is inside any polygon in pathsB, or vice versa.
        /// This is a simple but robust test to detect overlap/continuation between slices.
        /// </summary>
        private static bool PathIntersectsAny(PathD pathA, PathsD pathsB)
        {
            if (pathA == null || pathA.Count == 0 || pathsB == null || pathsB.Count == 0) return false;

            // Check any vertex of A inside B
            foreach (var pt in pathA)
            {
                foreach (var poly in pathsB)
                {
                    if (poly == null || poly.Count < 3) continue;
                    if (PointInPolygon(pt, poly)) return true;
                }
            }

            // Check any vertex of B inside A
            foreach (var poly in pathsB)
            {
                if (poly == null || poly.Count < 3) continue;
                foreach (var pt in poly)
                {
                    if (PointInPolygon(pt, pathA)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ray-casting point-in-polygon test (non-zero winding via crossings).
        /// Returns true if point is inside polygon (polygon must be closed or open list of vertices).
        /// </summary>
        private static bool PointInPolygon(PointD p, PathD poly)
        {
            bool inside = false;
            int n = poly.Count;
            if (n < 3) return false;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = poly[i];
                var pj = poly[j];

                // Check if edge (pj -> pi) crosses the horizontal ray to the right of point p
                bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                                 (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) == 0 ? double.Epsilon : (pj.y - pi.y)) + pi.x);
                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        private void AddShellCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double offset_x, double offset_y)
        {
            PathsD? shellPaths = slice.GetOuterLayer();
            if (shellPaths == null || shellPaths.Count == 0) return;

            // Trace each shell path
            foreach (var path in shellPaths)
            {
                if (path == null || path.Count < 2) continue;

                // Move to start position
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                // Extrude along the shell perimeter
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + CalculateExtrusion(previousPoint, currentPoint, settings);

                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + offset_x:F3} Y{currentPoint.y + offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;
                }

                // Close the loop by returning to the start point
                var startPoint = path[0];
                var lastPoint = path[path.Count - 1];
                double closingEdgeLength = Math.Sqrt(
                    Math.Pow(startPoint.x - lastPoint.x, 2) + 
                    Math.Pow(startPoint.y - lastPoint.y, 2)
                );
                double lastExtrusion = currentExtrusion + CalculateExtrusion(lastPoint, startPoint, settings);
                currentExtrusion = lastExtrusion;
                gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{startPoint.x + offset_x:F3} Y{startPoint.y + offset_y:F3} E{lastExtrusion:F5}"));
            }
        }

        private void AddInfillCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double offset_x, double offset_y)
        {
            PathsD? infillPaths = slice.GetInfill();
            if (infillPaths == null || infillPaths.Count == 0) return;

            // Process each infill line (these are open paths)
            foreach (var path in infillPaths)
            {
                if (path == null || path.Count < 2) continue;

                // Move to start position of infill line (travel move, no extrusion)
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                // Extrude along the infill line
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + CalculateExtrusion(previousPoint, currentPoint, settings);

                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + offset_x:F3} Y{currentPoint.y + offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;
                }
            }
        }


        /// <summary>
        /// Implements the formula from the slides to calculate the extrusion for a given edge.
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        private double CalculateExtrusion(PointD start, PointD end, GcodeSettings settings)
        {
            double edge_length = Math.Sqrt(
                Math.Pow(end.x - start.x, 2) +
                Math.Pow(end.y - start.y, 2)
            );
            double result = (settings.LayerHeight * settings.NozzleDiameter * edge_length) / settings.FilamentSurfaceArea;
            return result * settings.ExtrusionMultiplier;
         }

        /// <summary>
        /// Generates the gcode to print a given brioche model
        /// under specified settings, and writes it to the output path.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="settings"></param>
        /// <param name="outputPath"></param>
        public void Generate(BriocheModel model, GcodeSettings settings, string outputPath)
        {
            var gcode = new StringBuilder();

            // Load start gcode
            string startGCode = LoadGcodeFromFile("start.gcode");
            gcode.AppendLine(startGCode);

            // Add a friendly message
            gcode.AppendLine($"; Sliced by BriocheSlicer on {DateTime.Now}. Would you like a slice of brioche?");
            gcode.AppendLine($"; Layer height: {settings.LayerHeight}mm");
            gcode.AppendLine();

            // Process the model
            for (int i = 0; i < model.amount_Layers; i++)
            {
                var slice = model.GetSlice(i);
                var layerIndex = i + 1; // We start counting layers from 1 in the gcode
                AddLayerCode(gcode, slice!, layerIndex, settings, model);
            }

            // Load end gcode
            string endGCode = LoadGcodeFromFile("end.gcode");
            gcode.AppendLine(endGCode);

            // Write to file
            File.WriteAllText(outputPath, gcode.ToString());
        }
    }

}
