using Klinova.Domain.Common;
using Klinova.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klinova.Infrastructure.Persistence.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.Property(p => p.FirstName).HasMaxLength(FieldLengths.Name).IsRequired();
        b.Property(p => p.LastName).HasMaxLength(FieldLengths.Name).IsRequired();
        b.Property(p => p.SearchName).HasMaxLength((FieldLengths.Name * 2) + 1).IsRequired();
        b.Property(p => p.NationalId).HasMaxLength(FieldLengths.NationalId).IsUnicode(false);
        b.Property(p => p.PassportNumber).HasMaxLength(FieldLengths.Code);
        b.Property(p => p.BirthPlace).HasMaxLength(FieldLengths.Name);
        b.Property(p => p.MotherName).HasMaxLength(FieldLengths.Name);
        b.Property(p => p.FatherName).HasMaxLength(FieldLengths.Name);
        b.Property(p => p.Phone).HasMaxLength(FieldLengths.Phone);
        b.Property(p => p.Email).HasMaxLength(FieldLengths.Email);
        b.Property(p => p.Address).HasMaxLength(FieldLengths.Address);
        b.Property(p => p.InsuranceInstitution).HasMaxLength(FieldLengths.ShortText);
        b.Property(p => p.InsuranceNumber).HasMaxLength(FieldLengths.Code * 2);
        b.Property(p => p.EmergencyContactName).HasMaxLength(FieldLengths.ShortText);
        b.Property(p => p.EmergencyContactPhone).HasMaxLength(FieldLengths.Phone);
        b.Property(p => p.MedicalAlerts).HasMaxLength(FieldLengths.LongText / 4);
        b.Property(p => p.Notes).HasMaxLength(FieldLengths.LongText);
        b.Property(p => p.CreatedBy).HasMaxLength(FieldLengths.ShortText);
        b.Property(p => p.UpdatedBy).HasMaxLength(FieldLengths.ShortText);

        b.Property(p => p.UserId).HasMaxLength(450);

        b.HasIndex(p => p.NationalId).IsUnique();
        b.HasIndex(p => p.UserId).IsUnique();
        b.HasIndex(p => p.SearchName);
        b.HasIndex(p => p.Phone);
        b.HasIndex(p => new { p.LastName, p.FirstName });
    }
}
