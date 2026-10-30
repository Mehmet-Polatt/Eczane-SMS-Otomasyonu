# Eczane Yerel Hatırlatma ve Otomasyon Sistemi
**Sürüm:** 1.0 | **Geliştirme Ortamı:** Mac OS (VS Code) | **Hedef Ortam:** Windows (.exe)

## 1. Proje Özeti
Bu proje, eczanelerde bilgisayar ekranındaki hasta reçete tablolarından görsel verileri okuyan, yerel olarak depolayan ve zamanı geldiğinde internet bağlantısına ihtiyaç duymadan GSM Modem (Donanım) üzerinden SMS gönderen çevrimdışı (offline) bir masaüstü uygulamasıdır.

## 2. Teknoloji Yığını
*   **Programlama Dili:** C# (.NET 8 veya .NET 9)
*   **Kullanıcı Arayüzü (UI):** Avalonia UI (Mac'te geliştirilip Windows'ta çalışabilmesi için)
*   **Veritabanı:** SQLite (Entity Framework Core ile yönetilecek)
*   **OCR (Görüntü İşleme):** Tesseract OCR (Tesseract.NET sarmalayıcısı kullanılacak)
*   **Zamanlayıcı (Arka Plan Görevleri):** Quartz.NET
*   **Donanım İletişimi:** `System.IO.Ports` (Seri port üzerinden AT komutları ile SMS gönderimi)

## 3. Temel İş Akışı
1.  **Tarama (Tetikleme):** Kullanıcı tabloyu ekranda açar ve Avalonia arayüzündeki "Tara" butonuna basar. Sistem ekranın görüntüsünü alır.
2.  **OCR İşlemi:** Görüntüdeki veriler metne çevrilir ve ilgili kolonlar (Telefon, İlaç Adı, İlaç Bitiş, Reçete Bitiş) ayrıştırılır.
3.  **Mükerrer Kontrolü:** `Telefon + İlaç Adı + Reçete Bitiş Tarihi` kombinasyonu veritabanında (SQLite) sorgulanır. Zaten varsa o satır yoksayılır.
4.  **Format Doğrulama:** Yeni kayıtlar format testinden geçer (kurallar aşağıdadır). Kusursuz olanlar doğrudan veritabanına eklenir.
5.  **Manuel Düzeltme Arayüzü:** Sadece şüpheli/hatalı okunan veriler Avalonia arayüzünde bir DataGrid (Tablo) içinde kullanıcıya sunulur. Kullanıcı düzeltip onaylar, veritabanına yazılır.

## 4. Doğrulama (Validation) Kuralları
*   **Telefon:** Sadece rakamlardan oluşmalı. 10 veya 11 hane olmalı (örn: 05XX... veya 5XX...). Harf (O ile 0 karışıklığı vb.) veya özel karakter içeriyorsa "Hatalı" listesine at.
*   **Tarih:** `GG.AA.YYYY`, `GG/AA/YYYY` veya `GG-AA-YYYY` formatında olmalı. Geçerli bir takvim tarihi olmalı (32.13.2024 geçersizdir). Uygun değilse "Hatalı" listesine at.
*   **İsim/İlaç Adı:** Minimum 3 karakter olmalı. Anlamsız semboller (!, +, %, vb.) içeriyorsa "Hatalı" listesine at.

## 5. Arka Plan Görevleri ve SMS (Quartz.NET)
*   Uygulama arka planda çalışırken her gün saat 09:00'da veritabanını tarar.
*   Reçete veya ilaç bitiş tarihine **tam 3 gün kalmış** olan hastaları tespit eder.
*   Bulunan numaralara Seri Port üzerinden (GSM Modem) SMS gönderir.
*   *Geliştirme Notu:* Mac ortamında seri port isimleri `/dev/tty.*` şeklinde, Windows'ta ise `COM3`, `COM4` şeklindedir. Kod, port adını bir konfigürasyon dosyasından veya UI'daki bir ayarlardan okuyacak şekilde dinamik yazılmalıdır.

## 6. Geliştirme Aşamaları (Yapay Zeka İçin Yol Haritası)
1.  **Aşama 1 (Kurulum):** Avalonia projesinin oluşturulması, MVVM yapısının kurulması ve EF Core ile SQLite veritabanı `DbContext` ve `Model` sınıflarının (Hasta, IlacKayit) yazılması.
2.  **Aşama 2 (OCR ve Filtreleme):** Ekran görüntüsü alma fonksiyonunun ve Tesseract entegrasyonunun yapılması. Regex ile metin ayıklama ve validasyon kurallarının sınıflara dökülmesi.
3.  **Aşama 3 (Arayüz):** Ana sayfa, tarama bildirimleri ve "Hatalı Kayıt Düzeltme" (DataGrid) arayüzlerinin Avalonia XAML ile tasarlanması.
4.  **Aşama 4 (Görev Zamanlama ve SMS):** Quartz.NET entegrasyonu ve Seri Port üzerinden SMS gönderme arayüzünün (Interface) hazırlanması (Testleri Windows'ta yapılacak).