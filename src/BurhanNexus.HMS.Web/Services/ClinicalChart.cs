using System.Globalization;
using System.Net;
using System.Text;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Ipd;
using Microsoft.AspNetCore.Html;

namespace BurhanNexus.HMS.Web.Services;

/// <summary>
/// Clinical observation charts for printed forms (IPD and OT) – drawn only from the recorded values. Missing values leave gaps
/// (never treated as normal); dashed lines appear only for limits configured by the hospital (with their protocol source).
/// </summary>
public static class ClinicalChart
{
    public static IHtmlContent Render(IReadOnlyList<RecordView> records, ClinicalFormField field, TimeZoneInfo zone, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        var pts = records.Where(r => r.Number(field.Key) is not null).OrderBy(r => r.Record.RecordedUtc)
            .Select(r => (T: r.Record.RecordedUtc, V: (double)r.Number(field.Key)!.Value)).ToList();
        const double w = 680, h = 120, left = 46, right = 8, top = 14, bottom = 22;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg class=\"ipd-chart\" viewBox=\"0 0 {w} {h}\" role=\"img\" aria-label=\"{Esc(field.Label)}\" data-chart=\"{Esc(field.Key)}\">");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"4\" y=\"11\" class=\"c-title\">{Esc(field.Label)}{(field.Unit is null ? "" : " (" + Esc(field.Unit) + ")")}</text>");
        if (pts.Count == 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{w / 2}\" y=\"{h / 2}\" text-anchor=\"middle\" class=\"c-empty\">Not recorded</text></svg>");
            return new HtmlString(sb.ToString());
        }
        var t0 = fromUtc ?? pts[0].T; var t1 = toUtc ?? pts[^1].T;
        if (t1 <= t0) t1 = t0.AddHours(1);
        var vals = pts.Select(p => p.V).ToList();
        if (field.AlertLow is decimal lo) vals.Add((double)lo);
        if (field.AlertHigh is decimal hi) vals.Add((double)hi);
        double vmin = vals.Min(), vmax = vals.Max();
        if (vmax - vmin < 1e-9) { vmin -= 1; vmax += 1; }
        var pad = (vmax - vmin) * 0.1; vmin -= pad; vmax += pad;
        double X(DateTime t) => left + (t - t0).TotalSeconds / (t1 - t0).TotalSeconds * (w - left - right);
        double Y(double v) => top + (vmax - v) / (vmax - vmin) * (h - top - bottom);
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{left}\" y=\"{top}\" width=\"{w - left - right}\" height=\"{h - top - bottom}\" class=\"c-frame\"/>");
        for (var i = 0; i <= 4; i++)
        {
            var v = vmin + (vmax - vmin) * i / 4;
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{left}\" x2=\"{w - right}\" y1=\"{Y(v):0.#}\" y2=\"{Y(v):0.#}\" class=\"c-grid\"/><text x=\"{left - 3}\" y=\"{Y(v) + 3:0.#}\" text-anchor=\"end\" class=\"c-axis\">{v:0.#}</text>");
        }
        void Limit(decimal? l, string label)
        {
            if (l is not decimal d) return;
            var y = Y((double)d);
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{left}\" x2=\"{w - right}\" y1=\"{y:0.#}\" y2=\"{y:0.#}\" class=\"c-limit\"/><text x=\"{w - right - 2}\" y=\"{y - 2:0.#}\" text-anchor=\"end\" class=\"c-limit-t\">{label} {d:0.##}</text>");
        }
        Limit(field.AlertLow, "low");
        Limit(field.AlertHigh, "high");
        sb.Append("<polyline class=\"c-line\" points=\"");
        foreach (var p in pts) sb.Append(CultureInfo.InvariantCulture, $"{X(p.T):0.#},{Y(p.V):0.#} ");
        sb.Append("\"/>");
        foreach (var p in pts)
        {
            var flag = field.AlertLow is decimal l && p.V < (double)l || field.AlertHigh is decimal hh && p.V > (double)hh;
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{X(p.T):0.#}\" cy=\"{Y(p.V):0.#}\" r=\"2.6\" class=\"{(flag ? "c-dot flag" : "c-dot")}\"><title>{Local(p.T):dd MMM HH:mm} – {p.V:0.##}</title></circle>");
        }
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{left}\" y=\"{h - 6}\" class=\"c-axis\">{Esc(Local(t0).ToString("dd MMM HH:mm", CultureInfo.InvariantCulture))}</text>");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{w - right}\" y=\"{h - 6}\" text-anchor=\"end\" class=\"c-axis\">{Esc(Local(t1).ToString("dd MMM HH:mm", CultureInfo.InvariantCulture))}</text>");
        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}
