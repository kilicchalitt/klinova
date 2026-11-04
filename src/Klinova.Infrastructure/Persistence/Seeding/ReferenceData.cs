using Klinova.Domain.Organization;
using Klinova.Domain.Scheduling;
using Klinova.Domain.Services;

namespace Klinova.Infrastructure.Persistence.Seeding;

/// <summary>
/// Default departments and price list created on first start so that a new
/// installation is usable immediately. Prices are examples; administrators are
/// expected to adjust them under "Fiyat Listesi".
/// </summary>
internal static class ReferenceData
{
    /// <summary>The first site of a new installation; administrators rename it under "Kurumlar".</summary>
    public static Facility DefaultFacility() => new()
    {
        Name = "Merkez Poliklinik",
        City = "Ankara",
        District = "Çankaya",
    };

    public static IEnumerable<Polyclinic> Polyclinics(int facilityId) =>
    [
        new() { FacilityId = facilityId, Name = "Dahiliye", Description = "İç Hastalıkları Polikliniği", Location = "Zemin kat, 101" },
        new() { FacilityId = facilityId, Name = "Kulak Burun Boğaz", Description = "KBB Polikliniği", Location = "1. kat, 112" },
        new() { FacilityId = facilityId, Name = "Göz Hastalıkları", Description = "Göz Polikliniği", Location = "1. kat, 115" },
        new() { FacilityId = facilityId, Name = "Ortopedi ve Travmatoloji", Description = "Ortopedi Polikliniği", Location = "2. kat, 204" },
        new() { FacilityId = facilityId, Name = "Çocuk Sağlığı ve Hastalıkları", Description = "Pediatri Polikliniği", Location = "Zemin kat, 105" },
        new() { FacilityId = facilityId, Name = "Acil", Description = "Acil Müdahale Birimi", Location = "Giriş kat", AcceptsOnlineAppointments = false },
    ];

    /// <summary>
    /// National holidays of Türkiye with a fixed date. Religious holidays move every
    /// year and are added by administrators under "Resmî Tatiller".
    /// </summary>
    public static IEnumerable<Holiday> NationalHolidays(int year) =>
    [
        new() { Date = new DateOnly(year, 1, 1), Name = "Yılbaşı" },
        new() { Date = new DateOnly(year, 4, 23), Name = "Ulusal Egemenlik ve Çocuk Bayramı" },
        new() { Date = new DateOnly(year, 5, 1), Name = "Emek ve Dayanışma Günü" },
        new() { Date = new DateOnly(year, 5, 19), Name = "Atatürk'ü Anma, Gençlik ve Spor Bayramı" },
        new() { Date = new DateOnly(year, 7, 15), Name = "Demokrasi ve Millî Birlik Günü" },
        new() { Date = new DateOnly(year, 8, 30), Name = "Zafer Bayramı" },
        new() { Date = new DateOnly(year, 10, 28), Name = "Cumhuriyet Bayramı Arifesi", ClosedFrom = new TimeOnly(13, 0) },
        new() { Date = new DateOnly(year, 10, 29), Name = "Cumhuriyet Bayramı" },
    ];

    public static IEnumerable<MedicalService> Services() =>
    [
        new() { Code = "MUA", Name = "Poliklinik Muayenesi", Category = "Muayene", UnitPrice = 750m },
        new() { Code = "KNT", Name = "Kontrol Muayenesi", Category = "Muayene", UnitPrice = 0m },
        new() { Code = "ACL", Name = "Acil Muayene", Category = "Muayene", UnitPrice = 900m },
        new() { Code = "HMG", Name = "Hemogram (Tam Kan Sayımı)", Category = "Laboratuvar", UnitPrice = 150m },
        new() { Code = "BYK", Name = "Biyokimya Paneli", Category = "Laboratuvar", UnitPrice = 450m },
        new() { Code = "TIT", Name = "Tam İdrar Tahlili", Category = "Laboratuvar", UnitPrice = 120m },
        new() { Code = "RNT", Name = "Direkt Röntgen", Category = "Görüntüleme", UnitPrice = 400m },
        new() { Code = "USG", Name = "Ultrasonografi", Category = "Görüntüleme", UnitPrice = 900m },
        new() { Code = "EKG", Name = "Elektrokardiyografi (EKG)", Category = "Tetkik", UnitPrice = 300m },
        new() { Code = "SRM", Name = "Serum Uygulaması", Category = "Girişimsel", UnitPrice = 350m },
        new() { Code = "ENJ", Name = "Enjeksiyon", Category = "Girişimsel", UnitPrice = 100m },
        new() { Code = "PNS", Name = "Pansuman", Category = "Girişimsel", UnitPrice = 200m },
    ];
}
