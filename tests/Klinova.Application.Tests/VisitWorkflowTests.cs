using Klinova.Application.Billing;
using Klinova.Application.Reports;
using Klinova.Application.Visits;
using Klinova.Domain.Auditing;
using Klinova.Domain.Billing;
using Klinova.Domain.Common;
using Klinova.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace Klinova.Application.Tests;

public sealed class VisitWorkflowTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private VisitService Visits(Infrastructure.Persistence.AppDbContext ctx) => new(ctx, _db.Clock);

    [Fact]
    public async Task AdmitAsync_AssignsIncreasingQueueNumbersPerPolyclinicAndDay()
    {
        var (polyclinic, exam, _, patient) = await _db.SeedBasicsAsync();
        await using var ctx = _db.CreateContext();
        var second = new Domain.Patients.Patient { FirstName = "Can", LastName = "Demir" };
        second.RefreshSearchName();
        ctx.Patients.Add(second);
        await ctx.SaveChangesAsync();

        var v1 = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id, ServiceIds = [exam.Id] });
        var v2 = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = second.Id, PolyclinicId = polyclinic.Id });

        Assert.Equal(1, v1.QueueNumber);
        Assert.Equal(2, v2.QueueNumber);
        Assert.Equal(500m, v1.TotalAmount);
    }

    [Fact]
    public async Task AdmitAsync_RestartsQueueNumbersOnTheNextLocalDay()
    {
        var (polyclinic, _, _, patient) = await _db.SeedBasicsAsync();
        await using (var ctx = _db.CreateContext())
        {
            var v = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id });
            await Visits(ctx).CompleteAsync(v.Id);
        }

        // 21:30 UTC is already 00:30 the next day in Istanbul (UTC+3).
        _db.Clock.UtcNow = new DateTime(2026, 11, 4, 21, 30, 0, DateTimeKind.Utc);
        await using (var ctx = _db.CreateContext())
        {
            var next = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id });

            Assert.Equal(1, next.QueueNumber);
            Assert.Equal(new DateOnly(2026, 11, 5), next.VisitDate);
        }
    }

    [Fact]
    public async Task AdmitAsync_RejectsSecondOpenAdmissionToSamePolyclinic()
    {
        var (polyclinic, _, _, patient) = await _db.SeedBasicsAsync();
        await using var ctx = _db.CreateContext();
        await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id });

        await Assert.ThrowsAsync<DomainException>(() => Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id }));
    }

    [Fact]
    public async Task AdmitAsync_RejectsInactivePolyclinic()
    {
        var (polyclinic, _, _, patient) = await _db.SeedBasicsAsync();
        await using var ctx = _db.CreateContext();
        var entity = await ctx.Polyclinics.SingleAsync(p => p.Id == polyclinic.Id);
        entity.IsActive = false;
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id }));
    }

    [Fact]
    public async Task CallNextAsync_TakesLowestQueueNumber()
    {
        var (polyclinic, _, _, patient) = await _db.SeedBasicsAsync();
        await using var ctx = _db.CreateContext();
        var other = new Domain.Patients.Patient { FirstName = "Can", LastName = "Demir" };
        other.RefreshSearchName();
        ctx.Patients.Add(other);
        await ctx.SaveChangesAsync();
        var first = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id });
        await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = other.Id, PolyclinicId = polyclinic.Id });

        var called = await Visits(ctx).CallNextAsync(polyclinic.Id, doctorId: null);

        Assert.Equal(first.Id, called);
        Assert.Equal(VisitStatus.InExamination, (await Visits(ctx).GetDetailsAsync(first.Id)).Status);
    }

    [Fact]
    public async Task StartAsync_RecordsPhysicianOnlyForTheirOwnPolyclinic()
    {
        var (polyclinic, _, _, patient) = await _db.SeedBasicsAsync();
        await using var ctx = _db.CreateContext();
        var other = new Domain.Organization.Polyclinic { Name = "Göz" };
        ctx.Polyclinics.Add(other);
        await ctx.SaveChangesAsync();
        var own = new Domain.Organization.Doctor { FirstName = "Ece", LastName = "Kaya", PolyclinicId = polyclinic.Id };
        var foreign = new Domain.Organization.Doctor { FirstName = "Can", LastName = "Ak", PolyclinicId = other.Id };
        ctx.Doctors.AddRange(own, foreign);
        await ctx.SaveChangesAsync();

        var v1 = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id });
        await Visits(ctx).StartAsync(v1.Id, foreign.Id);
        Assert.Null((await Visits(ctx).GetDetailsAsync(v1.Id)).DoctorId);

        await Visits(ctx).ReturnToQueueAsync(v1.Id);
        await Visits(ctx).StartAsync(v1.Id, own.Id);
        Assert.Equal(own.Id, (await Visits(ctx).GetDetailsAsync(v1.Id)).DoctorId);
    }

    [Fact]
    public async Task FullWorkflow_AdmitExamineBillAndReport()
    {
        var (polyclinic, exam, lab, patient) = await _db.SeedBasicsAsync();
        int visitId;

        await using (var ctx = _db.CreateContext())
        {
            var visit = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id, Complaint = "Ateş", ServiceIds = [exam.Id] });
            visitId = visit.Id;
        }

        await using (var ctx = _db.CreateContext())
        {
            var visits = Visits(ctx);
            await visits.StartAsync(visitId, null);
            await visits.RecordExaminationAsync(visitId, new ExaminationInput { Complaint = "Ateş", Diagnosis = "Üst solunum yolu enfeksiyonu" });
            await visits.AddServiceAsync(visitId, lab.Id, 2);
            await visits.CompleteAsync(visitId);
        }

        await using (var ctx = _db.CreateContext())
        {
            var billing = new BillingService(ctx, _db.Clock, _db.User, Visits(ctx));
            var outstanding = await billing.ListOutstandingAsync();
            Assert.Equal(741m, Assert.Single(outstanding).TotalAmount);

            var payment = await billing.CollectAsync(visitId, new PaymentInput { Discount = 41m, Method = PaymentMethod.Card });
            Assert.Equal(700m, payment.NetAmount);
            Assert.Empty(await billing.ListOutstandingAsync());
            await Assert.ThrowsAsync<DomainException>(() => billing.CollectAsync(visitId, new PaymentInput()));
        }

        await using (var ctx = _db.CreateContext())
        {
            var report = await new ReportService(ctx, _db.Clock).GetRevenueReportAsync(_db.Clock.Today, _db.Clock.Today);

            Assert.Equal(1, report.PaymentCount);
            Assert.Equal(741m, report.GrossAmount);
            Assert.Equal(41m, report.DiscountAmount);
            Assert.Equal(700m, report.NetAmount);
            Assert.Equal(700m, Assert.Single(report.ByPolyclinic).Amount);
            Assert.Equal(700m, report.ByPaymentMethod[PaymentMethod.Card]);
            Assert.Equal(700m, report.ByService.Sum(s => s.Amount));
            Assert.Equal(2, report.ByService.Single(s => s.Label == "Hemogram").Count);

            var csv = System.Text.Encoding.UTF8.GetString(ReportService.ToCsv(report));
            Assert.Contains("Dahiliye;1;700,00", csv, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Changes_AreWrittenToTheAuditLog()
    {
        var (polyclinic, _, _, patient) = await _db.SeedBasicsAsync();
        await using (var ctx = _db.CreateContext())
        {
            var visit = await Visits(ctx).AdmitAsync(new AdmissionInput { PatientId = patient.Id, PolyclinicId = polyclinic.Id });
            await Visits(ctx).CancelAsync(visit.Id, "Hasta gelmedi");
        }

        await using (var ctx = _db.CreateContext())
        {
            var logs = await ctx.AuditLogs.Where(l => l.EntityName == nameof(Visit)).OrderBy(l => l.Id).ToListAsync();

            Assert.Equal(2, logs.Count);
            Assert.Equal(AuditAction.Created, logs[0].Action);
            Assert.Equal(AuditAction.Updated, logs[1].Action);
            Assert.All(logs, l => Assert.Equal("tester", l.UserName));
            Assert.Contains("Hasta gelmedi", logs[1].Changes, StringComparison.Ordinal);
            Assert.NotNull(logs[0].EntityId);
            Assert.NotEqual("0", logs[0].EntityId);
        }
    }

    [Fact]
    public async Task RevenueReport_RejectsInvertedRange()
    {
        await using var ctx = _db.CreateContext();
        await Assert.ThrowsAsync<DomainException>(() => new ReportService(ctx, _db.Clock).GetRevenueReportAsync(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
    }
}
