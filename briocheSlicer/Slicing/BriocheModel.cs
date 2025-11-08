using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Slicing
{
    internal class BriocheModel
    {
        private List<BriocheSlice> layers;
        public readonly int amount_Layers;
        public BriocheModel(List<BriocheSlice> newSlices) 
        { 
            layers = newSlices; 
            amount_Layers = layers.Count;
        }

        public BriocheSlice GetSlice(int index)
        {
            return layers[index];
        }

    }
}
