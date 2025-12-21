# Question
private bool IsHopNeeded(double firstNewX, double firstNewY, double? lastPrevX, double? lastPrevY)
{
    // If no prev yet, we dont know so safer to hop
    if (lastPrevX == null || lastPrevY == null) return true;


}

In this code how can i calculate the distance between the first and prev point? I want to check if they are in 1 millimeter of each other

# Response
To calculate the distance between the two points and check if they are within 1 millimeter, you can use the Euclidean distance formula: \(\sqrt{(x_2 - x_1)^2 + (y_2 - y_1)^2}\).[1][2]

Here is the completed function. I have included the standard `Math.Sqrt` approach and an optimized version using squared distance.

### Option 1: Standard Calculation
This version calculates the exact distance. Ensure your unit scale is correct (see **Unit Note** below).

```csharp
private bool IsHopNeeded(double firstNewX, double firstNewY, double? lastPrevX, double? lastPrevY)
{
    // If no prev yet, we dont know so safer to hop
    if (lastPrevX == null || lastPrevY == null) return true;

    // 1. Calculate the differences
    double dx = firstNewX - lastPrevX.Value;
    double dy = firstNewY - lastPrevY.Value;

    // 2. Calculate Euclidean distance
    double distance = Math.Sqrt(dx * dx + dy * dy);

    // 3. Check if distance is GREATER than 1mm
    // (If they are within 1mm, we return false because a hop is NOT needed)
    return distance > 1.0; 
}
```

### Option 2: Performance Optimization (Squared Distance)
In game development and VR (like Unity), calculating square roots (`Math.Sqrt`) is computationally expensive. To optimize, compare the **squared distance** against the **squared threshold**.[3][4]

```csharp
private bool IsHopNeeded(double firstNewX, double firstNewY, double? lastPrevX, double? lastPrevY)
{
    if (lastPrevX == null || lastPrevY == null) return true;

    double dx = firstNewX - lastPrevX.Value;
    double dy = firstNewY - lastPrevY.Value;

    // Calculate squared distance (avoids expensive Sqrt)
    double distSq = dx * dx + dy * dy;
    
    // Threshold is 1mm. Squared threshold is 1 * 1 = 1.
    // If working in meters (Unity default), 1mm = 0.001. ThresholdSq = 0.001 * 0.001 = 0.000001
    double threshold = 1.0; 
    
    // If squared distance is greater than squared threshold, hop is needed
    return distSq > (threshold * threshold);
}
```

### Important: Check Your Units
The code above assumes your coordinates (`firstNewX`, `firstNewY`) are already in **millimeters**.
*   **If your coordinates are in Millimeters:** Use `1.0` as the threshold.
*   **If your coordinates are in Meters (Unity Standard):** 1 millimeter equals `0.001` meters. You must change the threshold to `0.001`.[5]

**Using Unity?**
If you are using Unity types, you can also use `Vector2.Distance`:
```csharp
float dist = Vector2.Distance(new Vector2(firstNewX, firstNewY), new Vector2(lastPrevX, lastPrevY));
return dist > 1.0f; // or 0.001f for meters
```

[1](https://www.educative.io/answers/how-to-compute-euclidean-distance-in-c-sharp)
[2](https://www.geeksforgeeks.org/dsa/program-calculate-distance-two-points/)
[3](https://stackoverflow.com/questions/8077982/fastest-way-to-compute-distance-squared)
[4](https://www.reddit.com/r/godot/comments/1ft1ojo/distance_check_optimization/)
[5](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Vector2.Distance.html)
[6](https://stackoverflow.com/questions/11555355/calculating-the-distance-between-2-points)
[7](https://eliasbland.wordpress.com/2009/10/22/euclidean-distance-and-similarity-in-c/)
[8](https://nickgrattan.wordpress.com/2014/06/10/euclidean-manhattan-and-cosine-distance-measures-in-c/)
[9](https://www.w3resource.com/c-programming-exercises/basic-declarations-and-expressions/c-programming-basic-exercises-15.php)
[10](https://docs.unity3d.com/6000.1/Documentation/ScriptReference/Vector2.Distance.html)
[11](http://accord-framework.net/docs/html/M_Accord_Math_Distances_Euclidean_Distance_1.htm)
[12](https://stackoverflow.com/questions/77334954/unity-2d-distance-between-two-objects)
[13](https://unitycodemonkey.com/question.php?q=how-much-does-using-the-square-root-function-in-vector3distance-impact-performance-in-unity)
[14](https://www.geeksforgeeks.org/dsa/closest-pair-of-points-using-divide-and-conquer-algorithm/)
[15](https://www.wayline.io/blog/unity-get-distance-between-two-objects)
[16](https://numerics.net/documentation/latest/mathematics/optimization/multidimensional-optimization)
[17](https://www.codeproject.com/articles/Find-the-Nearest-Color-with-C-Using-the-Euclidean-)
[18](https://www.youtube.com/watch?v=sYf4bSj9j2w)
[19](https://www.facebook.com/groups/IndieGameDevs/posts/10152684488851573/)
[20](https://stackoverflow.com/questions/56090102/how-to-fix-vector2-distance-method)