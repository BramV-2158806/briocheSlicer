using briocheSlicer.Gcode;
using briocheSlicer.Rendering;
using briocheSlicer.Slicing;
using briocheSlicer.Slicing.TreeSupport;
using briocheSlicer.Workers;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

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

        private BuildPlate? buildPlate;

        public MainWindow()
        {
            InitializeComponent();
            slicer = new TheSlicer();
            codeGenerator = new TheCodeGenerator();
            gcodeSettings = new GcodeSettings();

            if (SliceCanvas != null)
                SliceCanvas.SizeChanged += (_, __) => RedrawCurrentSlice();

            // Add full-screen toggle on F11
            this.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.F11)
                {
                    ToggleFullScreen();
                    e.Handled = true;
                }
            };
        }

        private void ToggleFullScreen()
        {
            if (this.WindowStyle == WindowStyle.SingleBorderWindow)
            {
                // Enter full-screen
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                // Exit full-screen
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
            }
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
                    Model3DGroup group = ModelView(dlg.FileName);

                    SliceHeightSlider.IsEnabled = true;

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
                    Model3DGroup group = ModelView(files[0]);

                    SliceHeightSlider.IsEnabled = true;

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
        private Model3DGroup ModelView(string filename)
        {
            // Load the model and add it to the scene.
            Model3DGroup group = Add_Model_To_Scene(filename);

            // Create or update the buildplate
            if (buildPlate == null)
            {
                buildPlate = new BuildPlate(group.Bounds, 256);
            }
            else
            {
                buildPlate.UpdatePosition(group.Bounds);
            }
            group.Children.Add(buildPlate.GetModel());

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

            // Enable rotation buttons
            RotateLeftButton.IsEnabled = true;
            RotateRightButton.IsEnabled = true;
            RotateFrontButton.IsEnabled = true;
            RotateBackButton.IsEnabled = true;

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

            int layerIndex = (int)SliceHeightSlider.Value;
            layerIndex = Math.Clamp(layerIndex, 0, briocheModel != null ? briocheModel.amount_Layers - 1 : 0);

            var slice = briocheModel?.GetSlice(layerIndex);
            double zPosition = slice!.slice_height;

            // Update slicing plane position
            slicer.Get_Slicing_Plane().Update_Slicing_Plane_Z(zPosition);

            // Redraw the current slice
            RedrawCurrentSlice();
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
        /// Validates that only numeric input is allowed for the floors textbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FloorsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !int.TryParse(e.Text, out _);
        }

        /// <summary>
        /// Validates that the number of floors is at least 0.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FloorsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // If the text is empty or cannot be parsed as an integer
                if (string.IsNullOrWhiteSpace(textBox.Text) || !int.TryParse(textBox.Text, out int floors))
                {
                    textBox.Background = Brushes.LightPink;
                    return;
                }

                // If the number is less than 0, show validation error
                if (floors < 0)
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
        /// Validates that only numeric input is allowed for the roofs textbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RoofsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !int.TryParse(e.Text, out _);
        }

        /// <summary>
        /// Validates that the number of roofs is at least 0.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RoofsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // If the text is empty or cannot be parsed as an integer
                if (string.IsNullOrWhiteSpace(textBox.Text) || !int.TryParse(textBox.Text, out int roofs))
                {
                    textBox.Background = Brushes.LightPink;
                    return;
                }

                // If the number is less than 0, show validation error
                if (roofs < 0)
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
            // Normalize decimal separators (allow ',' or '.') and parse using invariant culture.
            string Normalize(string s) => (s ?? string.Empty).Trim().Replace(',', '.');

            // Validate layer height input
            string layerText = Normalize(LayerHeightTextBox.Text);
            if (!double.TryParse(layerText, NumberStyles.Float, CultureInfo.InvariantCulture, out double layerHeight) || layerHeight <= 0)
            {
                MessageBox.Show("Please enter a valid layer height (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate nozzle diameter input
            string nozzleText = Normalize(NozzleDiameterTextBox.Text);
            if (!double.TryParse(nozzleText, NumberStyles.Float, CultureInfo.InvariantCulture, out double nozzleDiameter) || nozzleDiameter <= 0)
            {
                MessageBox.Show("Please enter a valid nozzle diameter (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate extrusion retraction input
            string retracionText = Normalize(ExtrusionRetractionTextBox.Text);
            if (!double.TryParse(retracionText, NumberStyles.Float, CultureInfo.InvariantCulture, out double retractionLength) || retractionLength <= 0)
            {
                MessageBox.Show("Please enter a valid extrusion retraction length (must be a number greater than 0).",
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

            // Validate number of floors input
            if (!int.TryParse(FloorsTextBox.Text, out int floors) || floors < 0)
            {
                MessageBox.Show("Please enter a valid number of floors (must be at least 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate number of roofs input
            if (!int.TryParse(RoofsTextBox.Text, out int roofs) || roofs < 0)
            {
                MessageBox.Show("Please enter a valid number of roofs (must be at least 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate infill sparsity input
            string infillSparsityText = Normalize(InfillSparsityTextBox.Text);
            if (!double.TryParse(infillSparsityText, NumberStyles.Float, CultureInfo.InvariantCulture, out double infillSparsity) || infillSparsity <= 0)
            {
                MessageBox.Show("Please enter a valid infill sparsity (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate support sparsity input
            string supportSparsityText = Normalize(SupportSparsityTextBox.Text);
            if (!double.TryParse(supportSparsityText, NumberStyles.Float, CultureInfo.InvariantCulture, out double supportSparsity) || supportSparsity <= 0)
            {
                MessageBox.Show("Please enter a valid support sparsity (must be a number greater than 0).",
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
            gcodeSettings.ExtrusionRetractLength = retractionLength;
            gcodeSettings.NumberShells = shells;
            gcodeSettings.NumberFloors = floors;
            gcodeSettings.NumberRoofs = roofs;
            gcodeSettings.InfillSparsity = infillSparsity * nozzleDiameter;
            gcodeSettings.SupportSparsity = supportSparsity * nozzleDiameter;

            // Enable the slice height slider
            SliceHeightSlider.IsEnabled = true;
            PrintButton.IsEnabled = true;

            // If enabled: pre processing step to generate the tree alterd model 
            gcodeSettings.TreeSupportEnabled = TreeSupportCheckBox.IsChecked == true;
            Model3DGroup displayModel;
            if (gcodeSettings.TreeSupportEnabled)
            {
                // altered brioche model that now includes the tree trunks
                TreeSupportGenerator generator = new TreeSupportGenerator();
                Model3DGroup trunkModels = generator.LetTheForrestGrow(pureModel);
                
                // Create a model with both the 
                displayModel = new Model3DGroup();
                displayModel.Children.Add(trunkModels);
                displayModel.Children.Add(pureModel);
            }
            else
            {
                displayModel = pureModel;
            }

            // Update the build plate position based on the display model bounds
            if (buildPlate == null)
            {
                buildPlate = new BuildPlate(displayModel.Bounds, 256);
            }
            else
            {
                buildPlate.UpdatePosition(displayModel.Bounds);
            }
            displayModel.Children.Add(buildPlate.GetModel());

            // Create/update the slicing plane for the display model
            GeometryModel3D slicingPlane = slicer.Create_Slicing_plane(displayModel.Bounds);
            displayModel.Children.Add(slicingPlane);

            scene.Content = displayModel;

            // Slice the model
            briocheModel = slicer.Slice_Model(displayModel, gcodeSettings);

            // Reset slice plane to middle of object to show new slice with updated settings
            SliceHeightSlider.Value = briocheModel.amount_Layers / 2;
            SliceHeightSlider.Minimum = 0;
            SliceHeightSlider.Maximum = briocheModel.amount_Layers - 1;
            SliceHeightSlider.TickFrequency = 1;
            SliceHeightSlider.IsSnapToTickEnabled = true;

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

            // Generate G-code with current offsets
            codeGenerator.Generate(briocheModel, gcodeSettings, fullPath);

            EstimatedTimeText.Text = $"Estimated Print Time: {codeGenerator.formattedEstimatedTime}";

            // Add positive affordance
            MessageBox.Show($"G-code saved successfully to:\n{fullPath}",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Rotates the model 90° left around the Y-axis.
        /// </summary>
        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            RotateModel(new Vector3D(0, 1, 0), -90);
        }

        /// <summary>
        /// Rotates the model 90° right around the Y-axis.
        /// </summary>
        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            RotateModel(new Vector3D(0, 1, 0), 90);
        }

        /// <summary>
        /// Rotates the model 90° forward around the X-axis.
        /// </summary>
        private void RotateFront_Click(object sender, RoutedEventArgs e)
        {
            RotateModel(new Vector3D(1, 0, 0), -90);
        }

        /// <summary>
        /// Rotates the model 90° backward around the X-axis.
        /// </summary>
        private void RotateBack_Click(object sender, RoutedEventArgs e)
        {
            RotateModel(new Vector3D(1, 0, 0), 90);
        }

        /// <summary>
        /// Rotates the actual puremodel geometry.
        /// This results in a rotate object that will be sliced in the same way as it is
        /// displayed on the build plate.
        /// ** DISCLAIMER AI helped write this code.
        /// </summary>
        /// <param name="rotation">The rotation object to rotate the geometry by. Center and rotation.</param>
        private void RotateModelGeometry(RotateTransform3D rotation)
        {
            if (pureModel == null) return;
            foreach (var child in pureModel.Children)
            {
                if (child is GeometryModel3D geometryModel)
                {
                    if (geometryModel.Geometry is MeshGeometry3D mesh)
                    {
                        // Transform all vertex positions
                        var transformedPositions = new Point3DCollection();
                        foreach (var position in mesh.Positions)
                        {
                            transformedPositions.Add(rotation.Transform(position));
                        }
                        mesh.Positions = transformedPositions;

                        // Transform normals if they exist
                        if (mesh.Normals != null && mesh.Normals.Count > 0)
                        {
                            var transformedNormals = new Vector3DCollection();
                            foreach (var normal in mesh.Normals)
                            {
                                var transformedNormal = rotation.Transform(normal);
                                transformedNormal.Normalize();
                                transformedNormals.Add(transformedNormal);
                            }
                            mesh.Normals = transformedNormals;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Rotates the model around a specified axis by the given angle.
        /// </summary>
        /// <param name="axis">The axis to rotate around</param>
        /// <param name="angle">The angle in degrees</param>
        private void RotateModel(Vector3D axis, double angle)
        {
            if (pureModel == null) return;

            // Get the center point of the model
            Point3D center = BuildPlate.CalculateBoundsCenter(modelBounds);

            // Create rotation transform
            var rotation = new RotateTransform3D(
                new AxisAngleRotation3D(axis, angle),
                center
            );

            // Apply the transformation to the actual geometry vertices
            RotateModelGeometry(rotation);

            // Update the model bounds after rotation
            modelBounds = pureModel.Bounds;

            // Update buildplate position based on the rotated model
            if (buildPlate != null)
            {
                buildPlate.UpdatePosition(modelBounds);
            }

            // Update slicing plane based on the rotated model bounds
            if (slicer?.Get_Slicing_Plane() != null)
            {
                var slicingPlane = slicer.Create_Slicing_plane(modelBounds);

                // Replace the old slicing plane with the new one
                var group = scene.Content as Model3DGroup;
                if (group != null && group.Children.Count > 1)
                {
                    // We know that the last model in the group is the slicing plane
                    group.Children[group.Children.Count - 1] = slicingPlane;
                }
            }

            // Invalidate any existing slice data since the model geometry has changed
            briocheModel = null;
            PrintButton.IsEnabled = false;

            View.ZoomExtents();
        }

        /// <summary>
        /// Re-renders the 2D slice on SliceCanvas for the current slider Z.
        /// </summary>
        private void RedrawCurrentSlice()
        {
            if (scene?.Content == null || pureModel == null || SliceCanvas == null) return;
            if (modelBounds.IsEmpty || briocheModel == null || slicer == null) return;

            // Calculate the current layer index
            int layerCount = briocheModel.amount_Layers - 1;
            int layerIndex = (int)Math.Round(SliceHeightSlider.Value);
            layerIndex = Math.Clamp(layerIndex, 0, layerCount);

            SlicePreviewHeader.Text = $"Slice Preview: {layerIndex} / {layerCount}";

            // Get the slice of the current layer
            var currentSlice = briocheModel.GetSlice(layerIndex);
            if (currentSlice == null) return;

            var slice = currentSlice.GetOuterLayer();
            var infill = currentSlice.GetInfill();
            var roof = currentSlice.GetRoof();
            var floor = currentSlice.GetFloor();
            var support = currentSlice.GetSupport();

            // Draw the 2D slice
            if (slice != null && slice.Count > 0)
            {
                // Show slice paths and infill
                SliceRenderer.DrawSliceAutoFit(SliceCanvas, slice, infill, floor, roof, support);
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

        private void DecimalTextBox_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb)
                return;

            // Allow only digits, group/decimal separators and single decimal separator
            var input = e.Text;
            var decimalSep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            if (input == "." || input == ",")
            {
                // If there's already a decimal separator, reject additional one
                if (tb.Text.Contains(decimalSep))
                {
                    e.Handled = true;
                    return;
                }

                // Insert the culture decimal separator at the caret (replace selection)
                int selStart = tb.SelectionStart;
                int selLen = tb.SelectionLength;
                string newText = tb.Text.Remove(selStart, selLen).Insert(selStart, decimalSep);
                tb.Text = newText;
                tb.SelectionStart = selStart + decimalSep.Length;
                e.Handled = true;
                return;
            }

            // Allow digits; block other characters
            if (!char.IsDigit(input, 0))
            {
                e.Handled = true;
            }
        }

        private void DecimalTextBox_Pasting(object? sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox tb)
                return;

            if (!e.DataObject.GetDataPresent(DataFormats.Text)) return;

            string paste = (string)e.DataObject.GetData(DataFormats.Text)!;
            var decimalSep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            // Normalize any '.' or ',' to the current culture decimal separator
            paste = paste.Replace(".", decimalSep).Replace(",", decimalSep);

            // Prevent multiple decimal separators in the resulting text
            string resulting = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength).Insert(tb.SelectionStart, paste);
            int sepCount = resulting.Count(c => c.ToString() == decimalSep);
            if (sepCount > 1)
            {
                e.CancelCommand();
                return;
            }

            // Validate the combined text parses as a number (allow empty leading/trailing)
            string forParse = resulting.Replace(decimalSep, CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
            if (!double.TryParse(forParse, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                e.CancelCommand();
                return;
            }

            // Apply normalized text and cancel default paste
            tb.Text = resulting;
            tb.SelectionStart = tb.SelectionStart + paste.Length;
            e.CancelCommand();
        }
    }
}