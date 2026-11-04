using Klinova.Application.Auditing;
using Klinova.Application.Common;
using Klinova.Domain.Auditing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Klinova.Web.Pages.Admin;

public class AuditLogsModel(AuditLogService audit) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Entity { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserName { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Date { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<AuditLog> Result { get; private set; } = default!;

    public List<string> EntityNames { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        EntityNames = await audit.EntityNamesAsync(ct);
        Result = await audit.SearchAsync(new AuditLogQuery(Entity, UserName, Date, PageNumber), ct);
    }

    public static string ActionName(AuditAction action) => action switch
    {
        AuditAction.Created => "Ekleme",
        AuditAction.Updated => "Güncelleme",
        AuditAction.Deleted => "Silme",
        _ => action.ToString(),
    };

    public static string EntityDisplayName(string entity) => entity switch
    {
        "Patient" => "Hasta",
        "Visit" => "Başvuru",
        "VisitItem" => "Başvuru Hizmeti",
        "Payment" => "Tahsilat",
        "Polyclinic" => "Poliklinik",
        "Doctor" => "Hekim",
        "MedicalService" => "Hizmet",
        "InstitutionProfile" => "Kurum Bilgileri",
        "User" => "Kullanıcı",
        "UserRole" => "Kullanıcı Rolü",
        "Role" => "Rol",
        _ => entity,
    };
}
