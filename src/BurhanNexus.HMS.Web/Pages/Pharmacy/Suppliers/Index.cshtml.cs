using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Suppliers;

[HasPermission(Perm.PharmManageSuppliers, Perm.PharmManagePurchases)]
public class IndexModel : PharmacyPageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public bool Inactive { get; set; }
    public IReadOnlyList<Supplier> Suppliers { get; private set; } = Array.Empty<Supplier>();
    public Dictionary<int, decimal> Balances { get; } = new();

    public async Task OnGetAsync()
    {
        await InitAsync("suppliers");
        Suppliers = await Catalog.SuppliersAsync(Q, !Inactive);
        if (IsPrivate && await CanAsync(Perm.PharmManagePurchases))
            foreach (var s in Suppliers) Balances[s.Id] = await Query.SupplierBalanceAsync(s.Id);
    }
}
