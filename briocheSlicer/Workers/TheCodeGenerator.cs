using briocheSlicer.Gcode;
using briocheSlicer.Slicing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
            gcode.AppendLine($"G1 F{settings.TravelSpeed * 60:F0} Z{settings.LayerHeight * (layerIndex + 0.5)}");

            // Process each polygon in the slice
            // TODO create toolpaths and print using PATHSD from clipper instead of BriocheEdges
            var polygons = slice.getPolygons();
            foreach (var polygon in polygons)
            {
                AddPolygonCode(gcode, polygon, settings);
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
