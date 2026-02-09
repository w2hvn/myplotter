using UglyToad.PdfPig;

namespace PdfToGCode.Pdf
{
    public class PdfLoader
    {
        public PdfDocument Load(string filepath)
        {
            return PdfDocument.Open(filepath);
        }
    }
}
