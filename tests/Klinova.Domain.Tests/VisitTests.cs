using Klinova.Domain.Billing;
using Klinova.Domain.Common;
using Klinova.Domain.Organization;
using Klinova.Domain.Services;
using Klinova.Domain.Visits;

namespace Klinova.Domain.Tests;

public class VisitTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);

    private static Visit NewVisit() => new(patientId: 1, polyclinicId: 1, doctorId: null, new DateOnly(2026, 11, 4), queueNumber: 1, Now, "Baş ağrısı");

    private static MedicalService Service(decimal price, bool active = true)
        => new() { Code = "MUA", Name = "Muayene", UnitPrice = price, IsActive = active };

    [Fact]
    public void NewVisit_StartsInWaitingState()
    {
        var visit = NewVisit();

        Assert.Equal(VisitStatus.Waiting, visit.Status);
        Assert.True(visit.IsOpen);
        Assert.False(visit.IsPaid);
        Assert.Equal("Baş ağrısı", visit.Complaint);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public void Constructor_RejectsMissingReferences(int patientId, int polyclinicId, int queueNumber)
        => Assert.Throws<DomainException>(() => new Visit(patientId, polyclinicId, null, new DateOnly(2026, 1, 1), queueNumber, Now, null));

    [Fact]
    public void TotalAmount_SumsQuantityTimesUnitPrice()
    {
        var visit = NewVisit();
        visit.AddService(Service(150m), 1, Now);
        visit.AddService(Service(40.5m), 3, Now);

        Assert.Equal(271.5m, visit.TotalAmount);
    }

    [Fact]
    public void AddService_CopiesPriceSoThatLaterPriceChangesDoNotAffectTheVisit()
    {
        var visit = NewVisit();
        var service = Service(100m);
        visit.AddService(service, 2, Now);

        service.UnitPrice = 999m;

        Assert.Equal(200m, visit.TotalAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Visit.MaxQuantity + 1)]
    public void AddService_RejectsInvalidQuantity(int quantity)
        => Assert.Throws<DomainException>(() => NewVisit().AddService(Service(10m), quantity, Now));

    [Fact]
    public void AddService_RejectsInactiveService()
        => Assert.Throws<DomainException>(() => NewVisit().AddService(Service(10m, active: false), 1, Now));

    [Fact]
    public void StartExamination_MovesWaitingVisitToExamination()
    {
        var visit = NewVisit();

        visit.StartExamination(doctorId: 5, Now);

        Assert.Equal(VisitStatus.InExamination, visit.Status);
        Assert.Equal(5, visit.DoctorId);
        Assert.Equal(Now, visit.StartedAtUtc);
    }

    [Fact]
    public void StartExamination_FailsWhenNotWaiting()
    {
        var visit = NewVisit();
        visit.StartExamination(null, Now);

        Assert.Throws<DomainException>(() => visit.StartExamination(null, Now));
    }

    [Fact]
    public void ReturnToQueue_ClearsStartTime()
    {
        var visit = NewVisit();
        visit.StartExamination(null, Now);

        visit.ReturnToQueue();

        Assert.Equal(VisitStatus.Waiting, visit.Status);
        Assert.Null(visit.StartedAtUtc);
    }

    [Fact]
    public void Complete_SetsTimestamps()
    {
        var visit = NewVisit();

        visit.Complete(Now);

        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.Equal(Now, visit.CompletedAtUtc);
        Assert.Equal(Now, visit.StartedAtUtc);
    }

    [Fact]
    public void Cancel_RequiresReason()
        => Assert.Throws<DomainException>(() => NewVisit().Cancel("  ", Now));

    [Fact]
    public void Cancel_IsNotAllowedAfterCompletion()
    {
        var visit = NewVisit();
        visit.Complete(Now);

        Assert.Throws<DomainException>(() => visit.Cancel("Hasta ayrıldı", Now));
    }

    [Fact]
    public void CancelledVisit_RejectsFurtherChanges()
    {
        var visit = NewVisit();
        visit.Cancel("Hasta gelmedi", Now);

        Assert.Equal(VisitStatus.Cancelled, visit.Status);
        Assert.Throws<DomainException>(() => visit.AddService(Service(10m), 1, Now));
        Assert.Throws<DomainException>(() => visit.RecordExamination("a", "b", "c"));
    }

    [Fact]
    public void RegisterPayment_RequiresCompletedVisit()
    {
        var visit = NewVisit();
        visit.AddService(Service(100m), 1, Now);

        Assert.Throws<DomainException>(() => visit.RegisterPayment(0m, PaymentMethod.Cash, null, "kasa", Now));
    }

    [Fact]
    public void RegisterPayment_RequiresAtLeastOneService()
    {
        var visit = NewVisit();
        visit.Complete(Now);

        Assert.Throws<DomainException>(() => visit.RegisterPayment(0m, PaymentMethod.Cash, null, "kasa", Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void RegisterPayment_RejectsDiscountOutsideRange(decimal discount)
    {
        var visit = NewVisit();
        visit.AddService(Service(100m), 1, Now);
        visit.Complete(Now);

        Assert.Throws<DomainException>(() => visit.RegisterPayment(discount, PaymentMethod.Cash, null, "kasa", Now));
    }

    [Fact]
    public void RegisterPayment_CalculatesNetAmountAndLocksTheVisit()
    {
        var visit = NewVisit();
        visit.AddService(Service(100m), 2, Now);
        visit.Complete(Now);

        var payment = visit.RegisterPayment(25m, PaymentMethod.Card, "Personel indirimi", "kasa", Now);

        Assert.Equal(200m, payment.GrossAmount);
        Assert.Equal(25m, payment.Discount);
        Assert.Equal(175m, payment.NetAmount);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.True(visit.IsPaid);
        Assert.Throws<DomainException>(() => visit.AddService(Service(10m), 1, Now));
        Assert.Throws<DomainException>(() => visit.RegisterPayment(0m, PaymentMethod.Cash, null, "kasa", Now));
        Assert.Throws<DomainException>(visit.Reopen);
    }

    [Fact]
    public void Reopen_AllowsAddingServicesToUnpaidCompletedVisit()
    {
        var visit = NewVisit();
        visit.Complete(Now);

        visit.Reopen();
        visit.AddService(Service(30m), 1, Now);

        Assert.Equal(VisitStatus.InExamination, visit.Status);
        Assert.Null(visit.CompletedAtUtc);
        Assert.Equal(30m, visit.TotalAmount);
    }

    [Fact]
    public void AssignDoctor_RejectsDoctorFromAnotherPolyclinic()
    {
        var visit = NewVisit();
        var doctor = new Doctor { FirstName = "Ayşe", LastName = "Yılmaz", PolyclinicId = 2 };

        Assert.Throws<DomainException>(() => visit.AssignDoctor(doctor));
    }

    [Fact]
    public void RecordExamination_TrimsAndNullsBlankValues()
    {
        var visit = NewVisit();

        visit.RecordExamination("  Öksürük ", "", "  ");

        Assert.Equal("Öksürük", visit.Complaint);
        Assert.Null(visit.Diagnosis);
        Assert.Null(visit.ExaminationNotes);
    }
}
