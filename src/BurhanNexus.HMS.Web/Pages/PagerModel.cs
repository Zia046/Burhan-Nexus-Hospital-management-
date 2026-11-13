using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace BurhanNexus.HMS.Web.Pages;

public sealed class PagerModel
{
    public PagerModel(HttpRequest request, int page, int totalPages, int total)
    {
        Request = request;
        Page = page;
        TotalPages = totalPages;
        Total = total;
    }

    private HttpRequest Request { get; }
    public int Page { get; }
    public int TotalPages { get; }
    public int Total { get; }

    public string Url(int page)
    {
        var query = QueryHelpers.ParseQuery(Request.QueryString.Value)
            .ToDictionary(k => k.Key, v => (string?)v.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        query["p"] = page.ToString();
        var qb = new QueryBuilder(query.Where(kv => !string.IsNullOrEmpty(kv.Value)).Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!)));
        return Request.Path + qb.ToQueryString().ToString();
    }
}
