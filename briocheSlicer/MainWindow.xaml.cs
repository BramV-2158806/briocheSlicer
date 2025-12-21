using briocheSlicer.Gcode;
using briocheSlicer.Rendering;
using briocheSlicer.Slicing;
using briocheSlicer.Slicing.TreeSupport;
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
        public enum InfillPattern
        {
            Rectilinear,
            Horizontal,
            Honeycomb
        }

        private TheSlicer slicer;
        private TheCodeGenerator codeGenerator;

        private Rect3D modelBounds;
        private Model3DGroup? pureModel;
        private BriocheModel? briocheModel;

        private GcodeSettings gcodeSettings;

        private BuildPlate? buildPlate;

        private bool HasSliced { get; set; }

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
        /// Validates that only numeric input is allowed for the integer textboxes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IntTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !int.TryParse(e.Text, out _);
        }

        /// <summary>
        /// Validates that the number of roofs is at least 0.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IntTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // If the text is empty or cannot be parsed as an integer
                if (string.IsNullOrWhiteSpace(textBox.Text) || !int.TryParse(textBox.Text, out int value))
                {
                    textBox.Background = Brushes.LightPink;
                    return;
                }

                // If the number is less than 0, show validation error
                if (value <= 0)
                {
                    textBox.Background = Brushes.LightPink;
                }
                else
                {
                    textBox.Background = Brushes.White;
                }
            }
        }

        private void HandleInfillPattern()
        {
            RadioButton rb = InfillPatternPanel.Children
                .OfType<RadioButton>()
                .FirstOrDefault(r => r.IsChecked == true)!;

            if (rb.Name == "HorizontalInfillButton")
            {
                gcodeSettings.InfillType = GcodeSettings.InfillPattern.Horizontal;
            }
            else if (rb.Name == "RectilinearInfillButton")
            {
                gcodeSettings.InfillType = GcodeSettings.InfillPattern.Rectilinear;
            }
            else if (rb.Name == "HoneycombInfillButton")
            {
                gcodeSettings.InfillType = GcodeSettings.InfillPattern.Honeycomb;
            }
        }

        private void SupportCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (TreeSupportCheckBox == null || SupportPatternPanel == null)
                return;

            TreeSupportCheckBox.IsEnabled = true;
            SupportPatternPanel.IsEnabled = true;
        }

        private void HandleTreeSupportCheckbox_Check(object sender, RoutedEventArgs e)
        {
            if (HasSliced)
            {
                MessageBox.Show("Rebuild the application for Tree Support after slicing previous models!",
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                TreeSupportCheckBox.IsChecked = false;
                return;
            }
            TreeSupportCheckBox.IsChecked = true;

            // Disable support pattern selection when tree support is enabled
            SupportPatternPanel.IsEnabled = false;
        }

        private void SupportCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (TreeSupportCheckBox == null || SupportPatternPanel == null)
                return;

            TreeSupportCheckBox.IsChecked = false;
            TreeSupportCheckBox.IsEnabled = false;
            SupportPatternPanel.IsEnabled = false;
        }

        private void HandleSupportPattern()
        {
            if (SupportCheckBox.IsChecked != true)
            {
                gcodeSettings.SupportEnabled = false;
                return;
            }

            RadioButton rb = SupportPatternPanel.Children
                .OfType<RadioButton>()
                .FirstOrDefault(r => r.IsChecked == true)!;

            if (rb.Name == "RectilinearSupportButton")
            {
                gcodeSettings.SupportType = GcodeSettings.InfillPattern.Rectilinear;
            }
            else if (rb.Name == "HoneycombSupportButton")
            {
                gcodeSettings.SupportType = GcodeSettings.InfillPattern.Honeycomb;
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
                MessageBox.Show("Please enter a valid LAYER HEIGHT (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate nozzle diameter input
            string nozzleText = Normalize(NozzleDiameterTextBox.Text);
            if (!double.TryParse(nozzleText, NumberStyles.Float, CultureInfo.InvariantCulture, out double nozzleDiameter) || nozzleDiameter <= 0)
            {
                MessageBox.Show("Please enter a valid NOZZLE DIAMETER (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate extrusion retraction input
            string retracionText = Normalize(ExtrusionRetractionTextBox.Text);
            if (!double.TryParse(retracionText, NumberStyles.Float, CultureInfo.InvariantCulture, out double retractionLength) || retractionLength <= 0)
            {
                MessageBox.Show("Please enter a valid EXTRUSION RETRACTION LENGTH (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate number of shells input
            if (!int.TryParse(ShellsTextBox.Text, out int shells) || shells <= 0)
            {
                MessageBox.Show("Please enter a valid NUMBER OF SHELLS (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate number of floors input
            if (!int.TryParse(FloorAmountTextBox.Text, out int floors) || floors <= 0)
            {
                MessageBox.Show("Please enter a valid NUMBER OF FLOORS (must be at least 1).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate number of roofs input
            if (!int.TryParse(RoofAmountTextBox.Text, out int roofs) || roofs <= 0)
            {
                MessageBox.Show("Please enter a valid NUMBER OF ROOFS (must be at least 1).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate infill sparsity input
            string infillSparsityText = Normalize(InfillSparsityTextBox.Text);
            if (!double.TryParse(infillSparsityText, NumberStyles.Float, CultureInfo.InvariantCulture, out double infillSparsity) || infillSparsity <= 0)
            {
                MessageBox.Show("Please enter a valid INFILL SPARSITY (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate support sparsity input
            string supportSparsityText = Normalize(SupportSparsityTextBox.Text);
            if (!double.TryParse(supportSparsityText, NumberStyles.Float, CultureInfo.InvariantCulture, out double supportSparsity) || supportSparsity <= 0)
            {
                MessageBox.Show("Please enter a valid SUPPORT SPARSITY (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Speed settings
            // Validate shell speed input
            string shellSpeedText = Normalize(ShellSpeedTextBox.Text);
            if (!double.TryParse(shellSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double shellspeed) || shellspeed <= 0)
            {
                MessageBox.Show("Please enter a valid SHELL SPEED (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate roof speed input
            string roofSpeedText = Normalize(RoofSpeedTextBox.Text);
            if (!double.TryParse(roofSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double roofspeed) || roofspeed <= 0)
            {
                MessageBox.Show("Please enter a valid ROOF SPEED (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate floor speed input
            string floorSpeedText = Normalize(FloorSpeedTextBox.Text);
            if (!double.TryParse(floorSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double floorspeed) || floorspeed <= 0)
            {
                MessageBox.Show("Please enter a valid FLOOR SPEED (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate infill speed input
            string infillSpeedText = Normalize(InfillSpeedTextBox.Text);
            if (!double.TryParse(infillSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double infillspeed) || infillspeed <= 0)
            {
                MessageBox.Show("Please enter a valid INFILL SPEED (must be a number greater than 0).",
                                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate support sparsity input
            string supportSpeedText = Normalize(SupportSpeedTextBox.Text);
            if (!double.TryParse(supportSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double supportspeed) || supportspeed <= 0)
            {
                MessageBox.Show("Please enter a valid SUPPORT SPEED (must be a number greater than 0).",
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
            gcodeSettings.LayerHeight = layerHeight;
            gcodeSettings.NozzleDiameter = nozzleDiameter;
            gcodeSettings.ExtrusionRetractLength = retractionLength;
            gcodeSettings.NumberShells = shells;
            gcodeSettings.NumberFloors = floors;
            gcodeSettings.NumberRoofs = roofs;
            gcodeSettings.InfillSparsity = infillSparsity * nozzleDiameter;
            gcodeSettings.SupportSparsity = supportSparsity * nozzleDiameter;
            gcodeSettings.ShellSpeed = shellspeed;
            gcodeSettings.RoofSpeed = roofspeed;
            gcodeSettings.FloorSpeed = floorspeed;
            gcodeSettings.InfillSpeed = infillspeed;
            gcodeSettings.SupportSpeed = supportspeed;
            gcodeSettings.SupportEnabled = SupportCheckBox.IsChecked == true;
            HandleInfillPattern();
            HandleSupportPattern();

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
            HasSliced = true;

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

        // Handles input for decimal textboxes, allowing only digits and one decimal separator
        private void DecimalTextBox_PreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb)
                return;

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

                int selStart = tb.SelectionStart;
                int selLen = tb.SelectionLength;
                string newText = tb.Text.Remove(selStart, selLen).Insert(selStart, decimalSep);
                tb.Text = newText;
                tb.SelectionStart = selStart + decimalSep.Length;
                e.Handled = true;
                return;
            }

            if (!char.IsDigit(input, 0))
            {
                e.Handled = true;
            }
        }

        // Handles validation for decimal textboxes, '.' and ',' usage
        private void DecimalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            // Empty input → invalid
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Background = Brushes.LightPink;
                return;
            }

            // Normalize decimal separator for parsing
            string text = textBox.Text
                .Replace(
                    CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                textBox.Background = Brushes.LightPink;
                return;
            }

            if (value <= 0)
            {
                textBox.Background = Brushes.LightPink;
                return;
            }
            else
            {
                textBox.Background = Brushes.White;
            }
        }

        // Handles trailing '.' or ',' by removing it on focus loss
        private void DecimalTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb)
                return;

            string text = tb.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(text))
                return;

            text = text.Replace(',', '.');

            // Remove trailing decimal separator
            if (text.EndsWith("."))
                text = text[..^1];

            tb.Text = text;
        }
    }
}