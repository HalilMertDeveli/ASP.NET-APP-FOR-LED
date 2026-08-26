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
dotnet user-secrets set "Support:RequireStore" "true"

$supabaseUrl = Read-Host "Supabase project URL (https://xxxx.supabase.co)"
$supabaseKey = Read-Host "Supabase service_role key (server-only)"
if ([string]::IsNullOrWhiteSpace($supabaseUrl) -or [string]::IsNullOrWhiteSpace($supabaseKey)) {
  throw "Supabase URL ve service_role key gerekli"
}

dotnet user-secrets set "Supabase:Url" $supabaseUrl
dotnet user-secrets set "Supabase:ServiceRoleKey" $supabaseKey

$anon = Read-Host "Supabase anon / publishable key (browser-safe)"
if (-not [string]::IsNullOrWhiteSpace($anon)) {
  dotnet user-secrets set "Supabase:PublishableKey" $anon
}

Write-Host "`nMevcut secrets:" -ForegroundColor Green
dotnet user-secrets list
Write-Host "`nSupabase SQL Editor'de docs/supabase/support_messages.sql dosyasını çalıştırın." -ForegroundColor Yellow
Write-Host "Visual Studio'da uygulamayı yeniden başlatın (F5)." -ForegroundColor Cyan
