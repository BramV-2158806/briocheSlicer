using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using briocheSlicer.Slicing;
using Clipper2Lib;

namespace briocheSlicer.Rendering
{
    internal static class SliceRenderer
    {

        // Function to transform PointD from model space to Point in Canvas space
        // Ai was used to help make this functionality 
        private static Func<PointD, Point> CreateTransform(Canvas canvas, Rect bounds, double marginPercent)
        {
            double width = canvas.ActualWidth > 0 ? canvas.ActualWidth : Math.Max(200, canvas.Width);
            double height = canvas.ActualHeight > 0 ? canvas.ActualHeight : Math.Max(200, canvas.Height);

            double pad = Math.Max(12, Math.Min(width, height) * marginPercent);
            double sx = (width - 2 * pad) / bounds.Width;
            double sy = (height - 2 * pad) / bounds.Height;
            double s = Math.Max(1e-9, Math.Min(sx, sy));

            return p =>
            {
                double x = pad + s * (bounds.Right - p.x);
                double y = height - pad - s * (p.y - bounds.Y);
                return new Point(x, y);
            };
        }

        // Function to draw a closed path on the canvas using polygons
        // This is for Shells, Floors, Roofs
        private static void DrawClosedPath(Canvas canvas, PathD path, Func<PointD, Point> tx, Brush stroke, double thickness)
        {
            if (path.Count < 2) return;

            var fig = new PathFigure
            {
                IsClosed = true,
                IsFilled = false,
                StartPoint = tx(path[0])
            };

            var seg = new PolyLineSegment();
            for (int i = 1; i < path.Count; i++)
                seg.Points.Add(tx(path[i]));

            fig.Segments.Add(seg);

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            canvas.Children.Add(new Path
            {
                Data = geo,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round
            });
        }

        // Function to draw an open path on the canvas, draw lines instead of using PathGeometry
        // This is for Infill and Support
        private static void DrawOpenPath(Canvas canvas, PathD path, Func<PointD, Point> tx, Brush stroke, double thickness)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                var p1 = tx(path[i]);
                var p2 = tx(path[i + 1]);

                canvas.Children.Add(new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = stroke,
                    StrokeThickness = thickness
                });
            }
        }

        // Function to check if ther is anything to draw
        private static bool AllEmpty(params PathsD?[] groups)
        {
            return groups.All(g => g == null || g.Count == 0);
        }

        // Function to draw a group of paths like all shells, all infill, all floors, all roofs
        private static void DrawGroup(Canvas canvas, PathsD? paths, Func<PointD, Point> tx, Brush stroke, double thickness, bool closed)
        {
            if (paths == null) return;

            foreach (var path in paths)
            {
                if (path == null || path.Count < 2) continue;

                if (closed)
                    DrawClosedPath(canvas, path, tx, stroke, thickness);
                else
                    DrawOpenPath(canvas, path, tx, stroke, thickness);
            }
        }

        // Main function to draw a slice on a canvas with auto fit so that the entire slice is always able to fit into the canvas
        public static void DrawSliceAutoFit(Canvas canvas, PathsD? shells, PathsD? infill = null, PathsD? floor = null, PathsD? roof = null, PathsD? support = null, double strokePx = 1.5, double marginPercent = 0.06)
        {
            canvas.Children.Clear();

            if (AllEmpty(shells, infill, floor, roof, support))
                return;

            Rect bounds = ComputeBoundsFromPathsD(shells, infill, floor, roof, support);
            if (bounds.IsEmpty) return;

            var tx = CreateTransform(canvas, bounds, marginPercent);

            DrawGroup(canvas, shells, tx, Brushes.MediumSeaGreen, strokePx, closed: true);
            DrawGroup(canvas, floor, tx, Brushes.MediumPurple, strokePx * 0.8, closed: true);
            DrawGroup(canvas, roof, tx, Brushes.MediumPurple, strokePx * 0.8, closed: true);
            DrawGroup(canvas, infill, tx, Brushes.Orange, strokePx * 0.7, closed: false);
            DrawGroup(canvas, support, tx, Brushes.DeepSkyBlue, strokePx, closed: false);
        }

        // Goes over all the paths in the slice and gather the bound values
        private static Rect ComputeBoundsFromPathsD(PathsD? slice, PathsD? infill, PathsD? floor, PathsD? roof, PathsD? support)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            bool hasPoints = false;

            void Update(PathsD? paths)
            {
                if (paths == null) return;
                foreach (var path in paths)
                {
                    foreach (var pt in path)
                    {
                        hasPoints = true;
                        if (pt.x < minX) minX = pt.x;
                        if (pt.y < minY) minY = pt.y;
                        if (pt.x > maxX) maxX = pt.x;
                        if (pt.y > maxY) maxY = pt.y;
                    }
                }
            }

            Update(slice);
            Update(infill);
            Update(floor);
            Update(roof);
            Update(support);

            if (!hasPoints) return Rect.Empty;

            return new Rect(minX, minY,
                            Math.Max(1e-9, maxX - minX),
                            Math.Max(1e-9, maxY - minY));
        }
    }
}
