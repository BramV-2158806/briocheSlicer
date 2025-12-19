using HelixToolkit.Wpf;
using System.IO;
using System.Windows.Media.Media3D;
using static MR.DotNet;

namespace briocheSlicer.Slicing.TreeSupport
{
    public static class MeshUtils
    {
        public static Mesh ToMeshLib(Model3DGroup helixModelGroup)
        {
            // 1. Define temporary file paths
            string tempFile = Path.GetTempFileName() + ".obj";
            string tempMtlFile = Path.ChangeExtension(tempFile, ".mtl");

            try
            {
                // 2. Export Helix Model3DGroup to OBJ
                var exporter = new ObjExporter();
                exporter.MaterialsFile = tempMtlFile;  // Set the materials file path
                using (var stream = File.Create(tempFile))
                {
                    exporter.Export(helixModelGroup, stream);
                }

                // 3. Load into MeshLib
                Mesh mesh = MeshLoad.FromAnySupportedFormat(tempFile);

                // Repair possible holes in mesh
                var holes = mesh.HoleRepresentiveEdges;
                var fillHoleParams = new FillHoleParams();
                fillHoleParams.Metric = FillHoleMetric.GetUniversalMetric(mesh);
                fillHoleParams.OutNewFaces = new FaceBitSet();

                FillHoles(ref mesh, holes.ToList(), fillHoleParams);

                // Fix self intersections
                // 1. Calculate a voxel size based on your desired precision
                // Smaller size = higher quality but slower.
                // A good rule of thumb is bounding box diagonal / 500
                float voxelSize = mesh.BoundingBox.Diagonal() / 500.0f;

                // 2. Perform the remeshing
                // This function might be named slightly differently depending on version (e.g., 'FromMesh', 'ToMesh')
                // If 'MakeWatertight' is not available directly, use Offset with 0 distance:
                OffsetParameters offsetParams = new OffsetParameters();
                offsetParams.voxelSize = voxelSize;

                // Offset of 0.0 essentially rebuilds the mesh from scratch
                Mesh cleanMesh = Offset.OffsetMesh(new MeshPart(mesh), 0.0f, offsetParams);

                return cleanMesh;
            }
            finally
            {
                // 4. Cleanup both files
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
                if (File.Exists(tempMtlFile))
                {
                    File.Delete(tempMtlFile);
                }
            }
        }

        public static Model3DGroup ToHelixModel(Mesh meshLibMesh)
        {
            // 1. Save MeshLib result to temp file
            string tempFile = Path.GetTempFileName() + ".obj";

            try
            {
                MeshSave.ToAnySupportedFormat(meshLibMesh, tempFile);

                // 2. Load back into Helix Toolkit
                var importer = new ModelImporter();
                Model3DGroup group = importer.Load(tempFile);
                return group;
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputMesh"></param>
        /// <param name="shrinkAmount"> Has to be a negative value.</param>
        /// <returns></returns>
        public static Mesh ShrinkMesh(Mesh inputMesh, float shrinkAmount = -0.5f)
        {
            // 1. Wrap your mesh in a MeshPart (Required for offset operations)
            // The offset engine treats the mesh as a "part" of a potential assembly.
            MeshPart meshPart = new MeshPart(inputMesh);

            // 2. Setup Offset Parameters
            // The algorithm is voxel-based, so you must specify a voxel size (resolution).
            // A helper function 'SuggestVoxelSize' calculates a good default based on your mesh size.
            OffsetParameters offsetParams = new OffsetParameters();
            offsetParams.voxelSize = Offset.SuggestVoxelSize(meshPart, 1e6f); // 1e6f is a standard precision factor

            // 3. Perform the Offset (Shrink)
            // -1.0f = shrink by 1 unit (e.g., 1mm)
            // +1.0f = expand by 1 unit
            Mesh resultMesh = Offset.OffsetMesh(meshPart, shrinkAmount, offsetParams);

            return resultMesh;
        }

        private static Mesh CreateFloorBox(float size = 10000.0f)
        {
            // 1. Create simple OBJ content string for a cube
            // Center at (0,0,-size/2) to have top face at Z=0
            float zTop = 0;
            float zBottom = -size;
            float halfSize = size / 2.0f;

            string objContent = $@"
                v {-halfSize} {-halfSize} {zBottom}
                v {halfSize} {-halfSize} {zBottom}
                v {halfSize} {halfSize} {zBottom}
                v {-halfSize} {halfSize} {zBottom}
                v {-halfSize} {-halfSize} {zTop}
                v {halfSize} {-halfSize} {zTop}
                v {halfSize} {halfSize} {zTop}
                v {-halfSize} {halfSize} {zTop}
                f 1 4 3 2
                f 5 6 7 8
                f 1 2 6 5
                f 2 3 7 6
                f 3 4 8 7
                f 4 1 5 8
                ";

            // 2. Write to temp file
            string tempPath = Path.GetTempFileName() + ".obj";
            File.WriteAllText(tempPath, objContent);

            try
            {
                // 3. Load using MeshLib's robust loader
                return MeshLoad.FromAnySupportedFormat(tempPath);
            }
            finally
            {
                // 4. Cleanup
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        public static Mesh Ground(Mesh input_mesh)
        {
            var floorBox = CreateFloorBox();
            var result = Boolean(input_mesh, floorBox, BooleanOperation.DifferenceAB);
            return result.mesh;
        }

        public static Mesh ClipMeshAtHeight(Mesh inputMesh, float minZ)
        {
            // Create a large box that extends from minZ down to far below
            float size = 10000.0f;
            float halfSize = size / 2.0f;

            string objContent = $@"
                v {-halfSize} {-halfSize} {minZ - size}
                v {halfSize} {-halfSize} {minZ - size}
                v {halfSize} {halfSize} {minZ - size}
                v {-halfSize} {halfSize} {minZ - size}
                v {-halfSize} {-halfSize} {minZ}
                v {halfSize} {-halfSize} {minZ}
                v {halfSize} {halfSize} {minZ}
                v {-halfSize} {halfSize} {minZ}
                f 1 4 3 2
                f 5 6 7 8
                f 1 2 6 5
                f 2 3 7 6
                f 3 4 8 7
                f 4 1 5 8
                ";

            string tempPath = Path.GetTempFileName() + ".obj";
            try
            {
                File.WriteAllText(tempPath, objContent);
                Mesh cutoffBox = MeshLoad.FromAnySupportedFormat(tempPath);
                
                // Remove everything below minZ
                var result = Boolean(inputMesh, cutoffBox, BooleanOperation.DifferenceAB);
                return result.mesh;
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
