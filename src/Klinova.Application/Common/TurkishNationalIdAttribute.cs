using System.ComponentModel.DataAnnotations;
using Klinova.Domain.Patients;

namespace Klinova.Application.Common;

/// <summary>Validates an optional T.C. Kimlik No. Empty values are considered valid.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TurkishNationalIdAttribute : ValidationAttribute
{
    public TurkishNationalIdAttribute()
        : base("Geçerli bir T.C. Kimlik No giriniz.")
    {
    }

    public override bool IsValid(object? value)
        => value is null
           || (value is string s && (string.IsNullOrWhiteSpace(s) || TurkishNationalId.IsValid(s.Trim())));
}
