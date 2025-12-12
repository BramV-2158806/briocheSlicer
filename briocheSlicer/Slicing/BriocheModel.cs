using briocheSlicer.Gcode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clipper2Lib;
using System.Runtime.InteropServices.Marshalling;

namespace briocheSlicer.Slicing
{
    internal class BriocheModel
    {
        private List<BriocheSlice> layers;
        private GcodeSettings settings;
        public readonly int amount_Layers;
        public readonly double offset_x;
        public readonly double offset_y;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newSlices">
        /// The list of slices. Ordered bottom upwards.
        /// So slice[0] is the bottom slice and slice[count] is the top slice.
        /// </param>
        /// <param name="settings"></param>
        public BriocheModel(List<BriocheSlice> newSlices, GcodeSettings settings, double offset_x, double offset_y)
        {
            this.layers = newSlices;
            this.amount_Layers = layers.Count;
            this.settings = settings;
            this.offset_x = offset_x;
            this.offset_y = offset_y;

            Upwards_Pass();
            Downwards_Pass();
        }

        public BriocheSlice? GetSlice(int index)
        {
            if (index < 0 || index >= layers.Count)
            {
                return null;
            }
            return layers[index];
        }

        /// <summary>
        /// This function is written by AI
        /// </summary>
        /// <param name="currentIndex"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private List<BriocheSlice> GetPreviousLayers(int currentIndex, int count)
        {
            if (count <= 0 || currentIndex <= 0 || layers.Count == 0)
                return new List<BriocheSlice>(0);

            int startIndex = Math.Max(0, currentIndex - count);
            int endExclusive = currentIndex; // not including current
            var result = new List<BriocheSlice>(endExclusive - startIndex);

            for (int i = startIndex; i < endExclusive; i++)
                result.Add(layers[i]);

            return result;
        }

        /// <summary>
        /// This function is written by AI
        /// </summary>
        /// <param name="currentIndex"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private List<BriocheSlice> GetFollowingLayers(int currentIndex, int count)
        {

            if (count <= 0 || layers.Count == 0 || currentIndex >= layers.Count - 1)
                return new List<BriocheSlice>(0);

            int startIndex = currentIndex + 1;
            int endExclusive = Math.Min(layers.Count, startIndex + count);
            var result = new List<BriocheSlice>(endExclusive - startIndex);

            for (int i = startIndex; i < endExclusive; i++)
                result.Add(layers[i]);

            return result;
        }

        private void Upwards_Pass()
        {
            var n = settings.NumberFloors;
            for (int i = 0; i < this.amount_Layers; i++)
            {
                var slice = this.layers[i];

                if (i < n)
                {
                    // process base layers
                    slice.Generate_Floor(new List<PathsD>(), true);
                }
                else
                {
                    // Example: build a list of the previous n layers (bottom-to-top order)
                    var previousNLayers = GetPreviousLayers(i, n);
                    var perimiters = previousNLayers.Select(l => l.GetInnerShell()).ToList();
                    slice.Generate_Floor(perimiters);
                }
            }
        }

        /// <summary>
        /// Moves from the top layer down to the bottom layer
        /// and generates roofs.
        /// 
        /// This is presumed to be the final pass over the model
        /// so we also generate infill here.
        /// </summary>
        private void Downwards_Pass()
        {
            for (int i = this.amount_Layers - 1; i >= 0; i--)
            {
                var slice = this.layers[i];

                if (i >= this.amount_Layers - settings.NumberRoofs)
                {
                    // process top layers
                    slice.Generate_Roof(new List<PathsD>(), true);
                }
                else
                {
                    var followingNLayers = GetFollowingLayers(i, settings.NumberRoofs);
                    var perimiters = followingNLayers.Select(l => l.GetInnerShell()).ToList();
                    slice.Generate_Roof(perimiters);
                }

                slice.Generate_Infill();

                if (i == this.amount_Layers - 1)
                {
                    slice.Generate_Support(new PathsD(), true);
                } else
                {
                    var nextLayer = GetFollowingLayers(i, 1);
                    var outerPerimeter = nextLayer[0].GetOuterLayer()!;
                    var support = nextLayer[0].GetSupportRegion()!;
                    var union = Clipper.Union(outerPerimeter, support, FillRule.EvenOdd);
                    slice.Generate_Support(union);
                }
            }
        }
    }
}
