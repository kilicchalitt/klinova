# Kullanım Kılavuzu

Bu kılavuz Klinova'nın günlük kullanımını rollere göre anlatır.

## Roller

| Rol | Kimler kullanır | Erişebildiği ekranlar |
| --- | --- | --- |
| **Yönetici** | Başhekim, idari sorumlu, bilgi işlem | Tüm ekranlar; kullanıcılar, kurum ayarları, işlem kayıtları |
| **Hasta Kabul** | Danışma / kayıt personeli | Hasta kaydı ve arama, poliklinik kabul, başvurular, vezne |
| **Hekim** | Hekimler | Muayene listesi, başvurular, hasta kartları (salt okunur), muayene kaydı |
| **Vezne** | Vezne / muhasebe personeli | Vezne, raporlar, hasta ve başvuru listeleri (tanı ve muayene notları gizli) |

Bir kullanıcıya birden fazla rol verilebilir (ör. küçük bir kurumda kabul personeli aynı zamanda vezne).

## Günlük İş Akışı

```mermaid
sequenceDiagram
    participant K as Hasta Kabul
    participant H as Hekim
    participant V as Vezne
    K->>K: Hastayı ara / yeni kayıt aç
    K->>K: Poliklinik kabul → sıra numarası, sıra fişi
    H->>H: Muayene Listem → Sıradaki Hastayı Çağır
    H->>H: Şikâyet, tanı, notlar · hizmet / tetkik ekle
    H->>H: Muayeneyi Tamamla
    V->>V: Vezne → Tahsil Et (indirim, ödeme şekli)
    V->>V: Makbuz yazdır
```

### 1. Hasta Kabul

1. **Hastalar** ekranında arama kutusuna ad soyad, dosya numarası, T.C. Kimlik No veya
   telefon yazın. Türkçe karakter fark etmez: *ismail*, *İsmail* ve *ISMAIL* aynı sonucu verir.
2. Hasta kayıtlı değilse **Yeni Hasta Kaydı** ile kaydedin. T.C. Kimlik No girilirse
   algoritmik olarak doğrulanır; aynı numarayla ikinci kayıt açılamaz ve mevcut kaydın
   dosya numarası gösterilir.
3. Alerji ve kronik hastalıkları **Tıbbi Uyarılar** alanına yazın; bu bilgi hekim
   ekranlarında kırmızı uyarı olarak görünür.
4. **Poliklinik Kabul** ekranında polikliniği (ve istenirse hekimi) seçin. Muayene ücreti
   varsayılan olarak seçili gelir. **Kabul Et ve Sıra Fişi Yazdır** ile sıra numarası
   verilir ve fiş yazdırılır.

Sıra numaraları her poliklinik için her gün 1'den başlar. Aynı anda iki masadan kabul
yapılsa bile numaralar çakışmaz.

### 2. Hekim

1. **Muayene Listem** ekranı, kullanıcı hesabınız bir hekim kaydına bağlıysa kendi
   polikliniğinizi otomatik açar.
2. **Sıradaki Hastayı Çağır** en küçük sıra numaralı bekleyen hastayı muayeneye alır.
3. Muayene sayfasında şikâyet, tanı ve muayene notlarını kaydedin; gerekirse tetkik
   veya işlem ekleyin.
4. **Muayeneyi Tamamla** ile hasta veznenin listesine düşer. Hasta gelmediyse
   **Sıraya Geri Al** kullanılabilir.

Tahsilatı yapılmamış tamamlanmış bir başvuru **Yeniden Aç** ile düzeltilebilir.

### 3. Vezne

1. **Vezne** ekranında tahsilat bekleyen başvurular listelenir.
2. **Tahsil Et** ile ödeme şeklini (nakit, kart, havale, sigorta/kurum) ve varsa indirimi girin.
3. Makbuz numarası `MKB-xxxxxxxx` biçiminde otomatik verilir ve A4 makbuz yazdırılabilir.
4. Gün sonunda aynı ekranın alt bölümünde ödeme şekline göre kasa toplamları görünür.

Tahsilatı yapılan başvurunun hizmetleri değiştirilemez; bu, mali kayıtların tutarlılığını korur.

### 4. Raporlar

**Raporlar** ekranında tarih aralığı seçerek (hazır aralıklar: bugün, son 7 gün, bu ay,
geçen ay, bu yıl) net/brüt gelir, indirimler ve başvuru sayılarını; poliklinik, hekim,
hizmet ve ödeme şekli kırılımlarını görebilir, **Excel (CSV) İndir** ile dışa aktarabilirsiniz.

## Yönetim

- **Poliklinikler:** Pasif yapılan poliklinik kabul ekranında görünmez; geçmiş kayıtlar korunur.
- **Hekimler:** Hekimi "Hekim" rolündeki bir kullanıcı hesabına bağlayın.
- **Fiyat Listesi:** Fiyat değişikliği yalnızca sonradan eklenen hizmetlere yansır;
  geçmiş başvurular eklendikleri andaki fiyatı korur.
- **Kullanıcılar:** Yeni hesaplara tek seferlik geçici şifre üretilir. Ayrılan personelin
  hesabını silmek yerine pasifleştirin; açık oturumları birkaç dakika içinde sonlanır.
  Sistemde her zaman en az bir aktif yönetici kalır.
- **İşlem Kayıtları:** Hangi kaydın kim tarafından ne zaman eklendiği/değiştirildiği,
  eski ve yeni değerleriyle listelenir.

## Sık Sorulan Sorular

**Şifremi unuttum.** Yöneticiniz **Kullanıcılar** ekranından şifrenizi sıfırlayabilir.

**Hesabım kilitlendi.** Beş hatalı denemeden sonra hesap 15 dakika kilitlenir. Yönetici
şifre sıfırlayarak kilidi hemen açabilir.

**Hasta yanlışlıkla iki kez kaydedildi.** Kayıtlardan birini arşivleyin. Arşivlenen
hastalar aramalarda varsayılan olarak görünmez ve yeni başvuru açılamaz.
