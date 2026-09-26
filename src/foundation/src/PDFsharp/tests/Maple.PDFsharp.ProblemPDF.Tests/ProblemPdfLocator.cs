// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.IO;

namespace Maple.PDFsharp.ProblemPDF.Tests
{
    /// <summary>
    /// Locates the private Maple.iText.ProblemPDFs repository. The PDFs are read in place and must never be copied into this repository.
    /// </summary>
    static class ProblemPdfLocator
    {
        /// <summary>
        /// Environment variable that overrides the folder search.
        /// </summary>
        public const string EnvironmentVariable = "MAPLE_PROBLEM_PDFS";

        /// <summary>
        /// Gets the folder containing the problem PDFs, or null if none was found.
        /// Search order: MAPLE_PROBLEM_PDFS, then [repo]/ProblemPDFs (Azure pipeline checkout),
        /// then [repo]/../Maple.iText.ProblemPDFs (sibling folder on a dev machine).
        /// </summary>
        public static string? FindFolder()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (!String.IsNullOrWhiteSpace(fromEnvironment))
                return Directory.Exists(fromEnvironment) ? fromEnvironment : null;

            var repoRoot = FindRepoRoot();
            if (repoRoot == null)
                return null;

            var candidates = new[]
            {
                Path.Combine(repoRoot, "ProblemPDFs"),
                Path.Combine(repoRoot, "..", "Maple.iText.ProblemPDFs")
            };
            return candidates.Where(Directory.Exists).Select(Path.GetFullPath).FirstOrDefault();
        }

        /// <summary>
        /// Gets the full paths of all PDF files in the problem PDF folder, sorted by name.
        /// </summary>
        public static IReadOnlyList<string> GetFiles()
        {
            var folder = FindFolder();
            if (folder == null)
                return [];

            // Filter by extension manually so that upper case extensions like '.PDF' are matched on every platform.
            return Directory.EnumerateFiles(folder)
                .Where(f => String.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Gets the full path of a problem PDF by its file name.
        /// </summary>
        public static string GetPath(string fileName)
        {
            var folder = FindFolder() ?? throw new DirectoryNotFoundException(NotFoundMessage);
            return Path.Combine(folder, fileName);
        }

        public static string NotFoundMessage =>
            $"Problem PDF folder not found. Set {EnvironmentVariable}, check out Maple.iText.ProblemPDFs to [repo]/ProblemPDFs, " +
            "or clone it next to this repository as Maple.iText.ProblemPDFs.";

        static string? FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "PdfSharp.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
