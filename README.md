# LED Teknik Destek

Colorlight, NovaStar ve Huidu LED ekran kontrol sistemleri için teknik destek, AnyDesk ile uzaktan bağlantı ve destek talep formu sunan modern web sitesi.

Canlı: [asp-net-app-for-led.vercel.app](https://asp-net-app-for-led.vercel.app)

---

## Özellikler

| | |
|:--|:--|
| **Tasarım** | Responsive LED/neon arayüz |
| **Markalar** | Colorlight, NovaStar ve Huidu desteği |
| **Uzaktan** | AnyDesk ile uzaktan teknik destek |
| **Kayıt** | Supabase PostgreSQL (`support_messages`) |
| **E-posta** | Resend → `halilmertdeveliii@gmail.com` (Reply-To = müşteri) |
| **SEO** | ASP.NET Core Razor Pages, meta / sitemap |

## Teknolojiler

- ASP.NET Core 8 Razor Pages  
- Supabase (PostgREST + PostgreSQL)  
- Resend  
- Vercel Container Runtime  

## Destek formu akışı

1. Müşteri `/Contact` formunu doldurur  
2. ASP.NET Core doğrular  
3. Kayıt `support_messages` tablosuna yazılır  
4. Resend ile mail gönderilir (`Reply-To` = müşteri e-postası)  
5. Mail sonucu kayıtta `email_sent` / `status` olarak güncellenir  

## Yerel çalıştırma

```bash
dotnet restore src/LedSupport.Web/LedSupport.Web.csproj
dotnet run --project src/LedSupport.Web --launch-profile http
```

Adres: **http://localhost:5052**

### Gerekli secret’lar

`.env.example` dosyasına bakın. Gerçek değerleri user-secrets veya ortam değişkeni olarak verin:

- `Supabase__Url` / `Supabase__ServiceRoleKey`
- `Resend__ApiKey`
- `Resend__ToEmail` (varsayılan: `halilmertdeveliii@gmail.com`)

```powershell
cd scripts
powershell -ExecutionPolicy Bypass -File .\configure-support-secrets.ps1
```

### Supabase tablo

SQL: [`docs/supabase/support_messages.sql`](docs/supabase/support_messages.sql) — Supabase SQL Editor’de bir kez çalıştırın.

## Proje yapısı

```text
src/LedSupport.Web/     # Razor Pages + services
docs/supabase/          # SQL şema
docs/screenshots/       # README görselleri
legacy/                 # Eski MVC referans kodu
```

## Güvenlik

Gerçek API anahtarları ve service_role key repoya **eklenmez**. Yalnızca `.env.example` ve placeholder örnekleri commit edilir. Supabase service_role yalnızca sunucuda kullanılır.

## Lisans

Özel / ticari kullanım — ekip içi proje.
