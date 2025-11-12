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

namespace briocheSlicer.Workers
{
    internal class TheCodeGenerator
    {
        public TheCodeGenerator()
        {
        }

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
        /// <param name="slice"></param>
        /// <param name="layerIndex"></param>
        /// <param name="settings"></param>
        private void AddLayerCode(StringBuilder gcode, BriocheSlice slice, int layerIndex, GcodeSettings settings)
        {
            // Add some debug messaging in the gcode
            gcode.AppendLine($"; Layer {layerIndex}");

            // Move to layer height - keep in mind the mid layer slicing
            // remove thi mis layer slicing so we start from the bottom but we sliced 
            // in the middle of the layer
            gcode.AppendLine($"G1 F{settings.TravelSpeed * 60:F0} Z{settings.LayerHeight * (layerIndex)}");

            // Print perimiter
            AddShellCode(gcode, slice, settings);

            // Print infill
            AddInfillCode(gcode, slice, settings);
        }

        private void AddShellCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings)
        {
            PathsD? shellPaths = slice.GetSlice();
            if (shellPaths == null || shellPaths.Count == 0) return;

            // Trace each shell path
            foreach (var path in shellPaths)
            {
                if (path == null || path.Count < 2) continue;

                // Move to start position
                var firstPoint = path[0];
                gcode.AppendLine($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x:F3} Y{firstPoint.y:F3}");

                // Extrude along the shell perimeter
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = CalculateExtrusion(previousPoint, currentPoint, settings);

                    gcode.AppendLine($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x:F3} Y{currentPoint.y:F3} E{extrusion:F5}");
                }

                // Close the loop by returning to the start point
                var startPoint = path[0];
                var lastPoint = path[path.Count - 1];
                double closingEdgeLength = Math.Sqrt(
                    Math.Pow(startPoint.x - lastPoint.x, 2) + 
                    Math.Pow(startPoint.y - lastPoint.y, 2)
                );
                
                if (closingEdgeLength > 1e-6) // Only close if there's a gap
                {
                    double closingExtrusion = (settings.LayerHeight * settings.NozzleDiameter * closingEdgeLength) / 
                                          settings.FilamentDiameter * settings.ExtrusionMultiplier;
                    gcode.AppendLine($"G1 F{settings.PrintSpeed * 60:F0} X{startPoint.x:F3} Y{startPoint.y:F3} E{closingExtrusion:F5}");
                }
            }
        }

        private void AddInfillCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings)
        {
            PathsD? infillPaths = slice.GetInfill();
            if (infillPaths == null || infillPaths.Count == 0) return;

            // Process each infill line (these are open paths)
            foreach (var path in infillPaths)
            {
                if (path == null || path.Count < 2) continue;

                // Move to start position of infill line (travel move, no extrusion)
                var firstPoint = path[0];
                gcode.AppendLine($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x:F3} Y{firstPoint.y:F3}");

                // Extrude along the infill line
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = CalculateExtrusion(previousPoint, currentPoint, settings);

                    gcode.AppendLine($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x:F3} Y{currentPoint.y:F3} E{extrusion:F5}");
                }
            }
        }

        private void AddPolygonCode(StringBuilder gcode, List<BriocheEdge> edges, GcodeSettings settings)
        {
            if (edges.Count == 0) return;

            // Move to start position (travel move)
            var firstPoint = edges[0].Start;
            gcode.AppendLine($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.X:F3} Y{firstPoint.Y:F3}");

            // Print the polygon
            foreach (var edge in edges)
            {
                var endPoint = edge.End;
                double extrusion = CalculateExtrusion(edge, settings);
                gcode.AppendLine($"G1 F{settings.PrintSpeed * 60:F0} X{endPoint.X:F3} Y{endPoint.Y:F3} E{extrusion:F5}");
            }
        }

        /// <summary>
        /// Implements the formula from the slides to calculate the extrusion for a given edge.
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        private double CalculateExtrusion(BriocheEdge edge, GcodeSettings settings)
        {
            double edge_length = Math.Sqrt(
                Math.Pow(edge.End.X - edge.Start.X, 2) + 
                Math.Pow(edge.End.Y - edge.Start.Y, 2)
            );
            double result = (settings.LayerHeight * settings.NozzleDiameter * edge_length)/settings.FilamentDiameter;
            return (double) result * settings.ExtrusionMultiplier;
        }
        private double CalculateExtrusion(PointD start, PointD end, GcodeSettings settings)
        {
            double edge_length = Math.Sqrt(
                Math.Pow(end.x - start.x, 2) +
                Math.Pow(end.y - start.y, 2)
            );
            double result = (settings.LayerHeight * settings.NozzleDiameter * edge_length) / settings.FilamentDiameter;
            return result * settings.ExtrusionMultiplier;
        }


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
                AddLayerCode(gcode, slice, i, settings);
            }

            // Load end gcode
            string endGCode = LoadGcodeFromFile("end.gcode");
            gcode.AppendLine(endGCode);

            // Write to file
            File.WriteAllText(outputPath, gcode.ToString());
        }
    }

}
