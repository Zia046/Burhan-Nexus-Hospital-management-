using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace BurhanNexus.HMS.Infrastructure.Finance;

/// <summary>
/// Minimal, dependency-free Excel (.xlsx, Office Open XML) writer: one or more sheets of text / number / date cells.
/// Enough for financial exports that open in Excel, LibreOffice and Google Sheets.
/// </summary>
public sealed class XlsxWriter
{
    private readonly List<(string Name, List<object?[]> Rows)> _sheets = new();

    public XlsxWriter Sheet(string name, IEnumerable<object?[]> rows)
    {
        var clean = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        _sheets.Add((clean.Length == 0 ? $"Sheet{_sheets.Count + 1}" : clean[..Math.Min(31, clean.Length)], rows.ToList()));
        return this;
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Write(zip, "xl/workbook.xml", Workbook());
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            Write(zip, "xl/styles.xml", Styles);
            for (var i = 0; i < _sheets.Count; i++) Write(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(_sheets[i].Rows));
        }
        return ms.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    private string ContentTypes()
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        for (var i = 1; i <= _sheets.Count; i++)
            sb.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        return sb.Append("</Types>").ToString();
    }

    private string Workbook()
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");
        for (var i = 0; i < _sheets.Count; i++)
            sb.Append($"""<sheet name="{SecurityElement.Escape(_sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        return sb.Append("</sheets></workbook>").ToString();
    }

    private string WorkbookRels()
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (var i = 0; i < _sheets.Count; i++)
            sb.Append($"""<Relationship Id="rId{i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i + 1}.xml"/>""");
        sb.Append($"""<Relationship Id="rId{_sheets.Count + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        return sb.Append("</Relationships>").ToString();
    }

    // Style 0 = normal, 1 = bold header, 2 = #,##0.00, 3 = date
    private const string Styles = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="yyyy-mm-dd"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs></styleSheet>""";

    private static string SheetXml(List<object?[]> rows)
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append($"<row r=\"{r + 1}\">");
            var row = rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                var refName = Column(c) + (r + 1);
                switch (row[c])
                {
                    case null:
                        break;
                    case decimal d:
                        sb.Append($"<c r=\"{refName}\" s=\"2\"><v>{d.ToString(CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    case int or long or double:
                        sb.Append($"<c r=\"{refName}\"><v>{Convert.ToString(row[c], CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    case DateOnly date:
                        sb.Append($"<c r=\"{refName}\" s=\"3\"><v>{(date.ToDateTime(TimeOnly.MinValue) - new DateTime(1899, 12, 30)).TotalDays.ToString(CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    default:
                        var text = Convert.ToString(row[c], CultureInfo.InvariantCulture) ?? "";
                        // Inline strings are never evaluated as formulas, so user-entered text is safe.
                        sb.Append($"<c r=\"{refName}\" t=\"inlineStr\"{(r == 0 ? " s=\"1\"" : "")}><is><t xml:space=\"preserve\">{SecurityElement.Escape(text)}</t></is></c>");
                        break;
                }
            }
            sb.Append("</row>");
        }
        return sb.Append("</sheetData></worksheet>").ToString();
    }

    private static string Column(int index)
    {
        var s = "";
        for (index++; index > 0; index = (index - 1) / 26) s = (char)('A' + (index - 1) % 26) + s;
        return s;
    }
}
