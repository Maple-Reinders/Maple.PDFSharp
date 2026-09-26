// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.IO;
using FluentAssertions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Maple.PDFsharp.ProblemPDF.Tests
{
    /// <summary>
    /// Runs every PDF from the Maple.iText.ProblemPDFs repository through PDFsharp.
    /// </summary>
    public class ProblemPdfTests
    {
        public static IEnumerable<object[]> ProblemPdfs =>
            ProblemPdfLocator.GetFiles().Select(f => new object[] { Path.GetFileName(f) });

        [Fact]
        public void ProblemPdfFolder_contains_pdfs()
        {
            ProblemPdfLocator.FindFolder().Should().NotBeNull(ProblemPdfLocator.NotFoundMessage);
            ProblemPdfLocator.GetFiles().Should().NotBeEmpty("the problem PDF folder must contain PDF files");
        }

        [Theory]
        [MemberData(nameof(ProblemPdfs))]
        public void Can_open_and_count_pages(string fileName)
        {
            using var document = PdfReader.Open(ProblemPdfLocator.GetPath(fileName), PdfDocumentOpenMode.Import);

            document.PageCount.Should().BePositive();
        }

        [Theory]
        [MemberData(nameof(ProblemPdfs))]
        public void Can_merge_with_known_good_pdf(string fileName)
        {
            using var goodDocument = PdfReader.Open(new MemoryStream(KnownGoodPdf.Create()), PdfDocumentOpenMode.Import);
            using var problemDocument = PdfReader.Open(ProblemPdfLocator.GetPath(fileName), PdfDocumentOpenMode.Import);
            var expectedPageCount = goodDocument.PageCount + problemDocument.PageCount;

            using var mergedStream = new MemoryStream();
            using (var mergedDocument = new PdfDocument())
            {
                foreach (var page in goodDocument.Pages)
                    mergedDocument.AddPage(page);
                foreach (var page in problemDocument.Pages)
                    mergedDocument.AddPage(page);
                mergedDocument.Save(mergedStream, false);
            }

            mergedStream.Position = 0;
            using var reopenedDocument = PdfReader.Open(mergedStream, PdfDocumentOpenMode.Import);
            goodDocument.PageCount.Should().Be(KnownGoodPdf.PageCount);
            reopenedDocument.PageCount.Should().Be(expectedPageCount);
        }
    }
}
