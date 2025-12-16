# Question
im working with the helix toolkit in c# and am wondering how i can loop over all the traingles of a given 3Dgroup. I would like to calculate the angle between the normal of each triangle and the up direction vector. How can i calculate this angle? Is there a dot product function in helix?

# Answer
Calculating Triangle Normals - 3D Games in GameMaker - YouTube trailer
Calculating Triangle Normals - 3D Games in GameMaker - YouTube
Unity Tutorial: Vector3.Dot trailer
Unity Tutorial: Vector3.Dot
You loop the triangles by reading the MeshGeometry3D’s TriangleIndices in steps of three, compute a normal from the three vertices, then use Vector3D.DotProduct with your up vector and convert that to an angle.​

Accessing triangles in a 3DGroup
Each GeometryModel3D in your Model3DGroup typically has a MeshGeometry3D in its Geometry property, with positions and triangle indices.​

Example to traverse a Model3DGroup and enumerate triangles:

csharp
void ProcessModel(Model3D model, Vector3D up)
{
    if (model is Model3DGroup group)
    {
        foreach (var child in group.Children)
            ProcessModel(child, up);
    }
    else if (model is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
    {
        var positions = mesh.Positions;
        var indices   = mesh.TriangleIndices;

        for (int i = 0; i < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            Point3D p0 = positions[i0];
            Point3D p1 = positions[i1];
            Point3D p2 = positions[i2];

            // Compute triangle normal and angle to up
            Vector3D normal = GetTriangleNormal(p0, p1, p2);
            double angleRad = GetAngleBetween(normal, up);
            double angleDeg = angleRad * 180.0 / Math.PI;

            // use angleDeg as needed
        }
    }
}
Computing triangle normals
A standard way to get the triangle normal is cross product of two edges.​

csharp
Vector3D GetTriangleNormal(Point3D p0, Point3D p1, Point3D p2)
{
    Vector3D u = p1 - p0;
    Vector3D v = p2 - p0;
    Vector3D n = Vector3D.CrossProduct(u, v);
    n.Normalize();
    return n;
}
Dot product and angle to “up”
HelixToolkit (WPF) uses Point3D/Vector3D from System.Windows.Media.Media3D, which already provides a static Vector3D.DotProduct method, so you do not need a Helix-specific dot function.​

To compute the angle between the triangle normal and an up direction (for example new Vector3D(0, 1, 0)):

csharp
double GetAngleBetween(Vector3D a, Vector3D b)
{
    a.Normalize();
    b.Normalize();
    double dot = Vector3D.DotProduct(a, b); // in [-1, 1]
    dot = Math.Max(-1.0, Math.Min(1.0, dot)); // clamp for safety
    return Math.Acos(dot); // radians
}
Usage:

csharp
Vector3D up = new Vector3D(0, 1, 0);
ProcessModel(myModel3DGroup, up);
So: loop indices in triples, compute the triangle normal via cross product, then use Vector3D.DotProduct and Math.Acos to get the angle to your up vector.​

# Question
Im pretty sure the helix toolkit has a normal property for each vertices

# Answer
Yes, MeshGeometry3D exposes per-vertex normals; you can read them from the Normals collection instead of computing them yourself.​

Using the built-in normals
In HelixToolkit.Wpf you still end up with a standard WPF MeshGeometry3D, which has these key collections: Positions, TriangleIndices, Normals, TextureCoordinates. The Normals property is a Vector3DCollection with one normal per vertex, aligned by index with Positions (i.e., normal at index i belongs to position at index i).​

Example of accessing them while looping triangles:

csharp
void ProcessModel(Model3D model, Vector3D up)
{
    if (model is Model3DGroup group)
    {
        foreach (var child in group.Children)
            ProcessModel(child, up);
    }
    else if (model is GeometryModel3D geom && geom.Geometry is MeshGeometry3D mesh)
    {
        var positions = mesh.Positions;
        var indices   = mesh.TriangleIndices;
        var normals   = mesh.Normals; // per-vertex normals

        for (int i = 0; i < indices.Count; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            // Example: use the first vertex’s normal as the face normal
            Vector3D n = normals[i0];
            n.Normalize();

            Vector3D upNormalized = up;
            upNormalized.Normalize();

            double dot = Vector3D.DotProduct(n, upNormalized);
            dot = Math.Max(-1.0, Math.Min(1.0, dot));
            double angleRad = Math.Acos(dot);
            double angleDeg = angleRad * 180.0 / Math.PI;

            // use angleDeg
        }
    }
}
If your imported mesh did not come with normals, Helix/MeshGeometry3D can generate them (e.g., via helpers like CalculateNormals() in Helix extensions), after which mesh.Normals will be populated and you can use the same pattern.​