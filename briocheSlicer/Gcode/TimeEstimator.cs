using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Gcode
{
    internal class TimeEstimator
    {
        public double TotalTimeSeconds { get; private set; }
        public double PrintTimeSeconds { get; private set; }
        public double TravelTimeSeconds { get; private set; }
        public double ZTimeSeconds { get; private set; }

        // Toolhead state
        private double lastX = 0;
        private double lastY = 0;
        private double lastZ = 0;

        public void Reset()
        {
            TotalTimeSeconds = 0;
            PrintTimeSeconds = 0;
            TravelTimeSeconds = 0;
            ZTimeSeconds = 0;

            lastX = lastY = lastZ = 0;
        }

        public void AddTravelXY(double x, double y, double speedMmPerSec)
        {
            double dist = Distance2D(lastX, lastY, x, y);
            AddTime(dist, speedMmPerSec, TravelTimeSeconds);

            lastX = x;
            lastY = y;
        }

        public void AddPrintXY(double x, double y, double speedMmPerSec)
        {
            double dist = Distance2D(lastX, lastY, x, y);
            AddTime(dist, speedMmPerSec, PrintTimeSeconds);

            lastX = x;
            lastY = y;
        }

        public void AddZMove(double z, double speedMmPerSec)
        {
            double dist = Math.Abs(z - lastZ);
            AddTime(dist, speedMmPerSec, ZTimeSeconds);

            lastZ = z;
        }

        public void AddRetract(double seconds = 0.15)
        {
            TotalTimeSeconds += seconds;
        }

        private void AddTime(double distance, double speed, double bucket)
        {
            if (speed <= 0 || distance <= 0) return;

            double t = distance / speed;
            bucket += t;
            TotalTimeSeconds += t;
        }

        private static double Distance2D(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public TimeSpan GetTimeSpan()
        {
            return TimeSpan.FromSeconds(TotalTimeSeconds);
        }

        public string FormatDuration(TimeSpan t)
        {
            int hours = (int)t.TotalHours;
            return $"{hours}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}

