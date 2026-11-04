using Klinova.Domain.Appointments;
using Klinova.Domain.Common;
using Klinova.Domain.Notifications;
using Klinova.Domain.Organization;
using Klinova.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klinova.Infrastructure.Persistence.Configurations;

internal sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> b)
    {
        b.Property(f => f.Name).HasMaxLength(FieldLengths.ShortText).IsRequired();
        b.Property(f => f.City).HasMaxLength(FieldLengths.Name).IsRequired();
        b.Property(f => f.District).HasMaxLength(FieldLengths.Name).IsRequired();
        b.Property(f => f.Address).HasMaxLength(FieldLengths.Address);
        b.Property(f => f.Phone).HasMaxLength(FieldLengths.Phone);
        b.Property(f => f.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(f => f.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasIndex(f => f.Name).IsUnique();
        b.HasIndex(f => new { f.City, f.District });
    }
}

internal sealed class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> b)
    {
        b.Property(s => s.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(s => s.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasOne(s => s.Doctor).WithMany().HasForeignKey(s => s.DoctorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(s => new { s.DoctorId, s.DayOfWeek });
    }
}

internal sealed class DoctorLeaveConfiguration : IEntityTypeConfiguration<DoctorLeave>
{
    public void Configure(EntityTypeBuilder<DoctorLeave> b)
    {
        b.Property(l => l.Reason).HasMaxLength(FieldLengths.ShortText);
        b.Property(l => l.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(l => l.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasOne(l => l.Doctor).WithMany().HasForeignKey(l => l.DoctorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(l => new { l.DoctorId, l.StartDate, l.EndDate });
    }
}

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.Property(h => h.Name).HasMaxLength(FieldLengths.ShortText).IsRequired();
        b.Property(h => h.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(h => h.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasIndex(h => h.Date).IsUnique();
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.Property(a => a.ConfirmationCode).HasMaxLength(8).IsUnicode(false).IsRequired();
        b.Property(a => a.PatientNote).HasMaxLength(Appointment.NoteMaxLength);
        b.Property(a => a.CancellationReason).HasMaxLength(FieldLengths.ShortText * 2);
        b.Property(a => a.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(a => a.UpdatedBy).HasMaxLength(FieldLengths.ShortText);

        b.HasOne(a => a.Patient).WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Doctor).WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Polyclinic).WithMany().HasForeignKey(a => a.PolyclinicId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Visits.Visit>().WithMany().HasForeignKey(a => a.VisitId).OnDelete(DeleteBehavior.Restrict);

        // A physician's slot can be held by only one appointment unless it was cancelled.
        // The filter is plain SQL understood by both SQLite and SQL Server.
        b.HasIndex(a => new { a.DoctorId, a.Date, a.StartTime })
            .IsUnique()
            .HasFilter($"[Status] <> {(int)AppointmentStatus.Cancelled}")
            .HasDatabaseName("IX_Appointments_ActiveSlot");

        b.HasIndex(a => new { a.PatientId, a.Status });
        b.HasIndex(a => new { a.Date, a.Status });
        b.HasIndex(a => a.ConfirmationCode);
        b.HasIndex(a => a.VisitId).IsUnique();
    }
}

internal sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> b)
    {
        b.Property(w => w.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(w => w.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasOne(w => w.Patient).WithMany().HasForeignKey(w => w.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(w => w.Polyclinic).WithMany().HasForeignKey(w => w.PolyclinicId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(w => w.Doctor).WithMany().HasForeignKey(w => w.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(w => new { w.PolyclinicId, w.Status });
        b.HasIndex(w => new { w.PatientId, w.Status });
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(n => n.Recipient).HasMaxLength(FieldLengths.Email).IsRequired();
        b.Property(n => n.Subject).HasMaxLength(FieldLengths.ShortText * 2).IsRequired();
        b.Property(n => n.Body).HasMaxLength(FieldLengths.LongText).IsRequired();
        b.Property(n => n.LastError).HasMaxLength(500);
        b.HasIndex(n => new { n.Status, n.CreatedAtUtc });
        b.HasIndex(n => n.PatientId);
    }
}
