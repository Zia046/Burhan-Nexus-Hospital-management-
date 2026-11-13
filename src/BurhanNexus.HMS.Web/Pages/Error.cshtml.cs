using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BurhanNexus.HMS.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string Title { get; private set; } = "Something went wrong";
    public string Message { get; private set; } = "An unexpected error occurred. The error has been logged on the server.";

    public void OnGet(int? code) => Set(code);
    public void OnPost(int? code) => Set(code);

    private void Set(int? code)
    {
        switch (code)
        {
            case 404:
                Title = "Page not found";
                Message = "The page you requested does not exist.";
                break;
            case 403:
                Title = "Access denied";
                Message = "You do not have permission to open this page. Contact your administrator if you need access.";
                break;
            case 429:
                Title = "Too many attempts";
                Message = "Too many requests from this computer. Please wait a minute and try again.";
                break;
        }
    }
}
