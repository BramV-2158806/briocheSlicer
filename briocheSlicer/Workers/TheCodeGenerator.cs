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
        // Variable to keep count of position in the E dimension.
        // We use absolute extrusion but reset the extrusion position each layer.
        double currentExtrusion = 0.0;

        public TheCodeGenerator() {}

        /// <summary>
        /// Generates the gcode to print a given brioche model
        /// under specified settings, and writes it to the output path.
        /// ** Main function of the CodeGenerator **
        /// </summary>
        /// <param name="model"></param>
        /// <param name="settings"></param>
        /// <param name="outputPath"></param>
        public void Generate(BriocheModel model, GcodeSettings settings, string outputPath)
        {
            var gcode = new StringBuilder();

            // Load start gcode
            string startGCode = GcodeHelpers.LoadGcodeFromFile("start.gcode");
            gcode.AppendLine(startGCode);

            // Add poop line to calibrate extrusion
            gcode.AppendLine(Invariant($"G0 F{settings.PrintSpeed * 60:F0} X0 Y2"));
            gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X100 Y2 E10"));
            gcode.AppendLine(Invariant($"G92 E0"));

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
            string endGCode = GcodeHelpers.LoadGcodeFromFile("end.gcode");
            gcode.AppendLine(endGCode);

            // Write to file
            File.WriteAllText(outputPath, gcode.ToString());
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
            // remove the mid layer slicing so we start from the bottom but we sliced 
            // in the middle of the layer
            double extrusionHeight = settings.LayerHeight * layerIndex;
            gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{extrusionHeight}"));

            // Reset extrusion position
            gcode.AppendLine(Invariant($"G92 E0"));
            currentExtrusion = 0;

            // Print perimiter
            AddShellCode(gcode, slice, settings, model.offset_x, model.offset_y);

            // Print roofs and floors
            AddFloorAndRoofCode(gcode, slice, settings, model.offset_x, model.offset_y);

            // Print infill
            AddInfillCode(gcode, slice, settings, model.offset_x, model.offset_y);

            // Print support
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
                AddFloorCode(gcode, floorPaths, settings, offset_x, offset_y);
            }

            // Print roof paths
            PathsD? roofPaths = slice.GetRoof();
            if (roofPaths != null && roofPaths.Count > 0)
            {
                AddRoofCode(gcode, roofPaths, settings, offset_x, offset_y);
            }
        }

        /// <summary>
        /// Creates the gcode for the floor.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="floorPaths"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddFloorCode(StringBuilder gcode, PathsD floorPaths, GcodeSettings settings, double offset_x, double offset_y)
        {
            gcode.AppendLine("; Floor");
            RetractHelper retractHelper = new RetractHelper(settings.extrusion_rectrection_length);
            foreach (var path in floorPaths)
            {
                if (path == null || path.Count < 2) continue;

                retractHelper.Reset(gcode, currentExtrusion);

                // Move to start position (travel move, no extrusion)
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                // Extrude along the floor path
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(previousPoint, currentPoint, settings);

                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + offset_x:F3} Y{currentPoint.y + offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;
                }

                // If there is a tiny gap in the loop
                // we close the loop
                var startPoint = path[0];
                var lastPoint = path[path.Count - 1];
                double distance = Math.Sqrt(
                    Math.Pow(startPoint.x - lastPoint.x, 2) +
                    Math.Pow(startPoint.y - lastPoint.y, 2)
                );
                if (distance > 0.001)
                {
                    double closingExtrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(lastPoint, startPoint, settings);
                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{startPoint.x + offset_x:F3} Y{startPoint.y + offset_y:F3} E{closingExtrusion:F5}"));
                    currentExtrusion = closingExtrusion;
                }

                retractHelper.Retract(gcode, currentExtrusion);
            }
        }

        /// <summary>
        ///  Generates the roof code for a slice.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="roofPaths"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddRoofCode(StringBuilder gcode, PathsD roofPaths, GcodeSettings settings, double offset_x, double offset_y)
        {
            RetractHelper retractHelper = new RetractHelper(settings.extrusion_rectrection_length);
            gcode.AppendLine("; Roof");
            foreach (var path in roofPaths)
            {
                if (path == null || path.Count < 2) continue;

                retractHelper.Reset(gcode, currentExtrusion);

                // Move to start position (travel move, no extrusion)
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                // Extrude along the roof path
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(previousPoint, currentPoint, settings);

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
                    double closingExtrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(lastPoint, startPoint, settings);
                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{startPoint.x + offset_x:F3} Y{startPoint.y + offset_y:F3} E{closingExtrusion:F5}"));
                    currentExtrusion = closingExtrusion;
                }

                retractHelper.Retract(gcode, currentExtrusion);
            }
        }

        /// <summary>
        /// Adds the gcode for support structures
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice"></param>
        /// <param name="settings"></param>
        /// <param name="model"></param>
        /// <param name="layerIndex"></param>
        private void AddSupportCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, BriocheModel model, int layerIndex)
        {
            // If there are no supports to generate, return
            PathsD? supportPaths = slice.GetSupport();
            if (supportPaths == null || supportPaths.Count == 0) return;

            // If the next layer does not have support we can skip the layer before
            // To make it easier to remove
            BriocheSlice? nextSlice = model.GetSlice(layerIndex + 1);
            PathsD? nextSupport = nextSlice?.GetSupport();
            if (nextSupport == null || nextSupport.Count == 0)
            {
                gcode.AppendLine("; Last Support Layer Dropped! ");
                return;
            }

            RetractHelper retractHelper = new RetractHelper(settings.extrusion_rectrection_length);

            // Process each infill line (these are open paths)
            gcode.AppendLine("; support");
            foreach (var path in supportPaths)
            {
                if (path == null || path.Count < 2) continue;

                retractHelper.Reset(gcode, currentExtrusion);

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
                    double extrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(previousPoint, currentPoint, settings);

                    // Stop hop
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{slice.slice_height}"));

                    // Print line
                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + model.offset_x:F3} Y{currentPoint.y + model.offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;

                    // Start hop
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{slice.slice_height + 0.2}"));
                }

                retractHelper.Retract(gcode, currentExtrusion);
            }

        }

        /// <summary>
        /// Adds the shell code for a single slice.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddShellCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double offset_x, double offset_y)
        {
            PathsD? shellPaths = slice.GetOuterLayer();
            if (shellPaths == null || shellPaths.Count == 0) return;

            RetractHelper retractHelper = new RetractHelper(settings.extrusion_rectrection_length);

            // Trace each shell path
            gcode.AppendLine("; schells");
            foreach (var path in shellPaths)
            {
                if (path == null || path.Count < 2) continue;

                retractHelper.Reset(gcode, currentExtrusion);

                // Move to start position
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                // Extrude along the shell perimeter
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(previousPoint, currentPoint, settings);

                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + offset_x:F3} Y{currentPoint.y + offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;
                }

                // Close the loop by returning to the start point
                var startPoint = path[0];
                var lastPoint = path[path.Count - 1];
                double lastExtrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(lastPoint, startPoint, settings);
                currentExtrusion = lastExtrusion;
                gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{startPoint.x + offset_x:F3} Y{startPoint.y + offset_y:F3} E{lastExtrusion:F5}"));

                retractHelper.Retract(gcode, currentExtrusion);
            }
        }

        /// <summary>
        /// Generate code to print the infill.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddInfillCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double offset_x, double offset_y)
        {
            PathsD? infillPaths = slice.GetInfill();
            if (infillPaths == null || infillPaths.Count == 0) return;

            RetractHelper retractHelper = new RetractHelper(settings.extrusion_rectrection_length);

            // Process each infill line (these are open paths)
            gcode.AppendLine("; infill");
            foreach (var path in infillPaths)
            {
                if (path == null || path.Count < 2) continue;

                retractHelper.Reset(gcode, currentExtrusion);

                // Move to start position of infill line (travel move, no extrusion)
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + offset_x:F3} Y{firstPoint.y + offset_y:F3}"));

                // Extrude along the infill line
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(previousPoint, currentPoint, settings);

                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X{currentPoint.x + offset_x:F3} Y{currentPoint.y + offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;
                }

                retractHelper.Retract(gcode, currentExtrusion);
            }
        }
    }
}
