using LicenseSystem.Core.Interfaces;
using LicenseSystem.Core.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LicenseSystem.Admin.Pages.AuditLogs;

public class IndexModel : PageModel
{
    private readonly IAuditRepository _auditRepo;

    public IndexModel(IAuditRepository auditRepo)
    {
        _auditRepo = auditRepo;
    }

    public List<AuditEvent> Events { get; set; } = new();

    public async Task OnGetAsync()
    {
        Events = await _auditRepo.GetRecentEventsAsync(100);
    }
}
