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

        public double InfillSpeed { get; set; }

        public double RoofSpeed { get; set; }

        public double FloorSpeed { get; set; }

        public double ShellSpeed { get; set; }

        public double SupportSpeed { get; set; }

        public double TravelSpeed { get; set; }
        public double ExtrusionMultiplier { get; set; }
        public double FilamentDiameter { get; set; }
        public double FilamentSurfaceArea { get; set; }
        public int NumberShells { get; set; }
        public int NumberFloors { get; set; }
        public int NumberRoofs { get; set; } 
        public double InfillSparsity { get; set; }
        public double SupportSparsity { get; set; }
        public double ExtrusionRetractLength { get; set; }

        public double SupportEnabled { get; set; }
        public bool TreeSupportEnabled { get; set; }

        public GcodeSettings(
            double nozzleDiameter = 0.4,
            double layerHeight = 0.2,
            double printSpeed = 50,
            double infillSpeed = 50,
            double roofSpeed = 50,
            double floorSpeed = 50,
            double shellSpeed = 50,
            double supportSpeed = 50,
            double travelSpeed = 150,
            double extrusionMultiplier = 0.98,
            double filamentDiameter = 1.75,
            double filamentSurfaceArea = 2.405,
            int numberShells = 1,
            int numberFloors = 2,
            int numberRoofs = 2,
            double infillSparsity = 5,
            double supportSparsity = 8,
            double extrusionRectrectionLength = 0.2,
            bool disableSupport = false
            )
        {
            NozzleDiameter = nozzleDiameter;
            LayerHeight = layerHeight;
            PrintSpeed = printSpeed;
            InfillSpeed = infillSpeed;
            RoofSpeed = roofSpeed;
            FloorSpeed = floorSpeed;
            ShellSpeed = shellSpeed;
            SupportSpeed = supportSpeed;
            TravelSpeed = travelSpeed;
            ExtrusionMultiplier = extrusionMultiplier;
            FilamentDiameter = filamentDiameter;
            FilamentSurfaceArea = filamentSurfaceArea;
            NumberShells = numberShells;
            NumberFloors = numberFloors;
            NumberRoofs = numberRoofs;
            InfillSparsity = infillSparsity * nozzleDiameter;
            SupportSparsity = supportSparsity * nozzleDiameter;
            ExtrusionRetractLength = extrusionRectrectionLength;
            TreeSupportEnabled = disableSupport;
        }
    }
}
