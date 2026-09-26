// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.IO;
using System.Text;

namespace Maple.PDFsharp.ProblemPDF.Tests
{
    /// <summary>
    /// How revision 1 stores its cross-reference information and the marker object.
    /// </summary>
    public enum OlderRevision
    {
        /// <summary>Classic xref table, marker object 5 is an uncompressed object.</summary>
        XRefTable,
        /// <summary>Cross-reference stream, marker object 5 is an uncompressed object.</summary>
        XRefStream,
        /// <summary>Cross-reference stream, marker object 5 is compressed in object stream 4.</summary>
        ObjectStream
    }

    /// <summary>
    /// What the incremental update (revision 2) does with marker object 5.
    /// </summary>
    public enum NewerRevision
    {
        /// <summary>There is no revision 2.</summary>
        None,
        /// <summary>Revision 2 redefines object 5 with a valid xref stream entry.</summary>
        ValidEntry,
        /// <summary>Revision 2 has an xref stream entry for object 5 that points into a zeroed region instead of an object.</summary>
        InvalidEntry
    }

    /// <summary>
    /// Builds a one-page PDF whose catalog references marker object 5, optionally followed by an
    /// incremental update with an xref stream. Revision 1 defines '5 0 obj &lt;&lt;/Marker (Old)&gt;&gt;',
    /// a valid revision 2 defines '&lt;&lt;/Marker (New)&gt;&gt;'.
    /// </summary>
    static class TwoRevisionPdf
    {
        public const int MarkerObjectNumber = 5;

        public static byte[] Create(OlderRevision older, NewerRevision newer)
        {
            var pdf = new PdfBytes();
            var offsets = new Dictionary<int, long>();

            // Revision 1.
            pdf.Write("%PDF-1.7\n%âãÏÓ\n");
            offsets[1] = pdf.WriteObject(1, "<</Type/Catalog/Pages 2 0 R/MapleMarker 5 0 R>>");
            offsets[2] = pdf.WriteObject(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>");
            offsets[3] = pdf.WriteObject(3, "<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>");
            if (older == OlderRevision.ObjectStream)
            {
                const string header = "5 0 ";
                const string body = "<</Marker (Old)>>";
                offsets[4] = pdf.WriteStreamObject(4, $"/Type/ObjStm/N 1/First {header.Length}", header + body);
            }
            else
            {
                offsets[5] = pdf.WriteObject(5, "<</Marker (Old)>>");
            }

            long olderXRef;
            if (older == OlderRevision.XRefTable)
            {
                olderXRef = pdf.Position;
                var xref = new StringBuilder("xref\n0 6\n0000000000 65535 f\r\n");
                for (int number = 1; number <= 5; number++)
                    xref.Append(offsets.TryGetValue(number, out var offset) ? $"{offset:D10} 00000 n\r\n" : "0000000000 00000 f\r\n");
                pdf.Write(xref.ToString());
                pdf.Write("trailer\n<</Size 6/Root 1 0 R>>\n");
            }
            else
            {
                olderXRef = pdf.Position;
                var entries = new List<(int Type, long Field2, int Field3)>
                {
                    (0, 0, 65535),
                    (1, offsets[1], 0),
                    (1, offsets[2], 0),
                    (1, offsets[3], 0),
                    older == OlderRevision.ObjectStream ? (1, offsets[4], 0) : (0, 0, 0),
                    older == OlderRevision.ObjectStream ? (2, 4, 0) : (1, offsets[5], 0),
                    (1, olderXRef, 0)
                };
                pdf.WriteStreamObject(6, "/Type/XRef/Size 7/Index[0 7]/W[1 4 2]/Root 1 0 R", XRefStreamData(entries));
            }

            var startXRef = olderXRef;
            if (newer != NewerRevision.None)
            {
                // Revision 2: an incremental update with an xref stream (object 7) that updates object 5.
                long markerOffset;
                if (newer == NewerRevision.ValidEntry)
                {
                    markerOffset = pdf.WriteObject(5, "<</Marker (New)>>");
                }
                else
                {
                    // Like a broken incremental update: the entry points to zeroed bytes followed by content stream operators.
                    markerOffset = pdf.Position;
                    pdf.Write(new string('\0', 32) + " Tf 1 0 0 1 2 7.2 Tm\n");
                }
                long newerXRef = pdf.Position;
                var entries = new List<(int Type, long Field2, int Field3)>
                {
                    (1, markerOffset, 0),
                    (1, newerXRef, 0)
                };
                pdf.WriteStreamObject(7, $"/Type/XRef/Size 8/Index[5 1 7 1]/W[1 4 2]/Root 1 0 R/Prev {olderXRef}", XRefStreamData(entries));
                startXRef = newerXRef;
            }

            pdf.Write($"startxref\n{startXRef}\n%%EOF\n");
            return pdf.ToArray();
        }

        static byte[] XRefStreamData(List<(int Type, long Field2, int Field3)> entries)
        {
            // W [1 4 2], big-endian.
            var data = new List<byte>();
            foreach (var (type, field2, field3) in entries)
            {
                data.Add((byte)type);
                data.Add((byte)(field2 >> 24));
                data.Add((byte)(field2 >> 16));
                data.Add((byte)(field2 >> 8));
                data.Add((byte)field2);
                data.Add((byte)(field3 >> 8));
                data.Add((byte)field3);
            }
            return data.ToArray();
        }

        /// <summary>
        /// Writes PDF syntax as Latin-1 bytes and tracks object offsets.
        /// </summary>
        sealed class PdfBytes
        {
            public long Position => _stream.Position;

            public void Write(string text) => Write(Latin1.GetBytes(text));

            public void Write(byte[] bytes) => _stream.Write(bytes, 0, bytes.Length);

            public long WriteObject(int number, string body)
            {
                var offset = Position;
                Write($"{number} 0 obj\n{body}\nendobj\n");
                return offset;
            }

            public long WriteStreamObject(int number, string dictionaryEntries, string content)
                => WriteStreamObject(number, dictionaryEntries, Latin1.GetBytes(content));

            public long WriteStreamObject(int number, string dictionaryEntries, byte[] content)
            {
                var offset = Position;
                Write($"{number} 0 obj\n<<{dictionaryEntries}/Length {content.Length}>>\nstream\n");
                Write(content);
                Write("\nendstream\nendobj\n");
                return offset;
            }

            public byte[] ToArray() => _stream.ToArray();

            readonly MemoryStream _stream = new();

            // Latin-1 maps chars 0-255 one to one to bytes. Encoding.Latin1 does not exist in .NET Framework.
            static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
        }
    }
}
