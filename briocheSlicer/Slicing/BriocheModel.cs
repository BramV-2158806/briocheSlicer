using briocheSlicer.Gcode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clipper2Lib;
using System.Runtime.InteropServices.Marshalling;
using System.Diagnostics.Eventing.Reader;
using System.Numerics;

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
        ///  Constructor of the briocheSlice. The slices are already created, it modifies the slices by adding
        ///  roofs, floors, support and infill.
        ///  Before the slices are given to the briochemodel they just represent peremiters.
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
        /// ** This function is written by AI **
        /// </summary>
        /// <param name="currentIndex"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private List<BriocheSlice> GetLayersBelow(int currentIndex, int count)
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
        /// ** This function is written by AI **
        /// </summary>
        /// <param name="currentIndex"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private List<BriocheSlice> GetLayersAbove(int currentIndex, int count)
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

        /// <summary>
        /// Moves from the bottom layer to the top.
        /// It generates floors.
        /// This is the first pass over the model, after the initial stage.
        /// </summary>
        private void Upwards_Pass()
        {
            for (int i = 0; i < this.amount_Layers; i++)
            {
                var slice = this.layers[i];

                if (i < settings.NumberFloors)
                {
                    slice.Generate_Floor(new List<PathsD>(), true);
                }
                else
                {
                    var prev_layers = GetLayersBelow(i, settings.NumberFloors);
                    var prev_inner_perims = prev_layers.Select(l => l.GetInnerShell()).ToList();
                    slice.Generate_Floor(prev_inner_perims!);
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
                var prev_slice = null as BriocheSlice;
                if (i != 0) 
                    prev_slice = GetSlice(i - 1);

                // Process top layer
                if (i >= this.amount_Layers - settings.NumberRoofs)
                {
                    slice.Generate_Roof(new List<PathsD>(), true);
                    if (!settings.TreeSupportEnabled)
                    {
                        slice.Generate_Support(new PathsD(), i, true);
                    }
                }
                else
                {
                    // Handle roofs
                    // For previous layers we have to cal the above function.
                    // We are moving down so prev is up.
                    var prev_layers = GetLayersAbove(i, settings.NumberRoofs);
                    var prev_innerPerimiters = prev_layers.Select(l => l.GetInnerShell()).ToList();
                    slice.Generate_Roof(prev_innerPerimiters!);

                    // Handle support
                    if (!settings.TreeSupportEnabled && settings.SupportEnabled)
                    {
                        var prev_layer = GetLayersAbove(i, 1);
                        var prev_outerPerimeter = prev_layer[0].GetOuterShell()!;
                        var prev_support = prev_layer[0].GetSupportRegion()!;
                        var prev_perim_support = Clipper.Union(prev_outerPerimeter, prev_support, FillRule.EvenOdd);
                        slice.Generate_Support(prev_perim_support, i);
                    }
                }

                slice.Generate_Infill();
                if (slice.GetFloor()!.Count != 0 && prev_slice != null && prev_slice.GetFloor()!.Count == 0 && slice.GetInfill()!.Count != 0)
                {
                    // Merge infill and floor
                    slice.Join_Floor_and_Infill();
                }
            }
        }
    }
}
