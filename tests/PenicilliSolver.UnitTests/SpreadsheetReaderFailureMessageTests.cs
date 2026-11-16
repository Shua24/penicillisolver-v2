using System.Globalization;
using System.IO.Compression;
using System.Text;

using penicillisolver_v2.Domain.Constants;
using penicillisolver_v2.Services;

namespace PenicilliSolver.UnitTests;

/// <summary>
/// Covers the failure messages the spreadsheet readers produce when the
/// underlying file cannot be read at all.
/// </summary>
/// <remarks>
/// <para>
/// These four messages used to end with the framework's own exception text, so
/// an Indonesian sentence finished in English: "Berkas 'x' bukan tabel Excel
/// yang valid: The archive entry was compressed using an unsupported
/// compression method." The exception now travels on
/// <see cref="SpreadsheetImportResult.FailureCause"/> for the log, and the
/// message is the resource value and nothing else.
/// </para>
/// <para>
/// The assertions compare against the resource file rather than a literal
/// sentence, and they check the cause is still carried: dropping the tail is
/// only safe while the detail survives somewhere.
/// </para>
/// </remarks>
public sealed class SpreadsheetReaderFailureMessageTests
{
    /// <summary>
    /// Verifies that a file that does not exist is reported with the resource
    /// sentence and keeps its exception.
    /// </summary>
    [Fact]
    public void Csv_A_missing_file_reports_the_resource_sentence_and_keeps_the_cause()
    {
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-missing-{Guid.NewGuid():N}.csv");

        SpreadsheetImportResult result = ReadUnderIndonesian(
            () => CsvSpreadsheetReader.Read(missingPath, TestLocalizerFactory.Localizer));

        AssertFailureComesFrom("Service_FileCouldNotBeRead", missingPath, result);
    }

    /// <summary>
    /// Verifies that a malformed csv line is reported with the resource sentence
    /// and keeps its exception.
    /// </summary>
    [Fact]
    public void Csv_A_malformed_line_reports_the_resource_sentence_and_keeps_the_cause()
    {
        // An unterminated quoted field is what TextFieldParser refuses.
        string temporaryPath = WriteTemporaryFile(
            "malformed.csv",
            "Organism,Organism one\n\"unterminated,1\n");

        try
        {
            SpreadsheetImportResult result = ReadUnderIndonesian(
                () => CsvSpreadsheetReader.Read(temporaryPath, TestLocalizerFactory.Localizer));

            AssertFailureComesFrom("Service_CsvMalformedLine", temporaryPath, result);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    /// <summary>
    /// Verifies that a file that is not a workbook is reported with the resource
    /// sentence and keeps its exception.
    /// </summary>
    [Fact]
    public void Xlsx_A_file_that_is_not_a_workbook_reports_the_resource_sentence_and_keeps_the_cause()
    {
        string temporaryPath = WriteTemporaryFile(
            "not-a-workbook.xlsx",
            "this is not a zip archive, let alone a workbook");

        try
        {
            SpreadsheetImportResult result = ReadUnderIndonesian(
                () => XlsxSpreadsheetReader.Read(temporaryPath, TestLocalizerFactory.Localizer));

            AssertFailureComesFrom("Service_NotValidXlsxWorkbook", temporaryPath, result);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    /// <summary>
    /// Verifies that a zip archive which is not an OPC package at all is reported
    /// as an invalid workbook instead of escaping.
    /// </summary>
    /// <remarks>
    /// ClosedXML throws <c>NullReferenceException</c> for this shape. Before the
    /// catch was added it escaped <c>AcceptUploadAsync</c>, past the upload page's
    /// <c>catch (InvalidOperationException)</c>, and took the Blazor circuit down,
    /// so the reader saw a generic error bar instead of a sentence.
    /// </remarks>
    [Fact]
    public void Xlsx_A_zip_that_is_not_a_package_reports_the_resource_sentence_and_keeps_the_cause()
    {
        string temporaryPath = WriteTemporaryBytes(
            "plain-zip.xlsx",
            CreateZip(("notes.txt", "just a text file in a zip")));

        try
        {
            SpreadsheetImportResult result = ReadUnderIndonesian(
                () => XlsxSpreadsheetReader.Read(temporaryPath, TestLocalizerFactory.Localizer));

            AssertFailureComesFrom("Service_NotValidXlsxWorkbook", temporaryPath, result);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    /// <summary>
    /// Verifies that an OPC package declaring spreadsheet content types but
    /// carrying no workbook part is reported as an invalid workbook instead of
    /// escaping.
    /// </summary>
    /// <remarks>
    /// ClosedXML throws <c>InvalidOperationException</c> for this shape. It was
    /// caught by accident further up, by the upload page's size-limit clause, so
    /// the reader was told the file was too large — a misleading sentence for a
    /// file that was simply malformed.
    /// </remarks>
    [Fact]
    public void Xlsx_A_package_without_a_workbook_part_reports_the_resource_sentence_and_keeps_the_cause()
    {
        string temporaryPath = WriteTemporaryBytes(
            "workbook-part-missing.xlsx",
            CreateZip(
                ("[Content_Types].xml",
                    """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                    </Types>
                    """),
                ("_rels/.rels",
                    """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                    </Relationships>
                    """)));

        try
        {
            SpreadsheetImportResult result = ReadUnderIndonesian(
                () => XlsxSpreadsheetReader.Read(temporaryPath, TestLocalizerFactory.Localizer));

            AssertFailureComesFrom("Service_NotValidXlsxWorkbook", temporaryPath, result);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    /// <summary>
    /// Verifies that none of the reader failure messages still has a slot for
    /// the framework's own exception text.
    /// </summary>
    /// <remarks>
    /// This covers all four keys in both languages, including
    /// <c>Service_FileCouldNotBeOpened</c>, whose exception is a permission
    /// denial and therefore awkward to induce without depending on the test
    /// running as a non-root user. A key that regains a <c>{1}</c> placeholder
    /// fails here whatever the platform.
    /// </remarks>
    [Fact]
    public void Every_reader_failure_message_has_no_technical_placeholder()
    {
        string[] resourceKeys =
        [
            "Service_FileCouldNotBeRead",
            "Service_FileCouldNotBeOpened",
            "Service_CsvMalformedLine",
            "Service_NotValidXlsxWorkbook",
        ];

        string[] resourceFiles =
        [
            TestResourceFiles.NeutralFileName,
            TestResourceFiles.IndonesianFileName,
        ];

        List<string> offenders = new();

        foreach (string resourceFileName in resourceFiles)
        {
            IReadOnlyDictionary<string, string> resources =
                TestResourceFiles.LoadValues(resourceFileName);

            foreach (string resourceKey in resourceKeys)
            {
                bool found = resources.TryGetValue(resourceKey, out string? value);

                Assert.True(found, $"{resourceFileName} must hold a value for {resourceKey}.");

                string message = value ?? string.Empty;

                if (!message.Contains("{0}", StringComparison.Ordinal))
                {
                    offenders.Add($"{resourceFileName}:{resourceKey} no longer names the file");
                }

                if (message.Contains("{1}", StringComparison.Ordinal))
                {
                    offenders.Add($"{resourceFileName}:{resourceKey} still appends technical detail");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Reader failure messages that leak or lost their file name: "
                + string.Join(", ", offenders));
    }

    /// <summary>
    /// Runs a read with the UI culture set to Indonesian, which is what the
    /// localizer resolves the message under.
    /// </summary>
    /// <param name="read">The read to perform.</param>
    /// <returns>The import result.</returns>
    private static SpreadsheetImportResult ReadUnderIndonesian(Func<SpreadsheetImportResult> read)
    {
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(SupportedLanguages.Indonesian);

            SpreadsheetImportResult result = read();

            return result;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// Asserts that a failure carries exactly the resource sentence for the
    /// named key, and that the exception behind it is still available.
    /// </summary>
    /// <param name="resourceKey">The key the message must come from.</param>
    /// <param name="fileName">The file name the sentence interpolates.</param>
    /// <param name="result">The import result to inspect.</param>
    private static void AssertFailureComesFrom(
        string resourceKey,
        string fileName,
        SpreadsheetImportResult result)
    {
        IReadOnlyDictionary<string, string> resources =
            TestResourceFiles.LoadValues(TestResourceFiles.IndonesianFileName);

        string expectedMessage = string.Format(
            new CultureInfo(SupportedLanguages.Indonesian),
            resources[resourceKey],
            fileName);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Document);

        // Equality, not containment: the point is that nothing follows the
        // sentence, so a framework message cannot be appended to it.
        Assert.Equal(expectedMessage, result.ErrorMessage);

        Assert.NotNull(result.FailureCause);
        Assert.NotEmpty(result.FailureCause!.Message);
    }

    /// <summary>Writes a temporary file with the supplied content.</summary>
    private static string WriteTemporaryFile(string fileName, string content)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-reader-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directoryPath);

        string filePath = Path.Combine(directoryPath, fileName);

        File.WriteAllText(filePath, content);

        return filePath;
    }

    /// <summary>Writes a temporary file with the supplied bytes.</summary>
    private static string WriteTemporaryBytes(string fileName, byte[] content)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"penicillisolver-reader-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directoryPath);

        string filePath = Path.Combine(directoryPath, fileName);

        File.WriteAllBytes(filePath, content);

        return filePath;
    }

    /// <summary>
    /// Builds a zip archive from the supplied entries, so a test can hand the
    /// reader a package shape that is not a workbook.
    /// </summary>
    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream buffer = new();

        using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);

                using Stream entryStream = entry.Open();
                using StreamWriter writer = new(entryStream, new UTF8Encoding(false));

                writer.Write(content.Trim());
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Deletes a temporary file and the directory it was written into.</summary>
    private static void DeleteTemporaryFile(string filePath)
    {
        File.Delete(filePath);

        string? directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath is not null && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath);
        }
    }
}
