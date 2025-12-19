# Question
using System.Windows.Media.Media3D;

namespace briocheSlicer.Slicing
{
internal class TrunkPath
{
// Grow direction
private readonly Vector3D down = new Vector3D(0, 0, -1);

private List<Point3D> points;
private double touchAreaSize { get; }
private bool isDoneGrowing { get; }
private Point3D? currentPosition { get; }

public TrunkPath(double touchAreaSize, List<Point3D>? points = null)
{
if (points == null)
{
this.points = new List<Point3D>();
this.currentPosition = null;
}
else
{
this.points = points;
this.currentPosition = points.Last();
}

this.touchAreaSize = touchAreaSize;
this.isDoneGrowing = false;
}

public void Grow(double growthSpeed, Model3DGroup model)
{

}
}
}


i would like to use the helix toolkit to shoot rays in the direction im heading (down) if i hit something and its within some distance of the current position i want to get the normal of the point i hit and move my next position away from the model using that normal

# Answer
Use a WPF 3D ray hit test: construct a `RayHitTestParameters` from your current position and `down` direction, call `VisualTreeHelper.HitTest` on the `Viewport3D`, and in the callback use `RayMeshGeometry3DHitTestResult` to get the hit point and its normal.[1][2]

## 1. Shooting a ray from your current position

You need access to the `Viewport3D` that contains the `Model3DGroup`, because hit testing works on visuals, not on the `Model3DGroup` alone.[3][1]

```csharp
public void Grow(double growthSpeed, Viewport3D viewport)
{
    if (currentPosition == null)
        return;

    // Ray origin = current trunk position
    Point3D origin = currentPosition.Value;
    Vector3D direction = down;  // (0,0,-1) in your class
    direction.Normalize();

    var rayParams = new RayHitTestParameters(origin, direction);

    RayMeshGeometry3DHitTestResult? bestHit = null;

    HitTestResultCallback callback = result =>
    {
        var rayResult = result as RayHitTestResult;
        if (rayResult is RayMeshGeometry3DHitTestResult meshHit)
        {
            // Optionally filter by distance from origin
            double dist = (meshHit.PointHit - origin).Length;
            if (dist <= touchAreaSize) // your max distance
            {
                // Keep the nearest hit within the allowed distance
                if (bestHit == null || dist < (bestHit.PointHit - origin).Length)
                    bestHit = meshHit;
            }
        }
        return HitTestResultBehavior.Continue;
    };

    VisualTreeHelper.HitTest(viewport, null, callback, rayParams);

    if (bestHit != null)
    {
        HandleHit(bestHit, growthSpeed);
    }
    else
    {
        // no hit within range → keep growing straight down, or stop
    }
}
```

`RayMeshGeometry3DHitTestResult` gives you: the hit point (`PointHit`), the mesh (`MeshHit`), and indices of the triangle (`VertexIndex1/2/3`).[4][1]

## 2. Getting the normal at the hit point

You can either use the per‑vertex normals from the hit mesh or compute a triangle normal from the three vertices.[5][6]

```csharp
private void HandleHit(RayMeshGeometry3DHitTestResult hit, double growthSpeed)
{
    var mesh = hit.MeshHit;

    // Triangle vertices
    var p0 = mesh.Positions[hit.VertexIndex1];
    var p1 = mesh.Positions[hit.VertexIndex2];
    var p2 = mesh.Positions[hit.VertexIndex3];

    // Option A: triangle face normal (robust if vertex normals are not set nicely)
    Vector3D faceNormal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
    faceNormal.Normalize();

    // Option B: interpolate per-vertex normals if available
    Vector3D normal;
    if (mesh.Normals != null && mesh.Normals.Count == mesh.Positions.Count)
    {
        var n0 = mesh.Normals[hit.VertexIndex1];
        var n1 = mesh.Normals[hit.VertexIndex2];
        var n2 = mesh.Normals[hit.VertexIndex3];
        normal = n0 + n1 + n2;
        normal.Normalize();
    }
    else
    {
        normal = faceNormal;
    }

    // Move next position away from model along the normal
    Point3D hitPoint = hit.PointHit;
    double offset = growthSpeed; // or some separation distance
    Point3D nextPos = hitPoint + normal * offset;

    points.Add(nextPos);
    // update currentPosition field if you expose a setter or make it non-readonly
}
```

This gives you a raycast from your current trunk position, checks if the hit is within `touchAreaSize`, extracts the surface normal at that hit triangle, and then steps your trunk outward along that normal to define the next `Point3D` in the path.[7][1]

[1](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-hit-test-in-a-viewport3d)
[2](https://www.c-sharpcorner.com/UploadFile/yougerthen/wpf-and-user-interactivity-part-i-dealing-with-geometries-and-shapes/)
[3](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/3-d-graphics-overview)
[4](https://stackoverflow.com/questions/6817106/3d-hit-testing-in-wpf)
[5](https://www.charlespetzold.com/blog/2006/12/220812.html)
[6](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.media3d.meshgeometry3d.positions?view=windowsdesktop-10.0)
[7](https://stackoverflow.com/questions/66891594/calculate-the-centroid-of-a-3d-mesh-of-triangles)
[8](https://stackoverflow.com/questions/40471645/helixtoolkit-how-to-exclude-elements-from-hittest)
[9](https://github.com/helix-toolkit/helix-toolkit/issues/1114)
[10](https://helixtoolkit.userecho.com/communities/1/topics/1130-how-to-perform-mouse-click-hit-testing-on-modelvisual3d-models-in-helixtoolkit)
[11](https://github.com/helix-toolkit/helix-toolkit/issues/376)
[12](https://github.com/helix-toolkit/helix-toolkit/issues/1696)
[13](https://gitee.com/lemorlee/helix-toolkit/blob/master/CHANGELOG.md)
[14](https://blog.csdn.net/wmjcom/article/details/5090251)
[15](https://stackoverflow.com/questions/tagged/helix-3d-toolkit?tab=newest&page=4)
[16](https://github.com/helix-toolkit/helix-toolkit/issues/1225)
[17](https://stackoverflow.com/questions/800040/how-to-pass-the-following-parameter-hittestparameters3d-in-visualtreehelper-hit)
[18](https://helix-toolkit.github.io/announcements/release-2.13.1)
[19](https://github.com/helix-toolkit/helix-toolkit/issues/1353)
[20](https://www.codeproject.com/articles/3D-gadgets-for-your-WPF-Desktop)
[21](https://svn.oss.deltares.nl/repos/openearthtools/websites/dataviewer/dotnet/WPF/Third-Party/HelixToolkit/Helpers/Viewport3DHelper.cs)
[22](https://learn.microsoft.com/nl-nl/dotnet/desktop/wpf/graphics-multimedia/how-to-hit-test-using-geometry-as-a-parameter?view=netframeworkdesktop-4.8)
[23](https://www.charlespetzold.com/blog/2007/03/120326.html)
[24](https://www.fpaynter.com/tag/helix-toolkit/)