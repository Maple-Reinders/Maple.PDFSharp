// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.IO;
using FluentAssertions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Maple.PDFsharp.ProblemPDF.Tests
{
    /// <summary>
    /// Regression tests for the Maple patch that ignores invalid xref stream entries.
    /// The most recent revision of an object must take precedence (ISO 32000-1 7.5.6), so an invalid newest entry
    /// must make the object unavailable instead of resurrecting the superseded object from an older revision.
    /// </summary>
    public class IncrementalUpdateTests
    {
        [Theory]
        [InlineData(OlderRevision.XRefTable)]
        [InlineData(OlderRevision.XRefStream)]
        [InlineData(OlderRevision.ObjectStream)]
        public void Single_revision_loads_marker(OlderRevision older)
        {
            using var document = Open(older, NewerRevision.None);

            GetMarker(document).Should().Be("(Old)");
        }

        [Theory]
        [InlineData(OlderRevision.XRefTable)]
        [InlineData(OlderRevision.XRefStream)]
        [InlineData(OlderRevision.ObjectStream)]
        public void Valid_newer_entry_takes_precedence(OlderRevision older)
        {
            using var document = Open(older, NewerRevision.ValidEntry);

            GetMarker(document).Should().Be("(New)");
        }

        [Theory]
        [InlineData(OlderRevision.XRefTable)]
        [InlineData(OlderRevision.XRefStream)]
        [InlineData(OlderRevision.ObjectStream)]
        public void Invalid_newer_entry_does_not_resurrect_older_object(OlderRevision older)
        {
            using var document = Open(older, NewerRevision.InvalidEntry);

            document.PageCount.Should().Be(1);
            document.Internals.GetObject(new PdfObjectID(TwoRevisionPdf.MarkerObjectNumber)).Should().BeNull();
            GetMarker(document).Should().BeNull("the superseded object from revision 1 must not be loaded");
        }

        static PdfDocument Open(OlderRevision older, NewerRevision newer)
            => PdfReader.Open(new MemoryStream(TwoRevisionPdf.Create(older, newer)), PdfDocumentOpenMode.Import);

        /// <summary>
        /// Gets the /Marker value of any loaded object, or null if no object has a /Marker entry.
        /// </summary>
        static string? GetMarker(PdfDocument document)
        {
            var markers = document.Internals.GetAllObjects()
                .OfType<PdfDictionary>()
                .Where(dict => dict.Elements.ContainsKey("/Marker"))
                .Select(dict => dict.Elements["/Marker"]!.ToString())
                .ToList();
            markers.Should().HaveCountLessThan(2);
            return markers.SingleOrDefault();
        }
    }
}
