using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Suppliers;

[HasPermission(Perm.PharmManageSuppliers)]
public class EditModel : PharmacyPageModel
{
    [BindProperty] public SupplierInput Input { get; set; } = new();
    public bool IsNew { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await InitAsync("suppliers");
        IsNew = id is null;
        if (id is int sid)
        {
            var s = await Catalog.SupplierAsync(sid);
            if (s is null) return NotFound();
            Input = new SupplierInput { Name = s.Name, ContactPerson = s.ContactPerson, Phone = s.Phone, Address = s.Address, City = s.City, Email = s.Email, Notes = s.Notes, IsActive = s.IsActive };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        await InitAsync("suppliers");
        IsNew = id is null;
        var r = await Catalog.SaveSupplierAsync(id, Input);
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = "Supplier saved.";
        return Redirect($"/Pharmacy/Suppliers/{r.Value}");
    }
}
