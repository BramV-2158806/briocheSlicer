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
        // -------- PATHSD (auto-fit) --------
        public static void DrawSliceAutoFit(Canvas canvas, PathsD? slice, PathsD? infill = null,
         double strokePx = 1.5, double marginPercent = 0.06)
        {
            canvas.Children.Clear();
            if ((slice == null || slice.Count == 0) && (infill == null || infill.Count == 0)) return;

            var bounds = ComputeBoundsFromPathsD(slice, infill);

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
                      });
        }

        // -------- RAW SEGMENTS (auto-fit) --------
        public static void DrawSegmentsAutoFit(Canvas canvas, IReadOnlyList<BriocheEdge> segments,
                                               double strokePx = 1.5, double marginPercent = 0.06,
                                               bool drawEndpoints = true)
        {
            canvas.Children.Clear();
            if (segments == null || segments.Count == 0) return;

            var bounds = ComputeBoundsFromSegments(segments);
            DrawUsingTransform(canvas, strokePx, marginPercent, bounds, draw =>
            {
                var stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x90, 0x40)); // orange
                stroke.Freeze();

                foreach (var e in segments)
                {
                    var l = new Line
                    {
                        X1 = draw(e.Start).X,
                        Y1 = draw(e.Start).Y,
                        X2 = draw(e.End).X,
                        Y2 = draw(e.End).Y,
                        Stroke = stroke,
                        StrokeThickness = strokePx
                    };
                    canvas.Children.Add(l);

                    if (drawEndpoints)
                    {
                        var p1 = draw(e.Start); var p2 = draw(e.End);
                        canvas.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = Brushes.Red, Margin = new Thickness(p1.X - 2, p1.Y - 2, 0, 0) });
                        canvas.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = Brushes.Cyan, Margin = new Thickness(p2.X - 2, p2.Y - 2, 0, 0) });
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

            Point Xf(Point3D p)
            {
                double x = pad + s * (p.X - bounds.X);
                double y = H - pad - s * (p.Y - bounds.Y); // flip Y up
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

        public static Rect ComputeBoundsFromPolygons(IReadOnlyList<List<BriocheEdge>> polygons)
        {
            var pts = new List<Point3D>();
            foreach (var loop in polygons)
                foreach (var e in loop) { pts.Add(e.Start); pts.Add(e.End); }
            return ComputeBoundsFromPoints(pts);
        }

        public static Rect ComputeBoundsFromSegments(IReadOnlyList<BriocheEdge> segments)
        {
            var pts = new List<Point3D>();
            foreach (var e in segments) { pts.Add(e.Start); pts.Add(e.End); }
            return ComputeBoundsFromPoints(pts);
        }

        private static Rect ComputeBoundsFromPoints(IReadOnlyList<Point3D> pts)
        {
            if (pts == null || pts.Count == 0) return Rect.Empty;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (var p in pts)
            {
                if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y;
            }
            double w = Math.Max(1e-9, maxX - minX);
            double h = Math.Max(1e-9, maxY - minY);
            return new Rect(minX, minY, w, h);
        }

        private static Rect ComputeBoundsFromPathsD(PathsD? slice, PathsD? infill)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;

            bool hasPoints = false;

            if (slice != null)
            {
                foreach (var path in slice)
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

            if (infill != null)
            {
                foreach (var path in infill)
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

            if (!hasPoints) return Rect.Empty;

            double w = Math.Max(1e-9, maxX - minX);
            double h = Math.Max(1e-9, maxY - minY);
            return new Rect(minX, minY, w, h);
        }
    }
}
