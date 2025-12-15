using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Gcode
{
    internal class GcodeHelpers
    {
        /// <summary>
        /// Implements the formula from the slides to calculate the extrusion for a given edge.
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static double CalculateExtrusion(PointD start, PointD end, GcodeSettings settings)
        {
            double edge_length = Math.Sqrt(
                Math.Pow(end.x - start.x, 2) +
                Math.Pow(end.y - start.y, 2)
            );
            double result = (settings.LayerHeight * settings.NozzleDiameter * edge_length) / settings.FilamentSurfaceArea;
            return result * settings.ExtrusionMultiplier;
        }


        /// <summary>
        /// Loads gcode from the specified file into a string.
        /// It searches for the file in a Resource folder.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>String: the content of the file</returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static string LoadGcodeFromFile(string filename)
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
    }
}
