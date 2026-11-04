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

## Randevu Kuralları

`Klinova:Appointments` bölümü online randevu kurallarını belirler. Varsayılanlar MHRS'den alışık
olunan uygulamaya göre seçilmiştir.

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `BookingHorizonDays` | `30` | Hastanın online olarak kaç gün ileriye randevu alabileceği. |
| `StaffBookingHorizonDays` | `90` | Personelin (telefon / danışma) kaç gün ileriye randevu verebileceği. |
| `MinimumLeadMinutes` | `60` | Online randevunun en az kaç dakika sonra başlaması gerektiği. |
| `MaxActivePerPatient` | `5` | Bir hastanın aynı anda sahip olabileceği aktif online randevu sayısı. |
| `MaxActivePerPolyclinic` | `1` | Aynı poliklinikte aynı anda aktif randevu sayısı (hem online hem personel). |
| `CancellationDeadlineHours` | `2` | Hastanın randevudan kaç saat öncesine kadar online iptal / değişiklik yapabileceği. |
| `NoShowRestrictionDays` | `15` | Gelmeme sonrası aynı poliklinikten online randevu kısıtlaması (gün). `0` kapatır. |
| `NoShowsForGlobalRestriction` | `2` | Kısıtlama süresi içinde bu kadar gelmeme olursa tüm poliklinikler kısıtlanır. `0` kapatır. |
| `ReminderHoursBefore` | `24` | Hatırlatma mesajının randevudan kaç saat önce gönderileceği. |
| `MaxWaitlistEntriesPerPatient` | `3` | Hastanın aynı anda kaç bekleme listesi kaydı olabileceği. |
| `WaitlistNotifyCount` | `3` | Bir randevu iptal edildiğinde bilgilendirilecek bekleyen hasta sayısı. |

Kurum personeli, gelmeme kısıtlaması olan hastalara da telefonla / danışmadan randevu verebilir.

## Bildirimler

Hasta portalı hesabı olan hastalar tüm mesajları (randevu onayı, hatırlatma, iptal, bekleme listesi)
portaldaki **Bildirimlerim** bölümünde görür; bunun için hiçbir sağlayıcı gerekmez. Okunmamış bildirimler
menüde sayıyla gösterilir.

`Klinova:Notifications` bölümü isteğe bağlı SMS ve e-posta sağlayıcılarını tanımlar. Bir sağlayıcı
tanımlanmadıkça o kanal için mesaj oluşturulmaz. Mesajlar önce veritabanındaki gönderim kuyruğuna yazılır,
arka plan işi tarafından gönderilir; başarısız olanlar 5 kez yeniden denenir ve **Yönetim → Bildirimler**
ekranından izlenir.

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `Email:Enabled` | `false` | `true` ise e-postalar SMTP ile gönderilir; `false` ise e-posta oluşturulmaz. |
| `Email:Host`, `Email:Port`, `Email:EnableSsl` | — / `587` / `true` | SMTP sunucusu. |
| `Email:UserName`, `Email:Password` | — | SMTP kimlik bilgileri (**ortam değişkeni olarak verin**). |
| `Email:From`, `Email:FromName` | — / `Klinova` | Gönderen adresi ve adı. |
| `Sms:Provider` | `Log` | `Log` (SMS kanalı kapalı) veya `Webhook`. |
| `Sms:WebhookUrl` | — | `POST {"to": "...", "message": "..."}` kabul eden HTTPS adresi. SMS operatörünüzün API'sine bağlanan küçük bir ara servis kullanın. |
| `Sms:ApiKey` | — | Webhook'a `X-Api-Key` başlığıyla gönderilir (**ortam değişkeni olarak verin**). |

Örnek (Docker / systemd ortam değişkenleri):

```bash
Klinova__Notifications__Sms__Provider=Webhook
Klinova__Notifications__Sms__WebhookUrl=https://sms-adapter.kurum.local/send
Klinova__Notifications__Sms__ApiKey=********
```

Bilgi düzeyindeki loglara yalnızca mesaj türü ve maskelenmiş alıcı yazılır; mesaj metni hasta verisi
içerebileceği için yalnızca `Debug` düzeyinde loglanır.

## Arka Plan İşleri

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `Klinova:BackgroundJobs:Enabled` | `true` | Bildirim gönderimi ve randevu bakımı. Birden fazla sunucu çalıştırıyorsanız yalnızca birinde açık tutun. |
| `Klinova:BackgroundJobs:NotificationIntervalSeconds` | `30` | Gönderim kuyruğunun kontrol aralığı. |
| `Klinova:BackgroundJobs:MaintenanceIntervalMinutes` | `10` | Hatırlatma, "gelmedi" işaretleme ve bekleme listesi süresi kontrol aralığı. |

## İlk Kurulum Verileri

| Anahtar | Varsayılan | Açıklama |
| --- | --- | --- |
| `Klinova:Seed:AdminUserName` | `admin` | Hiç yönetici yoksa oluşturulacak hesabın kullanıcı adı. |
| `Klinova:Seed:AdminEmail` | `admin@klinova.local` | |
| `Klinova:Seed:AdminPassword` | *(boş)* | **Henüz yönetici yoksa zorunludur;** verilmezse uygulama açıklayıcı bir mesajla durur. Şifre hiçbir yere loglanmaz ve ilk girişte değiştirilmesi zorunludur. Yönetici oluşturulduktan sonra bu ayarı kaldırabilirsiniz. |
| `Klinova:Seed:DemoData` | `false` (Development'ta `true`) | Örnek şubeler, hekimler, hastalar, randevular, `kabul`, `hekim`, `vezne` demo kullanıcıları ve T.C. `10000000146` ile hasta portalı hesabı. Üretimde kapalı tutun. |
| `Klinova:Seed:DemoPassword` | *(boş)* | Demo kullanıcıların ve demo hastanın ortak şifresi; demo verisi açıksa zorunludur. |

Varsayılan kurum (şube), poliklinikler, fiyat listesi ve içinde bulunulan ile sonraki yılın sabit tarihli
millî bayramları, ilgili tablolar boşsa her ortamda oluşturulur. Demo verisi ayrıca ikinci bir şube,
hekimlerin hafta içi çalışma takvimi ve örnek randevular ekler.

## Günlükleme

`Logging:LogLevel` bölümü standart ASP.NET Core günlükleme ayarlarıdır. Kimlik doğrulama
olayları (`signed in`, `locked out`) `Information`/`Warning` düzeyinde yazılır.
Günlüklere hasta verisi yazılmaz.
