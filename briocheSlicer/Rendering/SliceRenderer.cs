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
        public static void DrawSliceAutoFit( Canvas canvas, PathsD? slice, PathsD? infill = null, PathsD? floor = null, PathsD? roof = null, PathsD? support = null, double strokePx = 1.5, double marginPercent = 0.06)
        {
            canvas.Children.Clear();
            Console.WriteLine($"Support path count: {support?.Count ?? 0}");
            if ((slice == null || slice.Count == 0) && 
                (infill == null || infill.Count == 0) && 
                (floor == null || floor.Count == 0) && 
                (roof == null || roof.Count == 0) &&
                (support == null || support.Count == 0)) 
                return;

            var bounds = ComputeBoundsFromPathsD(slice, infill, floor, roof, support);

            DrawUsingTransform(canvas, strokePx, marginPercent, bounds, draw =>
            {
                // Draw slice paths (perimeters/shells)
                if (slice != null && slice.Count > 0) 
                {
                    var stroke = new SolidColorBrush(Color.FromRgb(0x22, 0xCC, 0x88)); // green
                    stroke.Freeze();

                    foreach (var path in slice)
                    {
                        if (path == null || path.Count < 2) continue;

                        var fig = new PathFigure
                        {
                            IsClosed = true,
                            IsFilled = false,
                            StartPoint = draw(new Point3D(path[0].x, path[0].y, 0))
                        };
                        var seg = new PolyLineSegment();
                        for (int i = 1; i < path.Count; i++)
                        {
                            seg.Points.Add(draw(new Point3D(path[i].x, path[i].y, 0)));
                        }
                        fig.Segments.Add(seg);

                        var geo = new PathGeometry();
                        geo.Figures.Add(fig);

                        canvas.Children.Add(new Path
                        {
                            Data = geo,
                            Stroke = stroke,
                            StrokeThickness = strokePx,
                            StrokeLineJoin = PenLineJoin.Round
                        });
                    }
                }

                // Draw floor 
                var floorRoofStroke = new SolidColorBrush(Color.FromRgb(0xAA, 0x66, 0xCC));
                floorRoofStroke.Freeze();

                if (floor != null && floor.Count > 0)
                {
                    foreach (var path in floor)
                    {
                        if (path == null || path.Count < 2) continue;

                        var fig = new PathFigure
                        {
                            IsClosed = true,
                            IsFilled = false,
                            StartPoint = draw(new Point3D(path[0].x, path[0].y, 0))
                        };
                        var seg = new PolyLineSegment();
                        for (int i = 1; i < path.Count; i++)
                        {
                            seg.Points.Add(draw(new Point3D(path[i].x, path[i].y, 0)));
                        }
                        fig.Segments.Add(seg);

                        var geo = new PathGeometry();
                        geo.Figures.Add(fig);

                        canvas.Children.Add(new Path
                        {
                            Data = geo,
                            Stroke = floorRoofStroke,
                            StrokeThickness = strokePx * 0.8,
                            StrokeLineJoin = PenLineJoin.Round
                        });
                    }
                }

                // Draw roof
                if (roof != null && roof.Count > 0)
                {
                    foreach (var path in roof)
                    {
                        if (path == null || path.Count < 2) continue;

                        var fig = new PathFigure
                        {
                            IsClosed = true,
                            IsFilled = false,
                            StartPoint = draw(new Point3D(path[0].x, path[0].y, 0))
                        };
                        var seg = new PolyLineSegment();
                        for (int i = 1; i < path.Count; i++)
                        {
                            seg.Points.Add(draw(new Point3D(path[i].x, path[i].y, 0)));
                        }
                        fig.Segments.Add(seg);

                        var geo = new PathGeometry();
                        geo.Figures.Add(fig);

                        canvas.Children.Add(new Path
                        {
                            Data = geo,
                            Stroke = floorRoofStroke,
                            StrokeThickness = strokePx * 0.8,
                            StrokeLineJoin = PenLineJoin.Round
                        });
                    }
                }

                // Draw infill lines
                if (infill != null && infill.Count > 0)
                {
                    var infillStroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x90, 0x40)); // orange
                    infillStroke.Freeze();

                    foreach (var path in infill)
                    {
                        if (path == null || path.Count < 2) continue;

                        // Draw as line segments
                        for (int i = 0; i < path.Count - 1; i++)
                        {
                            var p1 = draw(new Point3D(path[i].x, path[i].y, 0));
                            var p2 = draw(new Point3D(path[i + 1].x, path[i + 1].y, 0));

                            canvas.Children.Add(new Line
                            {
                                X1 = p1.X,
                                Y1 = p1.Y,
                                X2 = p2.X,
                                Y2 = p2.Y,
                                Stroke = infillStroke,
                                StrokeThickness = strokePx * 0.7 // Slightly thinner for infill
                            });
                        }
                    }
                }
                // --- Draw support region ---
                if (support != null && support.Count > 0)
                {
                    var supportStroke = new SolidColorBrush(Color.FromRgb(90, 160, 255)); // soft blue
                    supportStroke.Freeze();

                    foreach (var path in support)
                    {
                        if (path == null || path.Count < 2) continue;

                        bool isClosed = (path[0].x == path[path.Count - 1].x &&
                                         path[0].y == path[path.Count - 1].y);

                        if (isClosed)
                        {
                            // polygon outline (support roof/platform)
                            var fig = new PathFigure
                            {
                                IsClosed = true,
                                IsFilled = false,
                                StartPoint = draw(new Point3D(path[0].x, path[0].y, 0))
                            };
                            var seg = new PolyLineSegment();
                            for (int i = 1; i < path.Count; i++)
                                seg.Points.Add(draw(new Point3D(path[i].x, path[i].y, 0)));

                            fig.Segments.Add(seg);

                            var geo = new PathGeometry();
                            geo.Figures.Add(fig);

                            canvas.Children.Add(new Path
                            {
                                Data = geo,
                                Stroke = supportStroke,
                                StrokeThickness = strokePx,
                                StrokeLineJoin = PenLineJoin.Round
                            });
                        }
                        else
                        {
                            // support pillars (open lines)
                            for (int i = 0; i < path.Count - 1; i++)
                            {
                                var p1 = draw(new Point3D(path[i].x, path[i].y, 0));
                                var p2 = draw(new Point3D(path[i + 1].x, path[i + 1].y, 0));

                                canvas.Children.Add(new Line
                                {
                                    X1 = p1.X,
                                    Y1 = p1.Y,
                                    X2 = p2.X,
                                    Y2 = p2.Y,
                                    Stroke = supportStroke,
                                    StrokeThickness = strokePx * 0.8,
                                });
                            }
                        }
                    }
                }

            });
        }

        // -------- shared helpers --------
        private static void DrawUsingTransform(
            Canvas canvas, double strokePx, double marginPercent, Rect bounds,
            Action<Func<Point3D, Point>> drawContent)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            double W = canvas.ActualWidth > 0 ? canvas.ActualWidth : Math.Max(200, canvas.Width);
            double H = canvas.ActualHeight > 0 ? canvas.ActualHeight : Math.Max(200, canvas.Height);

            double pad = Math.Max(12, Math.Min(W, H) * marginPercent);
            double sx = (W - 2 * pad) / bounds.Width;
            double sy = (H - 2 * pad) / bounds.Height;
            double s = Math.Max(1e-9, Math.Min(sx, sy));

            // Map model-space Point3D -> canvas Point, flipped horizontally to match viewer
            Point Xf(Point3D p)
            {
                // Flip X by mapping from bounds.Right down to bounds.Left
                double x = pad + s * (bounds.Right - p.X);
                // Keep Y flipped so that model Y up maps to canvas upward
                double y = H - pad - s * (p.Y - bounds.Y);
                return new Point(x, y);
            }

            drawContent(Xf);

            // subtle border
            canvas.Children.Add(new Rectangle
            {
                Width = W - 1,
                Height = H - 1,
                Stroke = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                StrokeThickness = 1
            });
        }

        // Add this overload to handle 4 arguments for ComputeBoundsFromPathsD
        private static Rect ComputeBoundsFromPathsD(
    PathsD? slice, PathsD? infill, PathsD? floor, PathsD? roof, PathsD? support)
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
