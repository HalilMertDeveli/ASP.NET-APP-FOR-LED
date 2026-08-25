#Requires -Version 5.1
<#
.SYNOPSIS
  LedTeknikDestek destek formu için user-secrets yapılandırması.
.NOTES
  Gerçek anahtarları bu script'e yazmayın; etkileşimli sorulur.
#>
$ErrorActionPreference = "Stop"
$web = Join-Path $PSScriptRoot "..\src\LedSupport.Web"
Set-Location $web

Write-Host "UserSecretsId project: LedSupport.Web" -ForegroundColor Cyan

$resend = Read-Host "Resend API Key (re_...)"
if ([string]::IsNullOrWhiteSpace($resend)) { throw "Resend API Key gerekli" }

dotnet user-secrets set "Resend:ApiKey" $resend
dotnet user-secrets set "Resend:ToEmail" "halilmertdeveliii@gmail.com"
dotnet user-secrets set "Resend:FromEmail" "LED Teknik Destek <onboarding@resend.dev>"
dotnet user-secrets set "Support:Mode" "Direct"

$cred = Read-Host "Firebase service account JSON tam yolu (boş bırakılabilir; Development'ta RequireFirestore=false)"
if (-not [string]::IsNullOrWhiteSpace($cred)) {
  if (-not (Test-Path $cred)) { throw "Dosya bulunamadı: $cred" }
  dotnet user-secrets set "Firebase:CredentialsPath" $cred
  dotnet user-secrets set "Support:RequireFirestore" "true"
} else {
  dotnet user-secrets set "Support:RequireFirestore" "false"
  Write-Host "Uyarı: Firestore kapalı. Sadece e-posta gönderilir (Development)." -ForegroundColor Yellow
}

Write-Host "`nMevcut secrets:" -ForegroundColor Green
dotnet user-secrets list
Write-Host "`nVisual Studio'da uygulamayı yeniden başlatın (F5)." -ForegroundColor Cyan
