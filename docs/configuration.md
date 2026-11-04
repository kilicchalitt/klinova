# Yapılandırma

Klinova standart ASP.NET Core yapılandırma sistemini kullanır. Değerler sırasıyla
`appsettings.json` → `appsettings.{Ortam}.json` → ortam değişkenleri → komut satırı
argümanlarından okunur; sonraki kaynak öncekini ezer.

Ortam değişkenlerinde hiyerarşi `__` (çift alt çizgi) ile ifade edilir:
`Klinova:Seed:AdminPassword` → `Klinova__Seed__AdminPassword`.

> **Gizli bilgiler repoya girmez.** Parola ve bağlantı cümlelerini ortam değişkenlerinde,
> Docker `.env` dosyasında (repoya eklenmez) veya geliştirme ortamında
> `dotnet user-secrets` ile tutun:
>
> ```bash
> dotnet user-secrets --project src/Klinova.Web set "Klinova:Seed:AdminPassword" "..."
> ```

## Veritabanı

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `Database:Provider` | `Sqlite` | `Sqlite` veya `SqlServer`. |
| `ConnectionStrings:Default` | `Data Source=data/klinova.db` | Seçilen sağlayıcıya ait bağlantı cümlesi. SQLite yolu çalışma klasörüne görelidir; klasör yoksa oluşturulur. |
| `Klinova:ApplyMigrationsOnStartup` | `true` | Açılışta bekleyen migration'ları uygular. DBA onayıyla şema yönetilen kurumlarda `false` yapın. |

## Uygulama

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `Klinova:TimeZone` | `Europe/Istanbul` | IANA saat dilimi. İş günü, sıra numarası sıfırlanması ve raporlardaki gün sınırları buna göre belirlenir. Tüm zamanlar veritabanında UTC saklanır. |
| `Klinova:UseHttpsRedirection` | `true` | HTTP isteklerini HTTPS'e yönlendirir. TLS bir ters vekil sunucuda sonlanıyorsa `false` yapın. |
| `Klinova:DataProtectionKeysPath` | `data/keys` | Oturum ve CSRF çerezlerini şifreleyen anahtarların kalıcı klasörü. Boş bırakılırsa anahtarlar bellekte tutulur ve her yeniden başlatmada kullanıcıların oturumu düşer. |
| `Klinova:KnownProxies` | `[]` | Güvenilen ters vekil sunucu IP adresleri (`X-Forwarded-*` başlıkları için). |
| `Klinova:LoginRequestsPerMinute` | `20` | Bir IP adresinden dakikada izin verilen giriş sayfası isteği. Tek NAT arkasındaki büyük kurumlarda artırın. |

## İlk Kurulum Verileri

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `Klinova:Seed:AdminUserName` | `admin` | Hiç yönetici yoksa oluşturulacak hesabın kullanıcı adı. |
| `Klinova:Seed:AdminEmail` | `admin@klinova.local` | |
| `Klinova:Seed:AdminPassword` | *(boş)* | Boşsa güçlü bir şifre rastgele üretilir ve **bir kez** loga yazılır. Her durumda ilk girişte değiştirilmesi zorunludur. |
| `Klinova:Seed:DemoData` | `false` (Development'ta `true`) | Örnek hekimler, hastalar ve `kabul`, `hekim`, `vezne` demo kullanıcıları. Üretimde kapalı tutun. |
| `Klinova:Seed:DemoPassword` | *(boş)* | Demo kullanıcıların ortak şifresi; boşsa rastgele üretilir ve loga yazılır. |

Varsayılan poliklinikler ve fiyat listesi, ilgili tablolar boşsa her ortamda oluşturulur.

## Günlükleme

`Logging:LogLevel` bölümü standart ASP.NET Core günlükleme ayarlarıdır. Kimlik doğrulama
olayları (`signed in`, `locked out`) `Information`/`Warning` düzeyinde yazılır.
Günlüklere hasta verisi yazılmaz.
