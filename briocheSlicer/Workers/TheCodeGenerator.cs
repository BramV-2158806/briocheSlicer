using briocheSlicer.Gcode;
using briocheSlicer.Slicing;
using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using static System.FormattableString; // comma to point string representation

namespace briocheSlicer.Workers
{
    internal class TheCodeGenerator
    {
        // Variable to keep count of position in the E dimension.
        // We use absolute extrusion but reset the extrusion position each layer.
        double currentExtrusion = 0.0;
        BriocheModel? model;
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
            this.model = model;


            // Load start gcode
            string startGCode = GcodeHelpers.LoadGcodeFromFile("start.gcode");
            gcode.AppendLine(startGCode);

            // skirt to calibrate extrusion and clean nozzle
            AddPrepareCode(gcode, model, settings);

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
        /// This is the main print function.
        /// Follows the paths from the pathsD and adds the trace gcode to the stringbuilder.
        /// There are optional booleans to enable hops, slowDown and closing of loop.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="paths"></param>
        /// <param name="settings"></param>
        /// <param name="sliceHeight"></param>
        /// <param name="closeLoop"></param>
        /// <param name="hop"></param>
        /// <param name="slowDown"></param>
        private void PrintPatshD(StringBuilder gcode, PathsD paths, GcodeSettings settings, double sliceHeight, bool closeLoop = false, bool hop = false, bool slowDown = false)
        {
            RetractHelper retractHelper = new RetractHelper(settings.ExtrusionRetractLength);

            double printMultiplier = 60;
            if (slowDown) printMultiplier = 30;

            foreach (var path in paths)
            {
                if (path == null || path.Count < 2) continue;

                retractHelper.Reset(gcode, currentExtrusion);

                // Move to start position
                var firstPoint = path[0];
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstPoint.x + model!.offset_x:F3} Y{firstPoint.y + model.offset_y:F3}"));

                // Start hop
                if (hop)
                {
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{sliceHeight + 0.2}"));
                }


                // Extrude along the infill line
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(previousPoint, currentPoint, settings);

                    // Stop hop
                    if (hop)
                    {
                        gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{sliceHeight}"));
                    }

                    // Print line
                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * printMultiplier:F0} X{currentPoint.x + model.offset_x:F3} Y{currentPoint.y + model.offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;

                    // Start hop
                    if (hop)
                    {
                        gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{sliceHeight + 0.2}"));
                    }
                }

                // Stop hop
                if (hop)
                {
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{sliceHeight}"));
                }

                // Close the loop by returning to the start point
                if (closeLoop)
                {
                    var startPoint = path[0];
                    var lastPoint = path[path.Count - 1];
                    double lastExtrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(lastPoint, startPoint, settings);
                    currentExtrusion = lastExtrusion;
                    gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * printMultiplier:F0} X{startPoint.x + model.offset_x:F3} Y{startPoint.y + model.offset_y:F3} E{lastExtrusion:F5}"));
                }

                retractHelper.Retract(gcode, currentExtrusion);
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
            // remove the mid layer slicing so we start from the bottom but we sliced 
            // in the middle of the layer
            double extrusionHeight = settings.LayerHeight * layerIndex;
            gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{extrusionHeight}"));

            // Reset extrusion position
            gcode.AppendLine(Invariant($"G92 E0"));
            currentExtrusion = 0;

            // Print perimiter
            AddShellCode(gcode, slice, settings);

            // Print roofs and floors
            AddFloorAndRoofCode(gcode, slice, settings);

            // Print infill
            AddInfillCode(gcode, slice, settings);

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
        /*private void AddLayerCode(StringBuilder gcode, BriocheSlice slice, int layerIndex, GcodeSettings settings, BriocheModel model)
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
        }*/

        /// <summary>
        /// Adds the G-code for floor and roof paths to the G-code StringBuilder.
        /// Floor and roof paths are closed paths that need to be traced with extrusion.
        /// </summary>
        /// <param name="gcode">The StringBuilder to append G-code to</param>
        /// <param name="slice">The current slice containing floor and roof paths</param>
        /// <param name="settings">G-code generation settings</param>
        private void AddFloorAndRoofCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings)
        {
            // Print floor paths
            PathsD? floorPaths = slice.GetFloor();
            if (floorPaths != null && floorPaths.Count > 0)
            {
                PrintPatshD(gcode, floorPaths, settings, slice.slice_height, closeLoop: true, slowDown: true);
            }

            // Print roof paths
            PathsD? roofPaths = slice.GetRoof();
            if (roofPaths != null && roofPaths.Count > 0)
            {
                PrintPatshD(gcode, roofPaths, settings, slice.slice_height, closeLoop: true, hop: true, slowDown: true);
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

            // Process each infill line (these are open paths)
            gcode.AppendLine("; support");
            PrintPatshD(gcode, supportPaths, settings, slice.slice_height, hop: true);

        }

        /// <summary>
        /// Adds the shell code for a single slice.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddShellCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings)
        {
            PathsD? shellPaths = slice.GetOuterLayer();
            if (shellPaths == null || shellPaths.Count == 0) return;

            RetractHelper retractHelper = new RetractHelper(settings.ExtrusionRetractLength);

            // Trace each shell path
            gcode.AppendLine("; schells");
            PrintPatshD(gcode, shellPaths!, settings, slice.slice_height, closeLoop: true);
        }

        /// <summary>
        /// Generate code to print the infill.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddInfillCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings)
        {
            PathsD? infillPaths = slice.GetInfill();
            if (infillPaths == null || infillPaths.Count == 0) return;

            // Process each infill line (these are open paths)
            gcode.AppendLine("; infill");
            PrintPatshD(gcode, infillPaths!, settings, slice.slice_height);
        }

        private void AddPrepareCode(StringBuilder gcode, BriocheModel model, GcodeSettings settings)
        {
            if (settings.TreeSupportEnabled)
            {
                // Add poop line to calibrate extrusion
                gcode.AppendLine(Invariant($"G0 F{settings.PrintSpeed * 60:F0} X0 Y2"));
                gcode.AppendLine(Invariant($"G1 F{settings.PrintSpeed * 60:F0} X100 Y2 E10"));
                gcode.AppendLine(Invariant($"G92 E0"));
            }

            // Get the lowest outer shell
            var bottomLayer = model.GetSlice(0);
            PathsD? outerShell = bottomLayer.GetOuterShell();
            if (outerShell == null) return;

            // Inflate it by a factor of the nozzle diameter
            double skirtOffset = 10 * settings.NozzleDiameter;
            PathsD skirtPath = Clipper.InflatePaths(outerShell!, skirtOffset, JoinType.Round, EndType.Polygon);

            // Print this path once
            PrintPatshD(gcode, skirtPath, settings, sliceHeight: settings.LayerHeight);
        }
    }
}
