# Eczane Otomasyon Sistemi

Eczane Otomasyon Sistemi, eczacıların hastalarına ait ilaç ve reçete bitiş tarihlerini takip etmelerine yardımcı olan masaüstü bir asistan uygulamasıdır. Reçete veya belge görüntülerini PDF/OCR iş akışıyla tarayıp tarih ve hasta bilgilerini çıkarmayı, kayıtları yerel olarak saklamayı ve yaklaşan bitiş tarihlerinde SMS hatırlatmaları planlamayı amaçlar. Uygulama verileri SQLite veritabanında yerel olarak tutar; OCR ve temel kayıt işlemleri için çevrimiçi bir servis gerekmez.

> **Durum notu:** SMS zamanlayıcısı şu an geliştirme/test ayarında her dakika çalışacak şekilde yapılandırılmıştır. Üretim kullanımı öncesinde zamanlayıcıyı günlük çalışma aralığına alıp GSM modem ve gerçek SMS gönderimini test edin. OCR, çıkarılan verilerin doğruluğunu garanti etmez; kayıtları onaylamadan önce kontrol edin.

## Özellikler

- Tesseract OCR ile taranmış belge görüntülerinden metin okuma, görüntü ön işleme ve alan ayrıştırma.
- Hasta adı, T.C. kimlik numarası ve ilaç bilgileriyle kayıt doğrulama; mevcut eşleşmelerin/mükerrer kayıtların engellenmesine yönelik korumalar.
- İlaç bitişi ve reçete bitişi için bağımsız hatırlatma koşulları ve SMS şablonları.
- Boş veya geçersiz alanlarla kayıt oluşturulmasını engelleyen doğrulama.
- Ayarlar ekranından düzenlenebilen ilaç ve reçete SMS metinleri.
- Avalonia UI ile dinamik açık/koyu tema.
- Quartz.NET ile arka plan zamanlama; GSM modem SMS servisi ve geliştirme amaçlı debug SMS servisi.
- SQLite ile yerel veri saklama; uygulama temel işlevleri internet bağlantısı olmadan çalışabilir.

## Teknoloji yığını

| Alan | Teknoloji |
| --- | --- |
| Dil ve çalışma zamanı | C# / .NET 8 |
| Masaüstü arayüzü | Avalonia UI 11 |
| Veri erişimi ve veritabanı | Entity Framework Core 8 / SQLite |
| OCR | Tesseract (Türkçe ve İngilizce dil modelleri) |
| Zamanlama | Quartz.NET |
| Görüntü ön işleme | SixLabors.ImageSharp |

## Gereksinimler

- .NET 8 SDK
- Git
- OCR için depodaki `src/EczaneOtomasyonu/tessdata/` klasörü ve `tur.traineddata` (İngilizce OCR da kullanıldığı için `eng.traineddata` da tutulur).
- macOS'ta Tesseract CLI: `brew install tesseract`. Uygulama macOS'ta CLI üzerinden OCR çalıştırır. Diğer platformlarda gerekli Tesseract native bağımlılıkları kurulu olmalıdır.
- Gerçek SMS gönderimi için desteklenen bir GSM modem/SIM ve uygulama ayarlarında seçilen seri port. Debug SMS servisi gerçek mesaj göndermez.

## Kurulum ve çalıştırma

Depoyu klonlayıp çözümü çalıştırın:

```bash
git clone GITHUB_REPO_URL_BURAYA
cd EczaneOtomasyonu
dotnet restore EczaneOtomasyonu.sln
dotnet run --project src/EczaneOtomasyonu/EczaneOtomasyonu.csproj
```

OCR'nin çalışması için `src/EczaneOtomasyonu/tessdata/` klasörünü silmeyin veya yeniden adlandırmayın. Uygulama `tur.traineddata` dosyasını öncelikle çalıştırılabilir dosyanın yanındaki `tessdata/` dizininde arar. Depodan çalıştırırken mevcut klasör proje içindedir. Dağıtım çıktısında model dosyalarının da bulunduğunu doğrulayın; gerekirse `tessdata` klasörünü publish klasörüne kopyalayın.

Release çıktısı üretmek için:

```bash
dotnet publish src/EczaneOtomasyonu/EczaneOtomasyonu.csproj -c Release -o ./publish
```

Çalıştırılan işletim sistemine uygun hedef runtime belirtmek isterseniz örneğin `-r win-x64` veya `-r osx-arm64` ekleyin. Tesseract native bağımlılıkları ve macOS CLI gereksinimi hedef platforma göre ayrıca karşılanmalıdır.

## Veri ve gizlilik

SQLite veritabanı kaynak kod deposunda tutulmaz. Uygulama veritabanını işletim sisteminin yerel uygulama verisi dizinindeki `EczaneOtomasyonu/eczane.db` konumunda oluşturur. SMS debug servisi masaüstüne `sms_log.txt` yazabilir. Bu dosyalar `.gitignore` içinde hariç tutulur. Hasta verisi içeren veritabanı, OCR belgeleri veya logları depoya eklemeyin.

## Proje yapısı

```text
EczaneOtomasyonu.sln
src/EczaneOtomasyonu/
  Data/           EF Core ve SQLite
  Infrastructure/ Quartz başlatma ve yaşam döngüsü
  Jobs/           SMS hatırlatma işi
  Models/         Hasta, ilaç kaydı ve ayarlar
  Services/       OCR, doğrulama, SMS ve diyalog servisleri
  Views/          Avalonia ekranları
  ViewModels/     MVVM ekran modelleri
  tessdata/       Tesseract dil modelleri
```

## Geliştirme notları

Quartz zamanlayıcısının test ayarı her dakika çalışır. Kodda üretim için günlük 09:00 cron tetikleyicisi de açıklama olarak yer alır; üretime geçmeden önce uygun tetikleyiciyi etkinleştirin. SMS gönderimini gerçek modemle doğrulamadan hastalara mesaj gönderilecek bir ortamda kullanmayın.

## Lisans

Bu depoya lisans dosyası eklenene kadar proje için açık kaynak lisansı tanımlanmış sayılmaz.
