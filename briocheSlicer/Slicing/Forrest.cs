using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing
{
    internal class Forrest
    {
        private List<TrunkPath> forrest;
        private double growthSpeed;
        private List<SeedCluster> clusters;

        public Forrest(double growthSpeed, List<SeedCluster> clusters)
        {
            this.growthSpeed = growthSpeed;
            this.forrest = new List<TrunkPath>(clusters.Count);
            this.clusters = clusters;
        }

        public Model3DGroup GrowAround(ModelVisual3D scene)
        {
            bool doneGrowing = false;

            while (!doneGrowing) 
            {
                for (int trunkIndex = 0; trunkIndex < forrest.Count; trunkIndex++)
                {
                    if (forrest[trunkIndex] != null && !forrest[trunkIndex].IsDoneGrowing())
                    forrest[trunkIndex].Grow(growthSpeed, scene);
                }
                Merge();
                doneGrowing = AllTrunksDone();
            }

            var modelGroup = new Model3DGroup();
            foreach (var trunk in  forrest) 
            {
                var trunkModel = trunk.Thicken();
                modelGroup.Children.Add(trunkModel);
            }
            return modelGroup;
        }

        /// <summary>
        /// Merges trunks if needed.
        /// Check if they need to be merged based on there area size and position.
        /// </summary>
        private void Merge() 
        {
            for (int outerTrunkIndex = 0; outerTrunkIndex < forrest.Count; outerTrunkIndex++)
            {
                for (int innerTrunkIndex = outerTrunkIndex + 1; innerTrunkIndex < forrest.Count; innerTrunkIndex++)
                {
                    var t1 = forrest[outerTrunkIndex];
                    var t2 = forrest[innerTrunkIndex];

                    var t1PosNullable = t1.GetCurrentPosition();
                    var t2PosNullable = t2.GetCurrentPosition();

                    if (t1PosNullable.HasValue && t2PosNullable.HasValue)
                    {
                        var t1Pos = t1PosNullable.Value;
                        var t2Pos = t2PosNullable.Value;

                        // AABB overlap test in 3D
                        bool isColliding =
                            (Math.Abs(t1Pos.X - t2Pos.X) <= (t1.GetTrunkAreaSize() + t2.GetTrunkAreaSize())) &&
                            (Math.Abs(t1Pos.Y - t2Pos.Y) <= (t1.GetTrunkAreaSize() + t2.GetTrunkAreaSize())) &&
                            (Math.Abs(t1Pos.Z - t2Pos.Z) <= (t1.GetTrunkAreaSize() + t2.GetTrunkAreaSize()));

                        if (isColliding)
                        {
                            t1.Merge(t2);
                            t2.SetIsDoneGorwing(true);
                        }
                    }
                }
            }
        }

        private bool AllTrunksDone()
        {
            foreach (var trunk in forrest)
            {
                if (trunk != null && !trunk.IsDoneGrowing())
                {
                    return false;
                }
            }
            return true;
        }
    }
}
