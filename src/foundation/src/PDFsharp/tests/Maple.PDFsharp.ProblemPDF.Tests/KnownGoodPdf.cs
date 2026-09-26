// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Maple.PDFsharp.ProblemPDF.Tests
{
    /// <summary>
    /// Creates a simple, known good PDF in memory to merge the problem PDFs with.
    /// Only shapes are drawn, so no font resolver is required.
    /// </summary>
    static class KnownGoodPdf
    {
        public const int PageCount = 2;

        public static byte[] Create()
        {
            using var document = new PdfDocument();
            for (var idx = 0; idx < PageCount; idx++)
            {
                var page = document.AddPage();
                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawRectangle(XPens.DarkBlue, XBrushes.LightBlue, 50, 50 + idx * 100, 200, 80);
            }

            using var stream = new MemoryStream();
            document.Save(stream, false);
            return stream.ToArray();
        }
    }
}
