using Klinova.Domain.Organization;
using Klinova.Domain.Services;

namespace Klinova.Infrastructure.Persistence.Seeding;

/// <summary>
/// Default departments and price list created on first start so that a new
/// installation is usable immediately. Prices are examples; administrators are
/// expected to adjust them under "Fiyat Listesi".
/// </summary>
internal static class ReferenceData
{
    public static IEnumerable<Polyclinic> Polyclinics() =>
    [
        new() { Name = "Dahiliye", Description = "İç Hastalıkları Polikliniği", Location = "Zemin kat, 101" },
        new() { Name = "Kulak Burun Boğaz", Description = "KBB Polikliniği", Location = "1. kat, 112" },
        new() { Name = "Göz Hastalıkları", Description = "Göz Polikliniği", Location = "1. kat, 115" },
        new() { Name = "Ortopedi ve Travmatoloji", Description = "Ortopedi Polikliniği", Location = "2. kat, 204" },
        new() { Name = "Çocuk Sağlığı ve Hastalıkları", Description = "Pediatri Polikliniği", Location = "Zemin kat, 105" },
        new() { Name = "Acil", Description = "Acil Müdahale Birimi", Location = "Giriş kat" },
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
