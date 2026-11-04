using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Klinova.Domain.Services;
using Klinova.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klinova.Infrastructure.Persistence.Configurations;

internal sealed class PolyclinicConfiguration : IEntityTypeConfiguration<Polyclinic>
{
    public void Configure(EntityTypeBuilder<Polyclinic> b)
    {
        b.Property(p => p.Name).HasMaxLength(FieldLengths.ShortText).IsRequired();
        b.Property(p => p.Location).HasMaxLength(FieldLengths.ShortText);
        b.Property(p => p.Description).HasMaxLength(FieldLengths.Address);
        b.Property(p => p.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(p => p.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasOne(p => p.Facility)
            .WithMany(f => f.Polyclinics)
            .HasForeignKey(p => p.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        // The same department name may exist in several facilities.
        b.HasIndex(p => new { p.FacilityId, p.Name }).IsUnique();
    }
}

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> b)
    {
        b.Property(d => d.Title).HasMaxLength(FieldLengths.Title);
        b.Property(d => d.FirstName).HasMaxLength(FieldLengths.Name).IsRequired();
        b.Property(d => d.LastName).HasMaxLength(FieldLengths.Name).IsRequired();
        b.Property(d => d.RegistrationNumber).HasMaxLength(FieldLengths.Code * 2);
        b.Property(d => d.Phone).HasMaxLength(FieldLengths.Phone);
        b.Property(d => d.Email).HasMaxLength(FieldLengths.Email);
        b.Property(d => d.UserId).HasMaxLength(450);
        b.Property(d => d.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(d => d.UpdatedBy).HasMaxLength(FieldLengths.ShortText);

        b.HasOne(d => d.Polyclinic)
            .WithMany(p => p.Doctors)
            .HasForeignKey(d => d.PolyclinicId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(d => d.UserId).IsUnique();
    }
}

internal sealed class MedicalServiceConfiguration : IEntityTypeConfiguration<MedicalService>
{
    public void Configure(EntityTypeBuilder<MedicalService> b)
    {
        b.Property(s => s.Code).HasMaxLength(FieldLengths.Code).IsRequired();
        b.Property(s => s.Name).HasMaxLength(FieldLengths.ShortText).IsRequired();
        b.Property(s => s.Category).HasMaxLength(FieldLengths.Name);
        b.Property(s => s.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(s => s.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
        b.HasIndex(s => s.Code).IsUnique();
    }
}

internal sealed class InstitutionProfileConfiguration : IEntityTypeConfiguration<InstitutionProfile>
{
    public void Configure(EntityTypeBuilder<InstitutionProfile> b)
    {
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Name).HasMaxLength(FieldLengths.ShortText).IsRequired();
        b.Property(p => p.Address).HasMaxLength(FieldLengths.Address);
        b.Property(p => p.Phone).HasMaxLength(FieldLengths.Phone);
        b.Property(p => p.Email).HasMaxLength(FieldLengths.Email);
        b.Property(p => p.TaxOffice).HasMaxLength(FieldLengths.Name);
        b.Property(p => p.TaxNumber).HasMaxLength(FieldLengths.Code);
        b.Property(p => p.ReceiptFooter).HasMaxLength(FieldLengths.Address);
        b.Property(p => p.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(p => p.UpdatedBy).HasMaxLength(FieldLengths.ShortText);
    }
}
