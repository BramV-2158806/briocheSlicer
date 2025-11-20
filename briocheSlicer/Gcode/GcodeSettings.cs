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
        public double NozzleDiameter { get; set; }
        public double LayerHeight { get; set; }
        public double PrintSpeed { get; set; }
        public double TravelSpeed { get; set; }
        public double ExtrusionMultiplier { get; set; }
        public double FilamentDiameter { get; set; }
        public double FilamentSurfaceArea { get; set; }
        public int NumberShells { get; set; }
        public int NumberFloors { get; set; }
        public int NumberRoofs { get; set; } 

        public GcodeSettings(
            double nozzleDiameter = 0.4,
            double layerHeight = 0.2,
            double printSpeed = 50,
            double travelSpeed = 150,
            double extrusionMultiplier = 0.98, // Calibratie van bambuu fillament
            double filamentDiameter = 1.75,
            double filamentSurfaceArea = 2.405,
            int numberShells = 1,
            int numberFloors = 2,
            int numberRoofs = 2)
        {
            NozzleDiameter = nozzleDiameter;
            LayerHeight = layerHeight;
            PrintSpeed = printSpeed;
            TravelSpeed = travelSpeed;
            ExtrusionMultiplier = extrusionMultiplier;
            FilamentDiameter = filamentDiameter;
            FilamentSurfaceArea = filamentSurfaceArea;
            NumberShells = numberShells;
            NumberFloors = numberFloors;
            NumberRoofs = numberRoofs;
        }
    }
}
