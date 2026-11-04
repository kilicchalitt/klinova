using Klinova.Domain.Auditing;
using Klinova.Domain.Billing;
using Klinova.Domain.Common;
using Klinova.Domain.Visits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klinova.Infrastructure.Persistence.Configurations;

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> b)
    {
        b.Property(v => v.Complaint).HasMaxLength(FieldLengths.LongText);
        b.Property(v => v.Diagnosis).HasMaxLength(FieldLengths.LongText);
        b.Property(v => v.ExaminationNotes).HasMaxLength(FieldLengths.LongText);
        b.Property(v => v.CancellationReason).HasMaxLength(FieldLengths.ShortText * 2);
        b.Property(v => v.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(v => v.UpdatedBy).HasMaxLength(FieldLengths.ShortText);

        b.HasOne(v => v.Patient).WithMany().HasForeignKey(v => v.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(v => v.Polyclinic).WithMany().HasForeignKey(v => v.PolyclinicId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(v => v.Doctor).WithMany().HasForeignKey(v => v.DoctorId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(v => v.Items).WithOne().HasForeignKey(i => i.VisitId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(v => v.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasOne(v => v.Payment).WithOne().HasForeignKey<Payment>(p => p.VisitId).OnDelete(DeleteBehavior.Restrict);

        // One queue number per polyclinic per day; guards against concurrent admissions.
        b.HasIndex(v => new { v.PolyclinicId, v.VisitDate, v.QueueNumber }).IsUnique();
        b.HasIndex(v => new { v.VisitDate, v.Status });
        b.HasIndex(v => v.PatientId);
    }
}

internal sealed class VisitItemConfiguration : IEntityTypeConfiguration<VisitItem>
{
    public void Configure(EntityTypeBuilder<VisitItem> b)
    {
        b.Property(i => i.ServiceCode).HasMaxLength(FieldLengths.Code).IsRequired();
        b.Property(i => i.ServiceName).HasMaxLength(FieldLengths.ShortText).IsRequired();
        b.HasOne<Domain.Services.MedicalService>().WithMany().HasForeignKey(i => i.MedicalServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(p => p.Note).HasMaxLength(FieldLengths.ShortText * 2);
        b.Property(p => p.ReceivedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasIndex(p => p.VisitId).IsUnique();
        b.HasIndex(p => p.PaidAtUtc);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(l => l.UserId).HasMaxLength(450);
        b.Property(l => l.UserName).HasMaxLength(FieldLengths.ShortText);
        b.Property(l => l.EntityName).HasMaxLength(FieldLengths.ShortText).IsRequired();
        b.Property(l => l.EntityId).HasMaxLength(FieldLengths.ShortText);
        b.HasIndex(l => l.TimestampUtc);
        b.HasIndex(l => new { l.EntityName, l.EntityId });
    }
}
