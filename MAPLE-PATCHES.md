# Maple patches to PDFsharp

This fork adds a few small changes so PDFsharp tolerates malformed PDFs we see in production. Each one logs a warning and carries on instead of throwing. Each patched spot in the code is marked with a `// MAPLE:` comment.

The PDFs that need these patches live in the private [Maple.iText.ProblemPDFs](https://github.com/Maple-Reinders/Maple.iText.ProblemPDFs) repo. The tests in `src/foundation/src/PDFsharp/tests/Maple.PDFsharp.ProblemPDF.Tests` open each one and merge it with a known-good PDF.

## After syncing with upstream

1. `git grep -n "MAPLE:" -- src`: check every patch survived the merge. Re-apply any that were lost, using the table below.
2. Run `dotnet test src/foundation/src/PDFsharp/tests/Maple.PDFsharp.ProblemPDF.Tests -c Release`. The tests find the PDFs in `../Maple.iText.ProblemPDFs`, `./ProblemPDFs`, or the folder named by `MAPLE_PROBLEM_PDFS`.
3. Run `dotnet test src/foundation/src/PDFsharp/tests/PdfSharp.Tests -c Release -f net8.0` to check the patches don't break upstream behaviour.
4. Bump `MaplePackageVersion` in `src/foundation/nuget/src/PDFsharp.NuGet/PDFsharp.NuGet.csproj`.

## Patches

| # | Location | What is tolerated | PDFs that need it |
|---|----------|-------------------|-------------------|
| 1 | `Pdf/PdfDictionary.cs`, `DictionaryElements.GetRectangle` | A page box array with junk after the 4 coordinates (e.g. `/MediaBox [0 0 612 792 78 0 R 77 0 R]`) uses the first 4 numbers. Any other invalid rectangle becomes an empty rectangle instead of throwing. | Problem PDF 5, Problem PDF 6 |
| 2 | `Pdf.IO/Lexer.cs`, `TryScanEndStreamSymbol` | Extra white-space (e.g. 20+ line feeds) between the stream content and `endstream`. The stream keeps its correct `/Length`. | Problem PDF 8, FF - Multiplex Estimate Guide ERROR |
| 3 | `Pdf.IO/Parser.cs`, `ReadDictionary` | A non-name in key position (e.g. from the malformed number `/ItalicAngle -17.-32768`) is skipped and the parser resyncs on the next name. A trailing key without a value is ignored. | Problem PDF 7 |
| 4 | `Pdf.IO/Parser.cs`, `ReadXRefStream` (plus tombstone checks in the xref table loop and `ReadAllObjectStreamsAndTheirReferences`) | An xref stream entry that doesn't point to an object (e.g. into a zeroed region left by a broken incremental update) makes that object unavailable (null). The object number is tombstoned so an older revision can't resurrect a superseded copy through `/Prev` or an older object stream, because the most recent revision takes precedence (ISO 32000-1 7.5.6). A valid entry in a newer revision still wins. Covered by `IncrementalUpdateTests`. | Problem PDF 4 |
| 5 | `Pdf/PdfObject.cs`, `FixUpObject` + `Pdf.Advanced/PdfImportedObjectTable.cs`, `TryGetValue` | When importing pages, a reference to an object that doesn't exist in the source document becomes `null`, as the PDF spec says (7.3.10). | FF - Foundation Plan, Problem PDF 4, Problem PDF 8 |

The earlier Maple fork of PDFsharp 1.50 (`justincowling/PDFsharp`, branch `feature/handle-broken-pdfs`) used the same approach. See commits `8145e7b` (invalid rectangle) and `11b9f02` (ignore bad xref entries).
