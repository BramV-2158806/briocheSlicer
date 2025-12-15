using System.Text;
using static System.FormattableString;

namespace briocheSlicer.Gcode
{
    internal class RetractHelper
    {
        private bool has_retracted;
        private double retraction_amount;
        public RetractHelper(double retraction_amount) 
        { 
            this.has_retracted = false;
            this.retraction_amount = retraction_amount;
        }

        public void Retract(StringBuilder gcode, double current_extrusion)
        {
            if (!has_retracted)
            {
                gcode.AppendLine(Invariant($"G1 E{current_extrusion - retraction_amount:F5}"));
                has_retracted = true;
            }
        }

        public void Reset(StringBuilder gcode, double current_extrusion)
        {
            if (has_retracted)
            {
                gcode.AppendLine(Invariant($"G1 E{current_extrusion:F5}"));
                has_retracted = false;
            }
        }
    }
}
