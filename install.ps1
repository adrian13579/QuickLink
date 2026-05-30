#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Builds and installs QuickLink on this machine.
.PARAMETER Platform
    Target CPU architecture: x64 (default), x86, or ARM64.
.PARAMETER Configuration
    Build configuration: Release (default) or Debug.
.EXAMPLE
    .\install.ps1
    .\install.ps1 -Platform ARM64
    .\install.ps1 -Configuration Debug
#>
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = $(switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64'   { 'x64'   }
        'X86'   { 'x86'   }
        'Arm64' { 'ARM64' }
        default { 'x64'   }
    }),

    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot   = $PSScriptRoot
$projectFile = "$repoRoot\src\QuickLink.csproj"
$certSubject = 'CN=AppPublisher'

# ── 1. Build ──────────────────────────────────────────────────────────────────
Write-Host "`n[1/4] Building ($Configuration | $Platform)..." -ForegroundColor Cyan

dotnet build $projectFile `
    -c $Configuration `
    -p:Platform=$Platform `
    -p:AppxPackageSigningEnabled=false `
    -p:GenerateAppxPackageOnBuild=true `
    --verbosity minimal

if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

# ── 2. Locate MSIX ───────────────────────────────────────────────────────────
Write-Host "`n[2/4] Locating MSIX package..." -ForegroundColor Cyan

$msix = Get-ChildItem -Recurse -Filter '*.msix' `
    -Path "$repoRoot\src\bin" `
    -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msix) { throw "No .msix file found under src\bin. Build may not have produced a package." }
Write-Host "  Found: $($msix.FullName)" -ForegroundColor Gray

# ── 3. Certificate ───────────────────────────────────────────────────────────
Write-Host "`n[3/4] Preparing signing certificate..." -ForegroundColor Cyan

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $certSubject -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "  Creating self-signed certificate ($certSubject)..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Subject        $certSubject `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -Type           CodeSigningCert `
        -KeyUsage       DigitalSignature `
        -KeyExportPolicy Exportable `
        -FriendlyName   'QuickLink Dev Cert' `
        -NotAfter       (Get-Date).AddYears(10)
}

Write-Host "  Certificate thumbprint: $($cert.Thumbprint)" -ForegroundColor Gray

# Trust the cert in LocalMachine Root and TrustedPeople so the MSIX can be installed
foreach ($storeName in @('Root', 'TrustedPeople')) {
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $storeName,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine
    )
    $store.Open('ReadWrite')
    if (-not ($store.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
        Write-Host "  Trusting cert in LocalMachine\$storeName..." -ForegroundColor Yellow
        $store.Add($cert)
    }
    $store.Close()
}

# Sign with signtool.exe (from Windows SDK)
$signtool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' `
    -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.DirectoryName -match '\\x64$' } |
    Sort-Object -Descending |
    Select-Object -First 1

if ($signtool) {
    Write-Host "  Signing MSIX..." -ForegroundColor Yellow
    & $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint $msix.FullName
    if ($LASTEXITCODE -ne 0) { throw "signtool failed (exit $LASTEXITCODE)." }
} else {
    Write-Warning "signtool.exe not found. Attempting unsigned install (requires Developer Mode)."
}

# ── 4. Install ────────────────────────────────────────────────────────────────
Write-Host "`n[4/4] Installing..." -ForegroundColor Cyan

Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown

Write-Host "`nDone. 'QuickLink' is installed — find it in Start or Windows Search." -ForegroundColor Green
