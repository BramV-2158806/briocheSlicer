using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Gcode
{
    /// <summary>
    /// Data class used to set the settins for G-code generation.
    /// </summary>
    internal class GcodeSettings
    {
        public double NozzleDiameter { get; set; } = 0.4;
        public double LayerHeight { get; set; } = 0.2;
        public double PrintSpeed { get; set; } = 50;
        public double TravelSpeed { get; set; } = 150;
        public double ExtrusionMultiplier { get; set; } = 1.0; // mogelijks 0.015 als we kijke naar calibratie van assignment 2
        public double FilamentDiameter { get; set; } = 1.75;
        public double FilamentSurfaceArea { get; set; } = 2.405;
        public int NumberShells { get; set; } = 1;

        


        public GcodeSettings(
            double nozzleDiameter = 0.4,
            double layerHeight = 0.2,
            double printSpeed = 50,
            double travelSpeed = 150,
            double extrusionMultiplier = 1.0,
            double filamentDiameter = 1.75,
            double filamentSurfaceArea = 2.405,
            int numberShells = 1)
        {
            NozzleDiameter = nozzleDiameter;
            LayerHeight = layerHeight;
            PrintSpeed = printSpeed;
            TravelSpeed = travelSpeed;
            ExtrusionMultiplier = extrusionMultiplier;
            FilamentDiameter = filamentDiameter;
            FilamentSurfaceArea = filamentSurfaceArea;
            NumberShells = numberShells;
        }
    }
}
