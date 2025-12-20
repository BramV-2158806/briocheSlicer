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

        // timing
        private readonly TimeEstimator timeEstimator = new TimeEstimator();
        private TimeSpan LastEstimatedTime => timeEstimator.GetTimeSpan();
        public string formattedEstimatedTime => $"{timeEstimator.FormatDuration(LastEstimatedTime)}";

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
            // Reset timing information
            timeEstimator.Reset();

            var gcode = new StringBuilder();
            this.model = model;


            // Load start gcode
            string startGCode = GcodeHelpers.LoadGcodeFromFile("start.gcode");
            gcode.AppendLine(startGCode);

            // skirt to calibrate extrusion and clean nozzle
            AddPrepareCode(gcode, model, settings);

            // Process the model
            for (int i = 0; i < model.amount_Layers; i++)
            {
                var slice = model.GetSlice(i);
                var layerIndex = i+1; // We start counting layers from 1 in the gcode
                AddLayerCode(gcode, slice!, layerIndex, settings, model);
            }

            // Load end gcode
            string endGCode = GcodeHelpers.LoadGcodeFromFile("end.gcode");
            gcode.AppendLine(endGCode);

            // Add the rough estimated time of startcode onto the total estimated time of printing the model
            timeEstimator!.AddStartCodeEstimate();

            TimeSpan estimate = timeEstimator.GetTimeSpan();
            gcode.AppendLine($"; Estimated print time: {estimate:hh\\:mm\\:ss}");

            int total_Minutes = (int)Math.Ceiling(estimate.TotalMinutes);

            gcode.Replace(";_ESTIMATED_PRINT_TIME_", $"M73 P0 R{total_Minutes}");

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
        /// <param name="extrusionHeight"></param>
        /// <param name="closeLoop"></param>
        /// <param name="hopEnabled"></param>
        /// <param name="slowDown"></param>
        private void PrintPatshD(StringBuilder gcode, PathsD paths, GcodeSettings settings, double printSpeed, double extrusionHeight, bool closeLoop = false, bool hopEnabled = false, bool slowDown = false)
        {
            RetractHelper retractHelper = new RetractHelper(settings.ExtrusionRetractLength);

            double printMultiplier = 60;
            if (slowDown) printMultiplier = 30;

            double? prevX = null;
            double? prevY = null;

            foreach (var path in paths)
            {
                if (path == null || path.Count < 2) continue;

                retractHelper.Reset(gcode, currentExtrusion);

                // Calculate the position of the first point
                var firstPoint = path[0];
                double firstX = firstPoint.x + model!.offset_x;
                double firstY = firstPoint.y + model.offset_y;

                timeEstimator.AddTravelXY(firstX, firstY, settings.TravelSpeed);

                bool isHopNeeded = IsHopNeeded(firstX, firstY, prevX, prevY);

                // Start hop
                if (hopEnabled && isHopNeeded)
                {
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{extrusionHeight + 1.0}"));
                }

                // Move to start position
                gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} X{firstX:F3} Y{firstY:F3}"));

                // Stop hop
                if (hopEnabled && isHopNeeded)
                {
                    gcode.AppendLine(Invariant($"G1 F{settings.TravelSpeed * 60:F0} Z{extrusionHeight}"));
                    timeEstimator.AddZMove(extrusionHeight + 1, settings.TravelSpeed);
                }


                // Extrude along the lines
                for (int i = 1; i < path.Count; i++)
                {
                    var currentPoint = path[i];
                    var previousPoint = path[i - 1];
                    double extrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(previousPoint, currentPoint, settings);

                    // Timing estimation
                    firstX = currentPoint.x + model!.offset_x;
                    firstY = currentPoint.y + model.offset_y;
                    timeEstimator.AddPrintXY(firstX, firstY, printSpeed);

                    // Print line
                    gcode.AppendLine(Invariant($"G1 F{printSpeed * printMultiplier:F0} X{currentPoint.x + model.offset_x:F3} Y{currentPoint.y + model.offset_y:F3} E{extrusion:F5}"));
                    currentExtrusion = extrusion;
                }

                // Close the loop by returning to the start point
                if (closeLoop)
                {
                    var startPoint = path[0];
                    var lastPoint = path[path.Count - 1];
                    double lastExtrusion = currentExtrusion + GcodeHelpers.CalculateExtrusion(lastPoint, startPoint, settings);
                    currentExtrusion = lastExtrusion;

                    // Timing estimation
                    firstX = startPoint.x + model!.offset_x;
                    firstY = startPoint.y + model.offset_y;
                    timeEstimator.AddPrintXY(firstX, firstY, printSpeed);

                    gcode.AppendLine(Invariant($"G1 F{printSpeed * printMultiplier:F0} X{startPoint.x + model.offset_x:F3} Y{startPoint.y + model.offset_y:F3} E{lastExtrusion:F5}"));
                }

                retractHelper.Retract(gcode, currentExtrusion);
                timeEstimator.AddRetract();
            }
        }

        private bool IsHopNeeded(double firstNewX, double firstNewY, double? lastPrevX, double? lastPrevY)
        {
            // If no prev yet, we dont know so safer to hop
            if (lastPrevX == null || lastPrevY == null) return true;

            // Calculate the difference between new and prev point (euclidian)
            double dx = firstNewX - lastPrevX.Value;
            double dy = firstNewY - lastPrevY.Value;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // If distance is greater then 1mm a hop is needed
            // if not we can assume its just the 'same' slice
            return distance > 1.0;
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
            AddShellCode(gcode, slice, settings, extrusionHeight);

            // Print roofs and floors
            AddFloorAndRoofCode(gcode, slice, settings, extrusionHeight);

            // Print infill
            AddInfillCode(gcode, slice, settings, extrusionHeight);

            // Print support
            AddSupportCode(gcode, slice, settings, model, layerIndex, extrusionHeight);
        }

        /// <summary>
        /// Adds the G-code for floor and roof paths to the G-code StringBuilder.
        /// Floor and roof paths are closed paths that need to be traced with extrusion.
        /// </summary>
        /// <param name="gcode">The StringBuilder to append G-code to</param>
        /// <param name="slice">The current slice containing floor and roof paths</param>
        /// <param name="settings">G-code generation settings</param>
        private void AddFloorAndRoofCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double extrusionHeight)
        {
            // Print floor paths
            PathsD? floorPaths = slice.GetFloor();
            if (floorPaths != null && floorPaths.Count > 0)
            {

                PrintPatshD(gcode, floorPaths, settings, settings.FloorSpeed, extrusionHeight, closeLoop: true, slowDown: true, hopEnabled: settings.TreeSupportEnabled);
            }

            // Print roof paths
            PathsD? roofPaths = slice.GetRoof();
            if (roofPaths != null && roofPaths.Count > 0)
            {
                PrintPatshD(gcode, roofPaths, settings, settings.RoofSpeed, extrusionHeight, closeLoop: true, hopEnabled: true, slowDown: true);
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
        private void AddSupportCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, BriocheModel model, int layerIndex, double extrusionHeight)
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
            PrintPatshD(gcode, supportPaths, settings, settings.SupportSpeed, extrusionHeight, hopEnabled: true);

        }

        /// <summary>
        /// Adds the shell code for a single slice.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddShellCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double extrusionHeight)
        {
            PathsD? shellPaths = slice.GetOuterLayer();
            if (shellPaths == null || shellPaths.Count == 0) return;

            RetractHelper retractHelper = new RetractHelper(settings.ExtrusionRetractLength);

            // Trace each shell path
            gcode.AppendLine("; schells");
            PrintPatshD(gcode, shellPaths!, settings, settings.ShellSpeed, extrusionHeight, closeLoop: true, hopEnabled: settings.TreeSupportEnabled);
        }

        /// <summary>
        /// Generate code to print the infill.
        /// </summary>
        /// <param name="gcode"></param>
        /// <param name="slice"></param>
        /// <param name="settings"></param>
        /// <param name="offset_x"></param>
        /// <param name="offset_y"></param>
        private void AddInfillCode(StringBuilder gcode, BriocheSlice slice, GcodeSettings settings, double extrusionHeight)
        {
            PathsD? infillPaths = slice.GetInfill();
            if (infillPaths == null || infillPaths.Count == 0) return;

            // Process each infill line (these are open paths)
            gcode.AppendLine("; infill");
            PrintPatshD(gcode, infillPaths!, settings, settings.InfillSpeed, extrusionHeight, hopEnabled: settings.TreeSupportEnabled);
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
            else
            {
                // Get the lowest outer shell
                var bottomLayer = model.GetSlice(0);
                PathsD? outerShell = bottomLayer.GetOuterShell();

                if (outerShell != null)
                {
                    // Inflate it by a factor of the nozzle diameter
                    double skirtOffset = 10 * settings.NozzleDiameter;
                    PathsD skirtPath = Clipper.InflatePaths(outerShell!, skirtOffset, JoinType.Round, EndType.Polygon);

                    // Print this path once
                    PrintPatshD(gcode, skirtPath, settings, settings.PrintSpeed, extrusionHeight: settings.LayerHeight, slowDown: true);
                }
            }

            // Add a friendly message
            gcode.AppendLine($"; Sliced by BriocheSlicer on {DateTime.Now}. Would you like a slice of brioche?");
            gcode.AppendLine($"; Layer height: {settings.LayerHeight}mm");
            gcode.AppendLine();
        }
    }
}
