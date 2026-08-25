# LED Teknik Destek

Colorlight, NovaStar ve Huidu LED ekran kontrol sistemleri için teknik destek, AnyDesk ile uzaktan bağlantı ve destek talep formu sunan web sitesi.

![Ana Sayfa](docs/screenshots/home.png)
![Hizmetler](docs/screenshots/services.png)
![İletişim Formu](docs/screenshots/contact.png)

## Özellikler

- Responsive LED/neon tasarım
- Colorlight, NovaStar ve Huidu desteği
- AnyDesk ile uzaktan teknik destek
- Firebase Firestore ile destek talebi kaydı
- Firebase Functions ve Resend ile e-posta bildirimi
- SEO uyumlu ASP.NET Core Razor Pages yapısı

## Teknolojiler

- ASP.NET Core 8 Razor Pages
- Firebase Firestore
- Firebase Cloud Functions
- Resend
- HTML, CSS ve JavaScript

## Proje yapısı

```text
src/LedSupport.Web/          # Razor Pages uygulaması
  wwwroot/images/            # LED / panel / kontrol görselleri (WebP)
functions/                   # Cloud Functions (TypeScript + Resend)
firestore.rules              # İstemci okuma/yazma kapalı
docs/                        # Kurulum ve ekran görüntüleri
legacy/                      # Eski MVC uygulama (referans)
```

## Yerel çalıştırma

Gereksinim: .NET SDK 8+

```bash
dotnet restore src/LedSupport.Web/LedSupport.Web.csproj
dotnet run --project src/LedSupport.Web --launch-profile http
```

Adres: http://localhost:5052

## Firebase / e-posta ayarları

Destek formu, Firestore kaydı ve Resend e-posta kurulumu için:

**[docs/FIREBASE_SUPPORT_SETUP.md](docs/FIREBASE_SUPPORT_SETUP.md)**

Hızlı secret yapılandırması:

```powershell
cd scripts
powershell -ExecutionPolicy Bypass -File .\configure-support-secrets.ps1
```

## Güvenlik notu

Gerçek API anahtarları, Resend / Firebase secret’ları, servis hesabı JSON dosyaları ve SMTP parolaları **bu repoya eklenmez**.  
Yalnızca `.env.example` ve dokümantasyondaki placeholder örnekleri commit edilir. Gizli değerleri `dotnet user-secrets`, ortam değişkenleri veya Firebase Secret Manager ile yönetin.

## Lisans

Özel / ticari kullanım — ekip içi proje.
