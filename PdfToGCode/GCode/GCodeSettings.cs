namespace PdfToGCode.GCode
{
    public class GCodeSettings
    {
        public int FeedRate { get; set; } = 1000;
        public double ZUp { get; set; } = 5.0;
        public double ZDown { get; set; } = -1.0;

        // Scale factor from PDF units (Points) to Machine units (e.g., mm).
        // 1 Point = 1/72 inch = 0.352778 mm.
        public double Scale { get; set; } = 0.352778;
    }
}
