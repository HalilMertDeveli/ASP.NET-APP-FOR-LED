# Destek formu — teşhis ve kurulum

## Teşhis (bu hatanın nedeni)

Mesaj: `Talebiniz şu an iletilemedi. Lütfen doğrudan e-posta veya telefon ile ulaşın.`

**Kök neden:** `FirebaseSupport:SubmitUrl` / `IngestSecret` (eski Function modu) veya `Resend:ApiKey` placeholder (`YOUR_...`) / boş.

Doğrulama:
```bash
cd src/LedSupport.Web
dotnet user-secrets list
```
Boşsa veya `YOUR_` içeriyorsa form bilinçli olarak reddedilir (istemciye secret sızdırılmaz; loglara yazılır).

Ek bulgular (`ledteknikdestek-1e74e`):
1. **Blaze/billing kapalı** → Cloud Functions + Secret Manager kullanılamıyor.
2. **Firestore veritabanı henüz yok** (API/billing) → kayıt için Console’da DB oluşturulmalı.
3. Web app oluşturuldu (public config `Firebase:WebApiKey`).

## Önerilen yol: Direct (Blaze zorunlu değil e-posta için)

ASP.NET → Resend e-posta + (opsiyonel) Firestore Admin SDK.

### 1) Resend
1. https://resend.com → API Key al
2. Test için `onboarding@resend.dev` gönderen kullanılabilir
3. Production’da kendi domain’inizi doğrulayın

### 2) User secrets
```powershell
cd E:\LED-SUPPORT\scripts
powershell -ExecutionPolicy Bypass -File .\configure-support-secrets.ps1
```
veya:
```bash
cd src/LedSupport.Web
dotnet user-secrets set "Resend:ApiKey" "re_xxxxxxxx"
dotnet user-secrets set "Resend:ToEmail" "halilmertdeveliii@gmail.com"
dotnet user-secrets set "Resend:FromEmail" "LED Teknik Destek <onboarding@resend.dev>"
dotnet user-secrets set "Support:Mode" "Direct"
```

### 3) Firestore (kalıcı kayıt)
1. https://console.firebase.google.com/project/ledteknikdestek-1e74e/usage/details → **Blaze / billing aç**
2. Build → Firestore → Create database (örn. `eur3`)
3. Project settings → Service accounts → **Generate new private key**
4. JSON’u Git dışı bir yere koy (örn. `C:\secrets\ledteknikdestek-sa.json`)
5. ```bash
   dotnet user-secrets set "Firebase:CredentialsPath" "C:\secrets\ledteknikdestek-sa.json"
   dotnet user-secrets set "Support:RequireFirestore" "true"
   ```
6. `firestore.rules` yayınla:
   ```bash
   npx -y firebase-tools@latest deploy --only firestore:rules --project ledteknikdestek-1e74e
   ```

Development’ta `RequireFirestore=false` → sadece e-posta ile form çalışır (Firestore sonraya bırakılabilir).

## Function mode (opsiyonel, Blaze şart)

Secrets: `RESEND_API_KEY`, `SUPPORT_INGEST_SECRET`, `SUPPORT_NOTIFY_EMAIL`, `SUPPORT_FROM_EMAIL`  
`Support:Mode=Function` + `FirebaseSupport:SubmitUrl` / `IngestSecret`

## Güvenlik
- Service account JSON, Resend key, SMTP parolası → **Git’e ekleme**
- User-secrets / env / Secret Manager kullan
