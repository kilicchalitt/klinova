using System.Text.Encodings.Web;
using System.Text.Json;
using Klinova.Application.Abstractions;
using Klinova.Domain.Auditing;
using Klinova.Domain.Common;
using Klinova.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Klinova.Infrastructure.Persistence;

/// <summary>
/// Maintains creation / modification stamps and writes an <see cref="AuditLog"/>
/// row for every inserted, updated or deleted record. The data change and its
/// audit rows are committed in the same transaction.
/// </summary>
/// <remarks>Registered as a scoped service: it keeps per-save state.</remarks>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, IClock clock) : SaveChangesInterceptor
{
    private const string SystemUser = "system";

    private static readonly string[] SensitivePropertyFragments = ["Password", "SecurityStamp", "ConcurrencyStamp", "Token", "AuthenticatorKey"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private List<PendingAudit>? _pending;
    private IDbContextTransaction? _ownedTransaction;
    private bool _writingAuditRows;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context && !_writingAuditRows)
        {
            Prepare(context);
            if (_pending is { Count: > 0 } && context.Database.CurrentTransaction is null)
            {
                _ownedTransaction = context.Database.BeginTransaction();
            }
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && !_writingAuditRows)
        {
            Prepare(context);
            if (_pending is { Count: > 0 } && context.Database.CurrentTransaction is null)
            {
                _ownedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
            }
        }

        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context && !_writingAuditRows && TryCreateAuditRows(context))
        {
            _writingAuditRows = true;
            try
            {
                context.SaveChanges();
                _ownedTransaction?.Commit();
            }
            finally
            {
                _writingAuditRows = false;
                Reset();
            }
        }
        else if (!_writingAuditRows)
        {
            _ownedTransaction?.Commit();
            Reset();
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && !_writingAuditRows && TryCreateAuditRows(context))
        {
            _writingAuditRows = true;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                if (_ownedTransaction is not null)
                {
                    await _ownedTransaction.CommitAsync(cancellationToken);
                }
            }
            finally
            {
                _writingAuditRows = false;
                Reset();
            }
        }
        else if (!_writingAuditRows)
        {
            if (_ownedTransaction is not null)
            {
                await _ownedTransaction.CommitAsync(cancellationToken);
            }

            Reset();
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (!_writingAuditRows)
        {
            _ownedTransaction?.Rollback();
            Reset();
        }
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (!_writingAuditRows)
        {
            if (_ownedTransaction is not null)
            {
                await _ownedTransaction.RollbackAsync(cancellationToken);
            }

            Reset();
        }
    }

    private void Prepare(DbContext context)
    {
        var now = clock.UtcNow;
        var user = currentUser.UserName ?? SystemUser;
        _pending = [];

        foreach (var entry in context.ChangeTracker.Entries())
        {
            StampAuditFields(entry, now, user);

            if (!ShouldAudit(entry))
            {
                continue;
            }

            var pending = new PendingAudit(entry, ToAction(entry.State), DisplayName(entry));
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                // Original values are lost once the save is accepted, so capture them now.
                pending.EntityId = KeyOf(entry);
                pending.Changes = entry.State == EntityState.Modified ? ModifiedValues(entry) : CurrentValues(entry, original: true);
                if (pending.Changes.Count == 0)
                {
                    continue;
                }
            }

            _pending.Add(pending);
        }
    }

    private bool TryCreateAuditRows(DbContext context)
    {
        if (_pending is not { Count: > 0 })
        {
            return false;
        }

        var now = clock.UtcNow;
        foreach (var pending in _pending)
        {
            if (pending.Action == AuditAction.Created)
            {
                // Generated keys and foreign keys are only known after the insert.
                pending.EntityId = KeyOf(pending.Entry);
                pending.Changes = CurrentValues(pending.Entry, original: false);
            }

            context.Set<AuditLog>().Add(new AuditLog
            {
                TimestampUtc = now,
                UserId = currentUser.UserId,
                UserName = currentUser.UserName ?? SystemUser,
                Action = pending.Action,
                EntityName = pending.EntityName,
                EntityId = pending.EntityId,
                Changes = JsonSerializer.Serialize(pending.Changes, JsonOptions),
            });
        }

        return true;
    }

    private void Reset()
    {
        _pending = null;
        _ownedTransaction?.Dispose();
        _ownedTransaction = null;
    }

    private static void StampAuditFields(EntityEntry entry, DateTime now, string user)
    {
        switch (entry.Entity)
        {
            case AuditableEntity auditable when entry.State == EntityState.Added:
                auditable.CreatedAtUtc = now;
                auditable.CreatedBy = user;
                break;
            case AuditableEntity auditable when entry.State == EntityState.Modified:
                auditable.UpdatedAtUtc = now;
                auditable.UpdatedBy = user;
                entry.Property(nameof(AuditableEntity.CreatedAtUtc)).IsModified = false;
                entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                break;
            case AppUser appUser when entry.State == EntityState.Added && appUser.CreatedAtUtc == default:
                appUser.CreatedAtUtc = now;
                break;
        }
    }

    private static bool ShouldAudit(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            return false;
        }

        return entry.Entity switch
        {
            AuditLog => false,
            Domain.Notifications.Notification => false,
            IdentityUserToken<string> or IdentityUserLogin<string> or IdentityUserClaim<string> or IdentityRoleClaim<string> => false,
            _ => true,
        };
    }

    private static AuditAction ToAction(EntityState state) => state switch
    {
        EntityState.Added => AuditAction.Created,
        EntityState.Deleted => AuditAction.Deleted,
        _ => AuditAction.Updated,
    };

    private static string DisplayName(EntityEntry entry) => entry.Entity switch
    {
        AppUser => "User",
        IdentityUserRole<string> => "UserRole",
        IdentityRole => "Role",
        _ => entry.Metadata.ClrType.Name,
    };

    private static string? KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        return key is null
            ? null
            : string.Join(",", key.Properties.Select(p => Convert.ToString(entry.Property(p.Name).CurrentValue, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static Dictionary<string, object?> CurrentValues(EntityEntry entry, bool original)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            values[name] = IsSensitive(name) ? "***" : original ? property.OriginalValue : property.CurrentValue;
        }

        return values;
    }

    private static Dictionary<string, object?> ModifiedValues(EntityEntry entry)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
        {
            var name = property.Metadata.Name;
            values[name] = IsSensitive(name)
                ? "*** changed ***"
                : new { Old = property.OriginalValue, New = property.CurrentValue };
        }

        return values;
    }

    private static bool IsSensitive(string propertyName)
        => SensitivePropertyFragments.Any(f => propertyName.Contains(f, StringComparison.OrdinalIgnoreCase));

    private sealed class PendingAudit(EntityEntry entry, AuditAction action, string entityName)
    {
        public EntityEntry Entry { get; } = entry;

        public AuditAction Action { get; } = action;

        public string EntityName { get; } = entityName;

        public string? EntityId { get; set; }

        public Dictionary<string, object?> Changes { get; set; } = [];
    }
}
