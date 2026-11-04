# Kurulum Rehberi

Bu rehber Klinova'yı bir kurumda üretim ortamına kurmak için gereken adımları anlatır.

- [Sistem Gereksinimleri](#sistem-gereksinimleri)
- [Seçenek 1: Docker](#seçenek-1-docker)
- [Seçenek 2: Linux sunucu (systemd)](#seçenek-2-linux-sunucu-systemd)
- [Seçenek 3: Windows Server (IIS)](#seçenek-3-windows-server-iis)
- [SQL Server Kullanımı](#sql-server-kullanımı)
- [HTTPS ve Ters Vekil Sunucu](#https-ve-ters-vekil-sunucu)
- [İlk Giriş](#ilk-giriş)
- [Yedekleme ve Geri Yükleme](#yedekleme-ve-geri-yükleme)
- [Güncelleme](#güncelleme)
- [Güvenlik Kontrol Listesi](#güvenlik-kontrol-listesi)

## Sistem Gereksinimleri

| | Küçük kurum (≤ 10 eş zamanlı kullanıcı) | Orta ölçekli kurum |
| --- | --- | --- |
| CPU / RAM | 2 çekirdek / 2 GB | 4 çekirdek / 4 GB |
| Disk | 10 GB (veri büyümesine göre) | 50 GB+ |
| Veritabanı | SQLite (dahili) | SQL Server 2019+ (Express yeterli) |
| İşletim sistemi | Docker çalıştırabilen herhangi bir sistem, Linux veya Windows Server 2019+ | |
| İstemci | Güncel Chrome, Edge, Firefox veya Safari | |

## Seçenek 1: Docker

```bash
git clone https://github.com/kilicchalitt/klinova.git
cd klinova
cp .env.example .env
```

`.env` dosyasını düzenleyin (`KLINOVA_ADMIN_PASSWORD` **zorunludur**),
ardından:

```bash
docker compose up -d
```

- Uygulama `8080` portunu dinler (`KLINOVA_PORT` ile değiştirilebilir).
- Veritabanı ve oturum anahtarları `klinova-data` adlı Docker volume'unda kalıcıdır.
- Konteyner root olmayan `app` kullanıcısıyla çalışır.
- Sağlık kontrolü: `GET /health` → `Healthy`.

## Seçenek 2: Linux sunucu (systemd)

1. [ASP.NET Core 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) kurun.
2. Uygulamayı yayımlayın (geliştirme makinesinde veya sunucuda):

   ```bash
   dotnet publish src/Klinova.Web -c Release -o /opt/klinova
   ```

3. Servis kullanıcısı ve veri klasörü oluşturun:

   ```bash
   sudo useradd --system --home /var/lib/klinova --shell /usr/sbin/nologin klinova
   sudo mkdir -p /var/lib/klinova && sudo chown klinova:klinova /var/lib/klinova
   ```

4. `/etc/systemd/system/klinova.service`:

   ```ini
   [Unit]
   Description=Klinova
   After=network.target

   [Service]
   User=klinova
   WorkingDirectory=/opt/klinova
   ExecStart=/usr/bin/dotnet /opt/klinova/Klinova.Web.dll
   Restart=always
   RestartSec=5
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
   Environment=ConnectionStrings__Default=Data Source=/var/lib/klinova/klinova.db
   Environment=Klinova__DataProtectionKeysPath=/var/lib/klinova/keys
   Environment=Klinova__UseHttpsRedirection=false
   # İlk kurulumda bir kez; sonrasında kaldırabilirsiniz:
   # Environment=Klinova__Seed__AdminPassword=...

   [Install]
   WantedBy=multi-user.target
   ```

5. Servisi başlatın: `sudo systemctl enable --now klinova`
6. Önüne Nginx veya Caddy ile HTTPS ekleyin ([aşağıya bakın](#https-ve-ters-vekil-sunucu)).

## Seçenek 3: Windows Server (IIS)

1. [ASP.NET Core 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)'ı kurun.
2. `dotnet publish src/Klinova.Web -c Release -o C:\inetpub\klinova` ile yayımlayın.
3. IIS'te yeni bir site oluşturun, *Uygulama Havuzu* için **.NET CLR sürümü: Yönetilen kod yok** seçin.
4. Uygulama havuzu kimliğine `C:\inetpub\klinova\data` klasöründe yazma izni verin
   (SQLite kullanılıyorsa).
5. Ayarları `appsettings.Production.json` dosyasında (repoya eklenmez) veya IIS
   yapılandırma düzenleyicisindeki ortam değişkenleriyle verin.
6. Siteye bir TLS sertifikası bağlayın.

## SQL Server Kullanımı

```json
{
  "Database": { "Provider": "SqlServer" },
  "ConnectionStrings": {
    "Default": "Server=SUNUCU\\SQLEXPRESS;Database=Klinova;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

- Veritabanı ve tablolar ilk açılışta migration'larla otomatik oluşturulur.
  Bunu kapatmak için `Klinova:ApplyMigrationsOnStartup=false` ayarlayıp şemayı
  `dotnet ef database update` ile kendiniz uygulayabilirsiniz.
- Parola içeren bağlantı cümlelerini dosyaya değil, ortam değişkenine
  (`ConnectionStrings__Default`) veya gizli yönetim aracınıza koyun.
- Docker ile denemek için: `docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d`

## HTTPS ve Ters Vekil Sunucu

Sağlık verisi taşıyan bir uygulama **yalnızca HTTPS** üzerinden yayınlanmalıdır.
Örnek Nginx yapılandırması:

```nginx
server {
    listen 443 ssl http2;
    server_name klinova.kurum.local;

    ssl_certificate     /etc/ssl/certs/klinova.crt;
    ssl_certificate_key /etc/ssl/private/klinova.key;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

Vekil sunucu aynı makinede değilse adresini `Klinova:KnownProxies` listesine ekleyin;
aksi halde istemci IP'si ve HTTPS bilgisi uygulamaya doğru aktarılmaz.

## İlk Giriş

1. Kullanıcı adı `admin` (veya `Klinova:Seed:AdminUserName`).
2. Şifre: yapılandırmada verdiğiniz değer (`Klinova__Seed__AdminPassword` / `KLINOVA_ADMIN_PASSWORD`).
   Bu değer zorunludur; verilmezse uygulama ne yapılması gerektiğini söyleyen bir mesajla durur.
3. Sistem sizden yeni bir şifre belirlemenizi ister.
4. **Kurum Ayarları**'ndan kurum adını, adresini ve makbuz notunu girin.
5. **Kurumlar ve Şubeler**'de varsayılan şubenin adını, ilini ve ilçesini düzenleyin; varsa diğer
   şubeleri ekleyin.
6. **Poliklinikler**, **Hekimler** ve **Fiyat Listesi**'ni kurumunuza göre düzenleyin
   (varsayılan fiyatlar örnek amaçlıdır). Online randevu verilmeyecek poliklinikleri kapatın.
7. **Çalışma Takvimleri**'nden her hekimin haftalık çalışma saatlerini ve randevu süresini girin;
   online randevu ancak bu takvimden üretilen saatler için verilir.
8. **Resmî Tatiller**'e o yılın Ramazan ve Kurban Bayramı tarihlerini ekleyin.
9. SMS ve e-posta sağlayıcınızı [yapılandırın](configuration.md#bildirimler).
10. **Kullanıcılar**'dan personel hesaplarını oluşturun ve hekim hesaplarını
    **Hekimler** ekranında ilgili hekim kaydına bağlayın.
11. Hasta portalının adresini (sitenin ana adresi) hastalarınızla paylaşın.

## Yedekleme ve Geri Yükleme

**SQLite:** `klinova.db` dosyasını uygulama çalışırken güvenle yedeklemek için SQLite'ın
çevrimiçi yedekleme komutunu kullanın:

```bash
sqlite3 /var/lib/klinova/klinova.db ".backup '/yedek/klinova-$(date +%F).db'"
```

Docker kullanıyorsanız:

```bash
docker run --rm -v klinova_klinova-data:/data -v "$PWD":/backup alpine \
  tar czf /backup/klinova-data-$(date +%F).tgz -C /data .
```

`keys` klasörünü de yedekleyin; bu klasör olmadan geri yüklenen sistemde tüm kullanıcıların
yeniden giriş yapması gerekir.

**SQL Server:** Kurumunuzun standart yedekleme planını (tam + fark + işlem günlüğü) uygulayın.

Yedekler sağlık verisi içerir: şifreli ortamda saklayın ve geri yüklemeyi düzenli olarak test edin.

## Güncelleme

1. Yedek alın.
2. Yeni sürümü çekin (`git pull` veya yeni sürüm paketi).
3. Docker: `docker compose up -d --build` · Diğer: yeniden yayımlayıp servisi yeniden başlatın.
4. Veritabanı migration'ları açılışta otomatik uygulanır.

Sürüm notları için [CHANGELOG.md](../CHANGELOG.md)'ye bakın.

## Güvenlik Kontrol Listesi

- [ ] Uygulama yalnızca HTTPS üzerinden erişilebilir; HTTP portu dışarıya kapalı.
- [ ] İlk yönetici şifresi değiştirildi; `Klinova__Seed__AdminPassword` ortam değişkeni kaldırıldı.
- [ ] Demo verisi kapalı (`Klinova:Seed:DemoData=false`).
- [ ] SMS / SMTP kimlik bilgileri dosyada değil, ortam değişkeninde veya gizli yönetim aracında.
- [ ] Hasta portalının aydınlatma metni hukuk birimi tarafından kuruma göre düzenlendi.
- [ ] Birden fazla uygulama sunucusu varsa arka plan işleri yalnızca birinde açık.
- [ ] Veritabanı dosyası / sunucusu ve `keys` klasörü yalnızca servis hesabı tarafından okunabilir.
- [ ] Düzenli, şifreli ve test edilmiş yedekler alınıyor.
- [ ] Her personelin kendi kullanıcı hesabı var; hesaplar paylaşılmıyor, ayrılan personelin hesabı pasifleştiriliyor.
- [ ] Sunucu ve .NET çalışma zamanı güncel; [Dependabot](../.github/dependabot.yml) güncellemeleri takip ediliyor.
- [ ] KVKK kapsamında aydınlatma metni, açık rıza süreçleri, VERBİS kaydı ve veri saklama/imha
      politikası kurum tarafından hazırlanmış.
