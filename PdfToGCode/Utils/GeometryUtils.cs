using System;
using System.Windows;

namespace PdfToGCode.Utils
{
    public static class GeometryUtils
    {
        public static double Distance(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
