using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace AdminWeb.Services.Export;

public sealed class ExcelWorkbookBuilder
{
    private readonly List<ExcelSheet> _sheets = [];

    public ExcelWorkbookBuilder AddSheet(string name, IEnumerable<IEnumerable<object?>> rows)
    {
        var safeName = ToSafeSheetName(name, _sheets.Count + 1);
        _sheets.Add(new ExcelSheet(safeName, rows.Select(row => row.ToList()).ToList()));
        return this;
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteEntry(archive, "docProps/app.xml", BuildAppProperties());
            WriteEntry(archive, "docProps/core.xml", BuildCoreProperties());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteEntry(archive, "xl/styles.xml", BuildStyles());

            for (var index = 0; index < _sheets.Count; index++)
            {
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", BuildWorksheet(_sheets[index]));
            }
        }

        return stream.ToArray();
    }

    private string BuildContentTypes()
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        builder.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
        builder.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");

        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<Override PartName=\"/xl/worksheets/sheet{index + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }

        builder.Append("</Types>");
        return builder.ToString();
    }

    private static string BuildRootRelationships()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
""";
    }

    private string BuildWorkbook()
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        builder.Append("<sheets>");

        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<sheet name=\"{Escape(_sheets[index].Name)}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\"/>");
        }

        builder.Append("</sheets></workbook>");
        return builder.ToString();
    }

    private string BuildWorkbookRelationships()
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");

        for (var index = 0; index < _sheets.Count; index++)
        {
            builder.Append($"<Relationship Id=\"rId{index + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{index + 1}.xml\"/>");
        }

        builder.Append($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        builder.Append("</Relationships>");
        return builder.ToString();
    }

    private static string BuildStyles()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="1"><font><sz val="11"/><color theme="1"/><name val="Calibri"/><family val="2"/></font></fonts>
  <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>
  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
""";
    }

    private string BuildWorksheet(ExcelSheet sheet)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        builder.Append("<sheetData>");

        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 1;
            var row = sheet.Rows[rowIndex];
            builder.Append($"<row r=\"{rowNumber}\">");

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                builder.Append(BuildCell(row[columnIndex], columnIndex, rowNumber));
            }

            builder.Append("</row>");
        }

        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static string BuildCell(object? value, int columnIndex, int rowNumber)
    {
        var reference = GetCellReference(columnIndex, rowNumber);
        if (value == null)
            return $"<c r=\"{reference}\"/>";

        if (IsNumeric(value))
        {
            var number = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
            return $"<c r=\"{reference}\"><v>{number}</v></c>";
        }

        var text = value is DateTime dateTime
            ? dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : value.ToString() ?? "";

        return $"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>";
    }

    private static bool IsNumeric(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private static string BuildAppProperties()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>VERSA AdminWeb</Application>
</Properties>
""";
    }

    private static string BuildCoreProperties()
    {
        var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:creator>VERSA AdminWeb</dc:creator>
  <cp:lastModifiedBy>VERSA AdminWeb</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">{created}</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">{created}</dcterms:modified>
</cp:coreProperties>
""";
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string GetCellReference(int columnIndex, int rowNumber)
    {
        return GetColumnName(columnIndex) + rowNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetColumnName(int columnIndex)
    {
        var dividend = columnIndex + 1;
        var columnName = new StringBuilder();

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName.Insert(0, (char)('A' + modulo));
            dividend = (dividend - modulo) / 26;
        }

        return columnName.ToString();
    }

    private static string ToSafeSheetName(string name, int index)
    {
        var cleaned = string.Join(" ", (name ?? "Sheet").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Replace(':', ' ')
            .Replace('\\', ' ')
            .Replace('/', ' ')
            .Replace('?', ' ')
            .Replace('*', ' ')
            .Replace('[', ' ')
            .Replace(']', ' ')
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = $"Sheet {index}";

        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string Escape(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private sealed record ExcelSheet(string Name, List<List<object?>> Rows);
}
