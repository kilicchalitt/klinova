using Klinova.Domain.Common;

namespace Klinova.Domain.Organization;

/// <summary>An outpatient department (e.g. Internal Medicine, ENT, Paediatrics).</summary>
public class Polyclinic : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Room or floor information printed on the queue slip.</summary>
    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public int FacilityId { get; set; }

    public Facility? Facility { get; set; }

    /// <summary>Whether patients may book this polyclinic through the online portal.</summary>
    public bool AcceptsOnlineAppointments { get; set; } = true;

    public ICollection<Doctor> Doctors { get; } = [];
}
