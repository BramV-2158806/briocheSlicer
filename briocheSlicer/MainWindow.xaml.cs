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
using briocheSlicer.Gcode;
using System.IO;

namespace briocheSlicer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TheSlicer slicer;
        private TheCodeGenerator codeGenerator;

        private Rect3D modelBounds;
        private Model3DGroup? pureModel;
        private BriocheModel? briocheModel;

        private GcodeSettings gcodeSettings;

        public MainWindow()
        {
            InitializeComponent();
            slicer = new TheSlicer();
            codeGenerator = new TheCodeGenerator();
            gcodeSettings = new GcodeSettings();

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

            // Redraw the current slice
            RedrawCurrentSlice();
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
        /// Validates that only numeric input is allowed for the shells textbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShellsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !int.TryParse(e.Text, out _);
        }

        /// <summary>
        /// Validates that the number of shells is at least 1.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShellsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // If the text is empty or cannot be parsed as an integer
                if (string.IsNullOrWhiteSpace(textBox.Text) || !int.TryParse(textBox.Text, out int shells))
                {
                    textBox.Background = Brushes.LightPink;
                    return;
                }

                // If the number is less than 1, show validation error
                if (shells < 1)
                {
                    textBox.Background = Brushes.LightPink;
                }
                else
                {
                    textBox.Background = Brushes.White;
                }
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

            // Validate number of shells input
            if (!int.TryParse(ShellsTextBox.Text, out int shells) || shells < 1)
            {
                MessageBox.Show("Please enter a valid number of shells (must be at least 1).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate model is loaded
            if (pureModel == null)
            {
                MessageBox.Show("No model loaded. Please load an STL file first.",
                                "No Model", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Slice entire object with given parameters
            slicer.Set_Layer_Height(layerHeight);
            slicer.Set_Nozzle_Diameter(nozzleDiameter);
            gcodeSettings.LayerHeight = layerHeight;
            gcodeSettings.NozzleDiameter = nozzleDiameter;
            gcodeSettings.NumberShells = shells;


            // Enable the slice height slider
            SliceHeightSlider.IsEnabled = true;
            PrintButton.IsEnabled = true;

            // Slice the model
            briocheModel = slicer.Slice_Model(pureModel, gcodeSettings);

            RedrawCurrentSlice();
        }

        /// <summary>
        /// Handles the Print button click event.
        /// Sends G-code to the printer.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (briocheModel == null)
            {
                MessageBox.Show("No slice available to print. Please slice the model first.",
                                "No Slice", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get the Downloads folder path
            string downloadsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "Downloads"
            );
            
            // Create filename with timestamp to avoid overwriting
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"brioche_slicer_output_{timestamp}.gcode";
            string fullPath = System.IO.Path.Combine(downloadsPath, filename);

            // Generate G-code
            codeGenerator.Generate(briocheModel, gcodeSettings, fullPath);

            // Add positive affordance
            MessageBox.Show($"G-code saved successfully to:\n{fullPath}",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Re-renders the 2D slice on SliceCanvas for the current slider Z.
        /// </summary>
        private void RedrawCurrentSlice()
        {
            if (scene?.Content == null || pureModel == null || SliceCanvas == null) return;
            if (modelBounds.IsEmpty || briocheModel == null || slicer == null) return;

            // Calculate the current layer index
            double z = modelBounds.Z + (SliceHeightSlider.Value / 100.0) * modelBounds.SizeZ;
            double? layerHeight = slicer.Get_Layer_Height()!;
            int layerIndex = (int)Math.Floor((z - modelBounds.Z) / layerHeight.Value);

            // Get the slice of the current layer
            var currentSlice = briocheModel.GetSlice(layerIndex); 
            var slice = currentSlice.GetSlice();
            var infill = currentSlice.GetInfill();

            // Draw the 2D slice
            if (slice != null && slice.Count > 0)
            {
                // Show slice paths and infill
                SliceRenderer.DrawSliceAutoFit(SliceCanvas, slice, infill);
            }
            else
            {
                // Negative affordance: if no paths, show message
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