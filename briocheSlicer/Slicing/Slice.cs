using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace briocheSlicer.Slicing
{
    internal class Slice
    {
        private List<List<BriocheEdge>> polygons; // Store multiple polygons
        private const double EPSILON = 1e-6; // Tolerance for floating point comparison

        public Slice(List<BriocheEdge> edges)
        {
            this.polygons = Connect_Edges(edges);
        }

        /// <summary>
        /// This function connects all the edges.
        /// Results in a ordered list of briocheEdges forming a closed loop.
        /// </summary>
        /// <returns>List of connected BriocheEdges.</returns>
        private static List<List<BriocheEdge>> Connect_Edges(List<BriocheEdge> unfilteredEdges)
        {
            if (unfilteredEdges == null || unfilteredEdges.Count == 0)
                return new List<List<BriocheEdge>>();

            //TODO: Implement edge connection logic here.

            return new List<List<BriocheEdge>>(); ; // Placeholder return
        }
    }
}
