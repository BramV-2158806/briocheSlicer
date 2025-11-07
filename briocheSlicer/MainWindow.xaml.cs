using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using briocheSlicer.Rendering;
using briocheSlicer.Slicing;
using briocheSlicer.Workers;
using HelixToolkit.Wpf;
using Microsoft.Win32;

namespace briocheSlicer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TheSlicer slicer;
        private Rect3D modelBounds;
        private Model3DGroup? pureModel;

        public MainWindow()
        {
            InitializeComponent();
            slicer = new TheSlicer();

            if (SliceCanvas != null)
                SliceCanvas.SizeChanged += (_, __) => RedrawCurrentSlice();
        }

        /// <summary>
        /// On openSTL button click.
        /// Loads the .stl file selected and displays the object
        /// in the view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenStl_Click(object sender, RoutedEventArgs e)
        {
            // Create dialog window
            var dlg = new OpenFileDialog
            {
                Filter = "STL files (*.stl)|*.stl",
                Title = "Select an STL file"
            };

            // Open window en parse input
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    Model3DGroup group = Show_Model_And_Slice_Plane(dlg.FileName);

                    SliceHeightSlider.IsEnabled = true;
                    UpdateSliceHeightText(SliceHeightSlider.Value);

                    RedrawCurrentSlice();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load STL:\n{ex.Message}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Handles file drag and drop into the helix view.
        /// Only accepts .stl files.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void View_Drop(object sender, DragEventArgs e)
        {
            // Dit file get dropt?
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Is it an stl file?
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && files[0].EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
                {
                    Model3DGroup group = Show_Model_And_Slice_Plane(files[0]);

                    SliceHeightSlider.IsEnabled = true;
                    UpdateSliceHeightText(SliceHeightSlider.Value);

                    RedrawCurrentSlice();
                }
            }
        }

        /// <summary>
        /// Adds the model to the scene and a slicing plane.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>
        /// The group of models, 
        /// now containting the model itself and a slicing plane
        /// </returns>
        private Model3DGroup Show_Model_And_Slice_Plane(string filename)
        {
            // Load the model and add it to the scene.
            Model3DGroup group = Add_Model_To_Scene(filename);

            // Create the slicing plane and att it to the scene.
            GeometryModel3D slicingPlane = slicer.Create_Slicing_plane(group.Bounds);

            // Enable the slice button
            SliceButton.IsEnabled = true;

            group.Children.Add(slicingPlane);
            return group;
        }

        /// <summary>
        /// Adds the model to the scene and returns the modelgroup added to the scene.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>The model group set as scene content.</returns>
        private Model3DGroup Add_Model_To_Scene(string filename)
        {
            // Read stl file
            var reader = new StLReader();
            pureModel = reader.Read(filename);

            // Set the xaml variable to the model we just loaded.
            var group = new Model3DGroup();
            group.Children.Add(pureModel);

            // Add model to the scene
            scene.Content = group;
            View.ZoomExtents();

            // Save modelbounnds for slicing plane max and min y
            modelBounds = group.Bounds;

            UpdateSliceHeightText(SliceHeightSlider.Value);

            return group;
        }

        /// <summary>
        /// Handles slider value changes to update the slicing plane position.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SliceHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (slicer == null || modelBounds.IsEmpty)
                return;

            // Convert slider value (0-100) to Y position within model bounds
            double normalizedValue = e.NewValue / 100.0; // 0.0 to 1.0
            double zPostion = modelBounds.Z + (normalizedValue * modelBounds.SizeZ);

            // Update slicing plane position
            slicer.Get_Slicing_Plane().Update_Slicing_Plane_Z(zPostion);

            // Update text display
            UpdateSliceHeightText(e.NewValue);
        }

        /// <summary>
        /// Updates the slice height text display.
        /// </summary>
        /// <param name="sliderValue"></param>
        private void UpdateSliceHeightText(double sliderValue)
        {
            if (SliceHeightText != null)
            {
                SliceHeightText.Text = $"{sliderValue:F0}%";
            }
        }

        /// <summary>
        /// Handles the Slice button click event.
        /// Validates inputs and initiates the slicing process.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Slice_Click(object sender, RoutedEventArgs e)
        {
            // Validate layer height input
            if (double.TryParse(LayerHeightTextBox.Text, out double layerHeight) && layerHeight <= 0)
            {
                MessageBox.Show("Please enter a valid layer height (must be greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate nozzle diameter input
            if (double.TryParse(NozzleDiameterTextBox.Text, out double nozzleDiameter) && nozzleDiameter <= 0)
            {
                MessageBox.Show("Please enter a valid nozzle diameter (must be greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Slice entire object with given parameters
            slicer.Set_Layer_Height(layerHeight);
            slicer.Set_Nozzle_Diameter(nozzleDiameter);
            // Use the pure model loaded earlier


            // Enable the slice height slider
            SliceHeightSlider.IsEnabled = true;

            RedrawCurrentSlice();
        }

        /// <summary>
        /// Re-renders the 2D slice on SliceCanvas for the current slider Z.
        /// </summary>
        private void RedrawCurrentSlice()
        {
            if (scene?.Content == null || pureModel == null || SliceCanvas == null) return;
            if (modelBounds.IsEmpty) return;

            double z = modelBounds.Z + (SliceHeightSlider.Value / 100.0) * modelBounds.SizeZ;

            // 1) Build intersections (triangles -> segments). Make sure your Calculate_intersection clamps Z to 'z'.
            var triangles = BriocheTriangle.Get_Triangles_From_Model(pureModel);
            var slice = slicer.Slice_Plane(triangles, z);
            var polys = slice.getPolygons();

            Debug.WriteLine($"[Slice] polygons={polys.Count} (loops).");

            if (polys.Count > 0)
            {
                // Show polygons
                SliceRenderer.DrawSliceAutoFit(SliceCanvas, polys);
            }
            else
            {
                // Fallback: if no loops, show message
                SliceCanvas.Children.Clear();
                var tb = new TextBlock
                {
                    Text = "No closed loops found at this height",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(8),
                    FontSize = 12
                };
                SliceCanvas.Children.Add(tb);
            }
        }

        /// <summary>
        /// Temporary demo edges (20x20 square) so you can see the 2D panel working immediately.
        /// Replace with your real triangle-plane intersections.
        /// </summary>
        private static List<BriocheEdge> BuildDemoEdges(double z)
        {
            return new List<BriocheEdge>
            {
                new(new Point3D(0,0,z),   new Point3D(20,0,z)),
                new(new Point3D(20,0,z),  new Point3D(20,20,z)),
                new(new Point3D(20,20,z), new Point3D(0,20,z)),
                new(new Point3D(0,20,z),  new Point3D(0,0,z)),
            };
        }
    }
}