# Eczane Otomasyonu: Doğrulama, Güvenlik ve İş Kuralları Başucu Rehberi

Bu doküman, projede yer alan tüm veri kaydetme, OCR analizi, mükerrer kayıt engelleme, manuel veri düzenleme ve veritabanı kısıtlamalarının mimari haritasını sunar. Gelecekte yeni bir kural eklemek veya mevcut bir kuralı güncellemek istediğinizde doğrudan ilgili dosya ve metotlara müdahale edebilmeniz için hazırlanmıştır.

---

## 📑 İçindekiler
1. [Merkezi Doğrulama Servisi (Validation Service)](#1-merkezi-doğrulama-servisi-validation-service)
2. [OCR Metin Ayrıştırma ve Girdi Temizleme (OCR Parser)](#2-ocr-metin-ayrıştırma-ve-girdi-temizleme-ocr-parser)
3. [Tarama Sonrası Mükerrer Kontrolü ve Ayıklama (Scan Processing)](#3-tarama-sonrası-mükerrer-kontrolü-ve-ayıklama-scan-processing)
4. [Düzeltilen Kayıtların Veritabanına Yazılması (Tarama Kaydetme)](#4-düzeltilen-kayıtların-veritabanına-yazılması-tarama-kaydetme)
5. [Kayıtlı Hastalar: Manuel Hücre Düzenleme ve Toplu Kaydetme](#5-kayıtlı-hastalar-manuel-hücre-düzenleme-ve-toplu-kaydetme)
6. [Kayıtlı Hastalar: Bağımlı Silme Mantığı (Orphan Cleanup)](#6-kayıtlı-hastalar-bağımlı-silme-mantığı-orphan-cleanup)
7. [Arayüz (XAML) Hücre Kilitleri ve İzinleri](#7-arayüz-xaml-hücre-kilitleri-ve-izinleri)
8. [Veritabanı Şeması ve EF Core Düzeyindeki Kısıtlamalar](#8-veritabanı-şeması-ve-ef-core-düzeyindeki-kısıtlamalar)
9. [Gelecekte Yeni Bir Kural Eklemek İçin Yol Haritası](#9-gelecekte-yeni-bir-kural-eklemek-için-yol-haritası)

---

## 1. Merkezi Doğrulama Servisi (Validation Service)

Tüm iş kuralları ve format validasyonlarının ana motorudur. Diğer servisler ve ViewModel'lar doğrulamayı buradan çağırır.

### 1.1. Telefon Numarası Doğrulaması
* **Kural:** Boş veya salt boşluk olamaz. `^\d{10,11}$` regex'ine tam uymalıdır (yalnızca rakam, 10 veya 11 hane: örn. `5XXXXXXXXX` veya `05XXXXXXXXX`).
* **Senaryo Sonucu:**
  * **Geçerli:** `true` döner, hata string'i boştur.
  * **Geçersiz:** `false` döner; `out string hata` parametresine *"Telefon sadece rakam içermeli ve 10-11 hane olmalı."* açıklaması verilir.
* **Tam Konum ve Katman:**
  * **Katman:** Servis Katmanı
  * **Dosya:** [ValidationService.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/ValidationService.cs#L26-L42)
  * **Metot:** `ValidationService.TelefonGecerliMi(string telefon, out string hata)`

### 1.2. Tarih Formatı ve Takvim Geçerliliği
* **Kural:** `^(\d{2})[.\-/](\d{2})[.\-/](\d{4})$` regex'i ile `GG.AA.YYYY`, `GG/AA/YYYY` veya `GG-AA-YYYY` formatında olmalıdır. Ayrıca takvimsel olarak geçerli bir gün/ay kombinasyonu olmalıdır (örn: 31 Şubat veya 13. ay girilemez).
* **Senaryo Sonucu:**
  * **Geçerli:** `true` döner ve `DateOnly tarih` nesnesi üretilir.
  * **Geçersiz:** `false` döner; *"Geçerli bir tarih formatı değil"* veya *"Geçersiz takvim tarihi"* hatası üretilir.
* **Tam Konum ve Katman:**
  * **Katman:** Servis Katmanı
  * **Dosya:** [ValidationService.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/ValidationService.cs#L47-L80)
  * **Metot:** `ValidationService.TarihGecerliMi(string tarihMetin, out DateOnly tarih, out string hata)`

### 1.3. İlaç Adı Doğrulaması
* **Kural:** Boş olamaz, en az 3 karakter olmalıdır. Yasaklı özel karakterleri içermemelidir: `[!+%@#$^&*()=[]{}|\\<>]`.
* **Senaryo Sonucu:**
  * **Geçerli:** `true` döner.
  * **Geçersiz:** `false` döner; karakter sayısı yetersizse veya yasak sembol varsa ilgili açıklama döner.
* **Tam Konum ve Katman:**
  * **Katman:** Servis Katmanı
  * **Dosya:** [ValidationService.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/ValidationService.cs#L85-L108)
  * **Metot:** `ValidationService.IlacAdiGecerliMi(string ilacAdi, out string hata)`

### 1.4. Bütünleşik Kayıt ve Mantıksal Tarih Sıralaması
* **Kural:** Telefon, İlaç Adı, İlaç Bitiş Tarihi ve Reçete Bitiş Tarihi kurallarının tamamı geçerli olmalıdır. Ek olarak **Reçete Bitiş Tarihi >= İlaç Bitiş Tarihi** olmalıdır (ilaç bitmeden reçete bitemez mantığı).
* **Senaryo Sonucu:**
  * **Geçerli:** `true` döner.
  * **Geçersiz:** `false` döner; hatalar birleştirilerek tek bir açıklama metni oluşturulur.
* **Tam Konum ve Katman:**
  * **Katman:** Servis Katmanı
  * **Dosya:** [ValidationService.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/ValidationService.cs#L113-L138)
  * **Metot:** `ValidationService.KaydiDogrula(...)`

---

## 2. OCR Metin Ayrıştırma ve Girdi Temizleme (OCR Parser)

Tesseract OCR'dan dönen ham koordinat ve metin satırlarının filtrelenerek sütunlara ayrıştırıldığı bölümdür.

### 2.1. Çöp Satır ve Tablo Başlığı Eleme
* **Kural:** Satırda geçerli bir telefon, 11 haneli TC kimlik veya tarih deseni bulunmuyorsa, satır tablo başlığı veya anlamsız metin kabul edilir.
* **Senaryo Sonucu:**
  * Satır pas geçilir (`continue`), `OcrSatirSonucu` listesine eklenmez.
* **Tam Konum ve Katman:**
  * **Katman:** Servis Katmanı
  * **Dosya:** [OcrMetinAyristirici.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/OcrMetinAyristirici.cs#L58-L60)
  * **Metot:** `OcrMetinAyristirici.Ayristir(...)`

### 2.2. TC Kimlik Tespiti ve Telefon Çakışma Önleme
* **Kural:** `(?<!\d)\d{11}(?!\d)` regex deseni aranır. Eğer satırda yakalanan telefon da 11 haneli ise (05... gibi), telefon numarası ile aynı olan değer TC kimlik olarak atanmaz.
* **Senaryo Sonucu:**
  * Eşleşen 11 haneli değer `OcrSatirSonucu.TcKimlik` propertysine aktarılır.
* **Tam Konum ve Katman:**
  * **Katman:** Servis Katmanı
  * **Dosya:** [OcrMetinAyristirici.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/OcrMetinAyristirici.cs#L41-L46)
  * **Metot:** `OcrMetinAyristirici.Ayristir(...)`

### 2.3. OCR Karakter Düzeltme (Tesseract Optik Hata Giderme)
* **Kural:** Telefon numarası okunurken sıkça '0' (sıfır) yerine 'O', '1' (bir) yerine 'l' veya 'I' okunabilir.
* **Senaryo Sonucu:**
  * `satir.Replace('O', '0').Replace('l', '1').Replace('I', '1')` ile sayısallaştırma yapılır.
* **Tam Konum ve Katman:**
  * **Katman:** Servis Katmanı
  * **Dosya:** [OcrMetinAyristirici.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/OcrMetinAyristirici.cs#L96-L100)
  * **Metot:** `OcrMetinAyristirici.TelefonAdaylariniDuzelt(string satir)`

---

## 3. Tarama Sonrası Mükerrer Kontrolü ve Ayıklama (Scan Processing)

Ekran görüntüsü tarandıktan sonra kayıtların doğrudan veritabanındaki kayıtlarla karşılaştırıldığı ve geçerli/hatalı listelerine ayrıldığı adımdır.

### 3.1. Üçlü Şart Mükerrerlik Kontrolü
* **Kural:** Her bir OCR satırı, veritabanındaki kayıtlarla **İsim Soyisim + TC Kimlik + İlaç Adı** 3'lü kombinasyonu üzerinden karşılaştırılır (`StringComparison.OrdinalIgnoreCase` ve `Trim()`).
* **Senaryo Sonucu:**
  * **Eşleşme VARSA:** Kullanıcı bu kaydı daha önce düzeltip kaydetmiştir. OCR çıktısında telefon vb. eksik kalsa bile kayıt **Hatalı Kayıtlar listesine DÜŞÜRÜLMEZ**, `mukerrerSayac++` artırılarak tamamen elenir.
  * **Eşleşme YOKSA:** Kayıt doğrulama adımına aktarılır.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel
  * **Dosya:** [MainWindowViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/MainWindowViewModel.cs#L192-L215)
  * **Metot:** `MainWindowViewModel.TaraAsync()`

### 3.2. Hatalı Kayıtları Ayırma
* **Kural:** Mükerrer olmayan kayıt, `ValidationService.KaydiDogrula` kontrolünden geçirilir.
* **Senaryo Sonucu:**
  * **Geçersizse:** `HataliKayitlar` ObservableCollection'ına eklenir. `HataNedeni` hücresine sebepleri kırmızı metinle yazılır.
  * **Geçerliyse:** Kuyruğa (`_gecerliKayitlar`) alınır.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel
  * **Dosya:** [MainWindowViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/MainWindowViewModel.cs#L217-L239)
  * **Metot:** `MainWindowViewModel.TaraAsync()`

---

## 4. Düzeltilen Kayıtların Veritabanına Yazılması (Tarama Kaydetme)

Kullanıcının ana sayfadaki DataGrid'de kırmızı alanları elle düzeltip `✓ Düzeltilenleri Kaydet` butonuna bastığı akıştır.

### 4.1. Yeniden Doğrulama (Re-Validation)
* **Kural:** `HataliKayitlar` listesindeki her satır tekrar `ValidationService.KaydiDogrula` ile kontrol edilir.
* **Senaryo Sonucu:**
  * **Hâlâ Geçersizse:** Satır tablodan silinmez, `HataNedeni` güncellenir ve kullanıcıya bırakılır.
  * **Düzeltilmişse:** `_gecerliKayitlar` listesine taşınır ve ekrandan kaldırılır.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel
  * **Dosya:** [MainWindowViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/MainWindowViewModel.cs#L313-L345)
  * **Metot:** `MainWindowViewModel.KaydetAsync()`

### 4.2. Hasta Upsert (Eşleştirme ve Güncelleme)
* **Kural:** Hasta veritabanında aranırken önce `TcKimlik` ile, TC boşsa `Telefon` ile aranır.
* **Senaryo Sonucu:**
  * Hasta yoksa yeni `Hasta` entity'si açılır.
  * Varsa isim ve TC kimlik bilgisi güncellenir.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel
  * **Dosya:** [MainWindowViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/MainWindowViewModel.cs#L351-L370)
  * **Metot:** `MainWindowViewModel.KaydetAsync()`

### 4.3. İlaç Mükerrer Kayıt Engeli
* **Kural:** `HastaId + IlacAdi + ReceteBitisTarihi` kombinasyonu veritabanında sorgulanır.
* **Senaryo Sonucu:**
  * Varsa o ilaç kaydı tekrar eklenmez (`continue`), yoksa `IlacKayit` tablosuna yazılır.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel
  * **Dosya:** [MainWindowViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/MainWindowViewModel.cs#L373-L390)
  * **Metot:** `MainWindowViewModel.KaydetAsync()`

---

## 5. Kayıtlı Hastalar: Manuel Hücre Düzenleme ve Toplu Kaydetme

Kayıtlı Hastalar sekmesinde Excel benzeri serbest hücre düzenlemelerinin kontrol edildiği ve veritabanına aktarıldığı akıştır.

### 5.1. TC Kimlik Format Kontrolü
* **Kural:** Eğer hücredeki TC Kimlik alanı doluysa, kesinlikle 11 karakter olmalı ve yalnızca rakamlardan (`All(char.IsDigit)`) oluşmalıdır.
* **Senaryo Sonucu:**
  * **Hatalıysa:** 
    1. `DialogService.UyariGosterAsync` ile ekranda modal hata penceresi açılır: *"İşlem İptal Edildi: [Hasta] adlı hastanın TC Kimlik numarası 11 haneli ve sadece rakamlardan oluşmalıdır!"*
    2. `await KayitlariYukleAsync()` tetiklenerek tablodaki hatalı veri silinir ve veritabanındaki orijinal temiz haline geri döner (**Revert**).
    3. Metot anında `return` edilir; veritabanına hiçbir şey yazılmaz.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel & Servis
  * **Dosya:** [KayitliHastalarViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/KayitliHastalarViewModel.cs#L165-L179)
  * **Metot:** `KayitliHastalarViewModel.DegisiklikleriKaydetAsync()`

### 5.2. Telefon Numarası Format Kontrolü
* **Kural:** Eğer hücredeki Telefon alanı doluysa, boşluklar temizlendikten sonra 10 veya 11 hane olmalı ve yalnızca rakamlardan (`All(char.IsDigit)`) oluşmalıdır.
* **Senaryo Sonucu:**
  * **Hatalıysa:** 
    1. `DialogService.UyariGosterAsync` ile ekranda modal hata penceresi açılır: *"İşlem İptal Edildi: [Hasta] adlı hastanın telefon numarası geçersiz! (10 veya 11 haneli rakam olmalıdır)"*
    2. `await KayitlariYukleAsync()` çağrılarak tablo orijinal haline geri döner (**Revert**).
    3. Metot anında `return` edilir.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel & Servis
  * **Dosya:** [KayitliHastalarViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/KayitliHastalarViewModel.cs#L182-L198)
  * **Metot:** `KayitliHastalarViewModel.DegisiklikleriKaydetAsync()`

### 5.3. Tarih ve İlaç Güncelleme
* **Kural:** Hücredeki tarihler `ValidationService.TarihGecerliMi` kontrolünden geçerse `DateOnly` olarak güncellenir; geçersiz bir tarih formatı yazılmışsa eski tarih korunur.
* **Senaryo Sonucu:**
  * `await db.SaveChangesAsync()` çağrılır. Ardından liste arka planda yeniden yüklenerek durum onaylanır.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel
  * **Dosya:** [KayitliHastalarViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/KayitliHastalarViewModel.cs#L220-L255)
  * **Metot:** `KayitliHastalarViewModel.DegisiklikleriKaydetAsync()`

---

## 6. Kayıtlı Hastalar: Bağımlı Silme Mantığı (Orphan Cleanup)

Tablodan bir satır silindiğinde veritabanında öksüz (ilaçsız) hasta kalmasını önleyen mekanizmadır.

* **Kural:** Kullanıcı `Sil` butonuna bastığında `IlacKayit` silinir. Ardından veritabanında hastanın başka herhangi bir ilacı kalıp kalmadığı kontrol edilir (`!db.IlacKayitlari.Any(x => x.HastaId == satir.HastaId)`).
* **Senaryo Sonucu:**
  * Hastanın başka ilacı kalmadıysa, ana `Hasta` kaydı da veritabanından tamamen silinir.
* **Tam Konum ve Katman:**
  * **Katman:** Backend / ViewModel
  * **Dosya:** [KayitliHastalarViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/KayitliHastalarViewModel.cs#L115-L133)
  * **Metot:** `KayitliHastalarViewModel.KayitSilAsync(...)`

---

## 7. Arayüz (XAML) Hücre Kilitleri ve İzinleri

Kullanıcının istemeden sistem alanlarını (ID, sistem logları, SMS durumu vb.) değiştirmesini engelleyen UI düzeyindeki korumalardır.

| Ekran | Sütun | Özellik | İzin Durumu |
| :--- | :--- | :--- | :--- |
| **Kayıtlı Hastalar** | İsim Soyisim, TC Kimlik, Telefon | DataGridTextColumn | `IsReadOnly="False"` (Düzenlenebilir) |
| **Kayıtlı Hastalar** | İlaç Adı | DataGridTemplateColumn | `IsReadOnly="False"` (TwoWay TextBox) |
| **Kayıtlı Hastalar** | İlaç Bitiş, Reçete Bitiş | DataGridTextColumn | `IsReadOnly="False"` (Düzenlenebilir) |
| **Kayıtlı Hastalar** | Kayıt Tarihi | DataGridTextColumn | **`IsReadOnly="True"`** (Kilitli) |
| **Kayıtlı Hastalar** | SMS Durumu | DataGridTemplateColumn | **`IsReadOnly="True"`** (Kilitli) |
| **Kayıtlı Hastalar** | İşlem (Sil Butonu) | DataGridTemplateColumn | **`IsReadOnly="True"`** (Kilitli) |
| **Hatalı Kayıtlar** | Hata Nedeni | DataGridTemplateColumn | **`IsReadOnly="True"`** (Kilitli) |

* **Konumlar:**
  * [KayitliHastalarView.axaml](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Views/KayitliHastalarView.axaml#L143-L225)
  * [MainWindow.axaml](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Views/MainWindow.axaml#L300-L368)

---

## 8. Veritabanı Şeması ve EF Core Düzeyindeki Kısıtlamalar

Kod düzeyindeki kontroller aşılsa dahi SQLite veritabanı motorunun veri bütünlüğünü koruduğu son savunma hattıdır.

### 8.1. Benzersiz İndeksler (Unique Indexes)
* **Telefon Benzersizliği:** `Hasta.Telefon` üzerinde Unique Index tanımlıdır. Aynı telefon numarasıyla iki hasta var olamaz.
* **İlaç + Reçete Benzersizliği:** `IlacKayit` üzerinde `{ HastaId, IlacAdi, ReceteBitisTarihi }` kombinasyonunda Unique Index tanımlıdır.
* **Konum:** [EczaneDbContext.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Data/EczaneDbContext.cs#L37-L66)

### 8.2. Karakter Uzunlukları ve Zorunlu Alanlar
* `Hasta.IsimSoyisim`: `IsRequired()`, `MaxLength(200)`
* `Hasta.Telefon`: `IsRequired()`, `MaxLength(11)`
* `Hasta.TcKimlik`: `MaxLength(11)`
* `IlacKayit.IlacAdi`: `IsRequired()`, `MaxLength(200)`
* `IlacKayit.HastaId`: `DeleteBehavior.Cascade` (Hasta silindiğinde ilaçlar otomatik temizlenir)
* **Konum:** [EczaneDbContext.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Data/EczaneDbContext.cs#L25-L67)

### 8.3. Dinamik Şema Geçişleri (Runtime Column Migration)
* Uygulama açılışında eski SQLite veritabanlarında `IsimSoyisim` veya `TcKimlik` sütunları yoksa `PRAGMA table_info` ile tespit edilip `ALTER TABLE` ile otomatik eklenir.
* **Konum:** [EczaneDbContextFactory.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Data/EczaneDbContextFactory.cs#L36-L105)

---

## 9. Gelecekte Yeni Bir Kural Eklemek İçin Yol Haritası

1. **Yeni bir format/mantık kuralı ekleyecekseniz (Örn: TC Algoritması kontrolü veya Reçete No doğrulaması):**
   * Doğrudan [ValidationService.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/Services/ValidationService.cs) içine yeni static metodunuzu ekleyin ve `KaydiDogrula` metoduna bağlayın.
2. **Kayıtlı Hastalar tablosundaki kaydetme kontrolünü genişletecekseniz:**
   * Doğrudan [KayitliHastalarViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/KayitliHastalarViewModel.cs#L156) içindeki `DegisiklikleriKaydetAsync` metodunun başındaki doğrulama döngüsüne yeni `if` bloğunuzu ekleyin.
3. **Mükerrerlik eşleşme alanlarını değiştirecekseniz:**
   * [MainWindowViewModel.cs](file:///Users/mehmetpolat/Desktop/EczaneOtomasyonu/src/EczaneOtomasyonu/ViewModels/MainWindowViewModel.cs#L192) içindeki `TaraAsync` LINQ sorgusunu güncelleyin.
