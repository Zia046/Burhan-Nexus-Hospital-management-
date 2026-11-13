using System.ComponentModel.DataAnnotations;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace BurhanNexus.HMS.Web.Pages.Setup;

[RequestSizeLimit(6 * 1024 * 1024)]
[EnableRateLimiting("login")]
public class IndexModel : PageModel
{
    private readonly ISetupService _setup;
    private readonly IHospitalAdminService _hospital;

    public IndexModel(ISetupService setup, IHospitalAdminService hospital)
    {
        _setup = setup;
        _hospital = hospital;
    }

    public SetupState State { get; private set; }
    public bool AccessBlocked { get; private set; }

    // Hospital
    [BindProperty, Required(ErrorMessage = "Hospital name is required."), StringLength(200)] public string HospitalName { get; set; } = string.Empty;
    [BindProperty] public HospitalType? HospitalType { get; set; }
    [BindProperty, StringLength(100)] public string? RegistrationNumber { get; set; }
    [BindProperty, Required(ErrorMessage = "Address is required."), StringLength(500)] public string Address { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "City is required."), StringLength(100)] public string City { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "Province is required."), StringLength(100)] public string Province { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "Contact number is required."), StringLength(50)] public string ContactNumber { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "Email address is required."), StringLength(200)] public string Email { get; set; } = string.Empty;
    [BindProperty, StringLength(300)] public string? Website { get; set; }
    [BindProperty] public IFormFile? Logo { get; set; }

    // Administrator
    [BindProperty, StringLength(150)] public string? AdminFullName { get; set; }
    [BindProperty, StringLength(64)] public string? AdminUsername { get; set; }
    [BindProperty, StringLength(128)] public string? AdminPassword { get; set; }
    [BindProperty, StringLength(128)] public string? AdminConfirmPassword { get; set; }
    [BindProperty, StringLength(50)] public string? AdminContactNumber { get; set; }

    // Preferences
    [BindProperty] public string CurrencyCode { get; set; } = "PKR";
    [BindProperty] public string TimeZoneId { get; set; } = "Asia/Karachi";
    [BindProperty] public string DateFormat { get; set; } = "dd/MM/yyyy";
    [BindProperty] public string TimeFormat { get; set; } = "hh:mm tt";
    [BindProperty] public string DefaultLanguage { get; set; } = "en";
    [BindProperty, StringLength(200)] public string? ReceiptPrinterName { get; set; }
    [BindProperty] public string ReceiptPaperSize { get; set; } = "80mm";
    [BindProperty, StringLength(200)] public string? ReportPrinterName { get; set; }
    [BindProperty] public string ReportPaperSize { get; set; } = "A4";

    private async Task<IActionResult?> GuardAsync()
    {
        State = await _setup.GetStateAsync();
        if (State == SetupState.Completed) return RedirectToPage("/Account/Login");
        if (State == SetupState.ResetRequested && !User.IsSystemAdministrator())
        {
            AccessBlocked = true;
            return Page();
        }
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var guard = await GuardAsync();
        if (guard is not null) return guard;

        if (State == SetupState.ResetRequested)
        {
            var s = await _hospital.GetSettingsAsync();
            var c = await _hospital.GetConfigurationAsync();
            HospitalName = s.HospitalName; RegistrationNumber = s.RegistrationNumber; Address = s.Address; City = s.City;
            Province = s.Province; ContactNumber = s.ContactNumber; Email = s.Email; Website = s.Website;
            HospitalType = c.HospitalType; CurrencyCode = c.CurrencyCode; TimeZoneId = c.TimeZoneId; DateFormat = c.DateFormat;
            TimeFormat = c.TimeFormat; DefaultLanguage = c.DefaultLanguage; ReceiptPrinterName = c.ReceiptPrinterName;
            ReceiptPaperSize = c.ReceiptPaperSize; ReportPrinterName = c.ReportPrinterName; ReportPaperSize = c.ReportPaperSize;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var guard = await GuardAsync();
        if (guard is not null) return AccessBlocked ? Forbid() : guard;

        var firstInstall = State == SetupState.NotConfigured;
        if (HospitalType is null) ModelState.AddModelError(nameof(HospitalType), "Please select the hospital type (Government or Private).");
        if (firstInstall)
        {
            if (string.IsNullOrWhiteSpace(AdminFullName)) ModelState.AddModelError(nameof(AdminFullName), "Administrator full name is required.");
            if (string.IsNullOrWhiteSpace(AdminUsername)) ModelState.AddModelError(nameof(AdminUsername), "Administrator username is required.");
            if (AdminPassword != AdminConfirmPassword) ModelState.AddModelError(nameof(AdminConfirmPassword), "The passwords do not match.");
        }

        byte[]? logoBytes = null;
        string? logoType = null;
        if (Logo is { Length: > 0 })
        {
            if (Logo.Length > HospitalAdminService.MaxLogoBytes)
                ModelState.AddModelError(nameof(Logo), "The logo must be smaller than 2 MB.");
            else
            {
                using var ms = new MemoryStream();
                await Logo.CopyToAsync(ms);
                logoBytes = ms.ToArray();
                logoType = Validation.DetectImageType(logoBytes);
                if (logoType is null) ModelState.AddModelError(nameof(Logo), "The logo must be a PNG, JPEG, GIF or WebP image.");
            }
        }

        if (!ModelState.IsValid) return Page();

        var result = await _setup.CompleteAsync(new SetupInput
        {
            HospitalName = HospitalName, HospitalType = HospitalType!.Value, RegistrationNumber = RegistrationNumber,
            Address = Address, City = City, Province = Province, ContactNumber = ContactNumber, Email = Email, Website = Website,
            LogoContent = logoBytes, LogoContentType = logoType,
            AdminFullName = AdminFullName ?? string.Empty, AdminUsername = AdminUsername ?? string.Empty,
            AdminPassword = AdminPassword ?? string.Empty, AdminContactNumber = AdminContactNumber,
            CurrencyCode = CurrencyCode, TimeZoneId = TimeZoneId, DateFormat = DateFormat, TimeFormat = TimeFormat,
            DefaultLanguage = DefaultLanguage, ReceiptPrinterName = ReceiptPrinterName, ReceiptPaperSize = ReceiptPaperSize,
            ReportPrinterName = ReportPrinterName, ReportPaperSize = ReportPaperSize
        });

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
            return Page();
        }

        TempData["FlashSuccess"] = firstInstall
            ? "Setup completed successfully. Log in with the administrator account you just created."
            : "Hospital setup updated successfully.";
        return firstInstall ? RedirectToPage("/Account/Login") : Redirect("/");
    }
}
