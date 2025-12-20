# Brioche Slicer
The brioche slicer is an STL object slicer. It implements the traditional slicing techniques used in FDM printing slicers. 
And creates a .gcode files which can be sent to a 3D printer. Do take note that this slicers creates gcode for the bambulab A1 printer.
The start and endcode are thus specialised for the A1 printer. However these should be easily modified by change the start and end gcode file
in the resources directory.

This project is a Visual Studio solution which uses the following NuGet packages:
- Clipper2: used for the toolpath generations and the boolean set opperations on the path regions.
- HelixToolkit WPF: used to create the user interface and visualize the 3D models aswell as the slice view.
- HdbscanSharp: used for hierarchical clustering of the seeds to efficiently calculate the paths for the TRUNK support.
- MeshLib: used for boolean set opperations on the 3D meshes of the object and the trunk support.

# Solution structure and high level workings
The solution is made up of **six main parts**:
- Docs/
- Gcode/
- Rendering/
- Resources/
- Slicing/
- MainWindow.xaml + MainWindow.xaml.cs

In the followig sections we will give a high level overview of the workings of the project.
This is mainly so the teaching staf can easily move to critical functions.

## Docs/
The docs directory contains the prompts send to AI, as requested by the teaching staf.

## MainWindow.xaml + MainWindow.xaml.cs
The **MainWindow.xaml** is a file that describes the UI  using XML. If we are not mistaken, helix parses this file and creates variables for the elements
mentioned in the xaml file. These elements can be accessed in the **MainWindow.xaml.cs** where the UI logic is implemented. The slice and print button are by default disabled.
When loading a model, the slice button becomes enabled, When a model is sliced the print button becomes enabled. After slicing the model the slice plane also becomes controllable.
It is here in the _MainWindow.Slice_Click()_ where the slicer object gets created and where the 3D model and print settings trun into a sliced brioche model.

**Intersting functions are**:
- MainWindow.Slice_Click()

## Slicing/
The slicing directory holds the meat of the project. This is where a combination of a 3D model and some print settings get turned into a collection of toolpaths (PathsD) to
be fed into the code generation for printing. The slicing starts in the _TheSlicer.Slice_Model()_ where the model gets devided in a variable number of slices. 
To calculate the number slices the model bounds and layerheight are used. 

With the model devided in slices, we calculate the perimiter of the model at each slice using the _TheSlicer.Slice_Plane()_ function. 
This function implements the **first of the three stage** slicing project. The model consists of a list of triangles, each trianlge consists of three edges.
At this stage we loop over the triangles and check which triangles have exactly two edges of which one node is above, and one is below the slicing plane.
With these triangles selected we want to calculate the intersection points of these two edges with the slicing plane. If edge one results in intersection A and two intersects
in intersection B, then the **edge A-B** is the 2D slice representation of this triangle. These unordered edges are then connected using the _BriocheSlice.Connect_Edges()_
resulting in a 2D perimiter slice at each Z value of the slicing plane. At this stage, a slice only consists of a **perimiter** which are created with the 
_BriocheSlice.Generate_Shells()_ function.

The first stage resulted in a list of **BriocheSlice** where only the perimiter was set. This ordered list of slices is used as input for the **BriocheModel**. The constructor
of the BriocheModel implements the **second and third stages** of the slicing pipeline. The second stage being the _BriocheModel.Upwards_Pass()_ and the third stage being the 
_BriocheModel.Downwards_Pass()_. In the upwards pass the the **floors** are being generating using the _BriocheSlice.Generate_Floor()_ function. The downwards pass creates the 
**roofs, support and infill** using the respective functions: _BriocheSlice.Generate_Roof()_, _BriocheSlice.Generate_Support()_ and _BriocheSlice.Generate_Infill()_ functions.

**Intersting functions are**:
- TheSlicer.Slice_Model()
- TheSlicer.Slice_Plane()
- BriocheSlice.Connect_Edges()
- BriocheSlice.Generate_Shells()
- BriocheModel.Upwards_Pass()
- BriocheSlice.Generate_Floor()
- BriocheModel.Downwards_Pass()
- BriocheSlice.Generate_Roof()
- BriocheSlice.Generate_Support()
- BriocheSlice.Generate_Infill()

## Gcode/
The gcode direcotry contains all the code that is associated with the generation of the gcode.
This is where our sliced model (briochemodel) enters and the gcode to print this model is saved to a file.
The gcode gets saved in a file in the **downloads** folder.

The main function for the gcode generation is called: _TheCodeGenerator.Generate()_ 
which calls _TheCodeGenerator.AddLayerCode()_ for each layer of the model.

**Intersting functions are**:
- TheCodeGenerator.Generate()
- TheCodeGenerator.AddLayerCode()

## Rendering/
This directory host some helper classes and function to create the 2D slice visualisation.

## Resources/
The resources directory holds the start and end code to append to the generate gcode, it also hosts the collection of used test models.