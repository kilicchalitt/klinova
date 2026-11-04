using Klinova.Application.Abstractions;
using Klinova.Application.Common;
using Klinova.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Auditing;

public sealed record AuditLogQuery(string? EntityName = null, string? UserName = null, DateOnly? Date = null, int Page = 1, int PageSize = 50);

/// <summary>Read-only access to the audit trail for administrators.</summary>
public sealed class AuditLogService(IAppDbContext db, IClock clock)
{
    public async Task<PagedResult<AuditLog>> SearchAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var logs = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            logs = logs.Where(l => l.EntityName == query.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            var user = query.UserName.Trim();
            logs = logs.Where(l => l.UserName != null && l.UserName.Contains(user));
        }

        if (query.Date is { } date)
        {
            var start = clock.StartOfDayUtc(date);
            var end = clock.StartOfDayUtc(date.AddDays(1));
            logs = logs.Where(l => l.TimestampUtc >= start && l.TimestampUtc < end);
        }

        var total = await logs.CountAsync(ct);
        var items = await logs.OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditLog>(items, page, pageSize, total);
    }

    public Task<List<string>> EntityNamesAsync(CancellationToken ct = default)
        => db.AuditLogs.AsNoTracking().Select(l => l.EntityName).Distinct().OrderBy(n => n).ToListAsync(ct);
}
