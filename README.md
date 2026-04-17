# Web.HMD - LED Servis ve Operasyon Platformu

Bu proje, LED ekran servis operasyonlarini yonetmek, destek dosyalarini saklamak ve kullanici/admin akislarini tek panelde toplamak icin gelistirilmis bir ASP.NET Core MVC uygulamasidir.

## Neler Var?

- Kullanici kayit/giris (Cookie Authentication)
- Google ve Facebook ile sosyal giris
- LED servis kartlari, detay sayfasi ve sepet akis
- Admin tarafinda panel dosyasi yukleme/import
- SQL Server uzerinde dosya icerigi (binary) saklama
- Katmanli ve genisletilebilir Onion Architecture

## Mimari (Onion Architecture)

Proje katmanlari su sekilde ayrilmistir:

- `Web.HMD`  
  Sunum katmani (MVC Controller, View, UI)

- `LedApp.Application`  
  Is kurallari ve uygulama servisleri (`AuthService`, `PanelSupportService` vb.)

- `LedApp.Domain`  
  Cekirdek model, entity ve interface tanimlari

- `LedApp.Infrastructure`  
  Veri erisimi, EF Core `DbContext`, repository implementasyonlari, DI altyapisi

> Temel kural: `Web` katmani dogrudan veritabani yerine `Application` servisleri ile konusur.

## Teknolojiler

- .NET 10 / ASP.NET Core MVC
- Entity Framework Core + SQL Server
- Cookie Authentication
- Google/Facebook Authentication
- BCrypt (sifre hashleme)

## Proje Yapisi

```text
Web.HMD/
  Web.HMD/                 # MVC app
  LedApp.Application/      # Application services + DTO
  LedApp.Domain/           # Domain entities + interfaces
  LedApp.Infrastructure/   # EF Core + Repository + DI
  LedApp.API/              # API projesi (opsiyonel/gelistirilebilir)
  Entity.HMD/              # Ek entity/config kalintilari
```

## Kurulum

### 1) Gereksinimler

- .NET SDK 10
- SQL Server (LocalDB veya SQL Server Instance)

### 2) Ayarlar

`Web.HMD/appsettings.json` icinde:

- `ConnectionStrings:LedAppDb`
- `PanelLibrary:RootPath`
- `Authentication:Google:*`

> Canli ortamda bu degerleri `appsettings.Production.json` veya environment variable ile yonetin.

### 3) Migration / Database

Ornek komutlar:

```bash
dotnet ef database update --project LedApp.Infrastructure --startup-project Web.HMD
```

### 4) Calistirma

```bash
dotnet run --project Web.HMD
```

## Admin Dosya Yukleme Akisi

Admin panelde 3 ana yol vardir:

1. **Tekli Yukleme** (`Upload`)  
   `pValue + chipset + decoder + dosya` ile kayit/guncelleme

2. **Coklu Yukleme** (`ScanUpload`)  
   Ayni kombinasyon icin birden fazla dosya

3. **Toplu Import** (`ImportFromLibrary`)  
   Klasor kutuphanesinden SQL Server'a aktarim

### Desteklenen klasor pattern'leri

- `p2.5+1065s+2012`
- `p2.5-1065s-2012`
- `p2.5_1065s_2012`
- `p2.5/1065s-2012` (p bilgisi ust klasorden alinabilir)

Desteklenen dosyalar: `.rcvp`, `.hex`  
Dosya icerigi `PanelSupportFiles.FileContent` alaninda binary olarak saklanir.

## Guvenlik Notlari

- Sifreler hashli saklanir (BCrypt)
- Plain text sifre tutulmaz
- Sosyal giriste provider sifresi alinmaz, sistem tarafinda guvenli random parola uretilip hashlenir

## Gelistirme Notlari

- UI tarafi `wwwroot/css/site.css` icinde merkezi yonetilir.
- Yeni admin/servis modulleri icin once `Application` servisleri olusturup controller'i ince tutmaniz onerilir.
- Onion yapisini korumak icin `Web` katmaninda dogrudan EF Core kullanmayin.

## Lisans

Bu proje ekip ici/ozel kullanim amacli gelistirilmistir.
