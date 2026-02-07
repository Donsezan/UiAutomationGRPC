# PowerShell script to generate self-signed certificates for UiAutomationGRPC
$certDir = Join-Path $PSScriptRoot "..\certs"
if (-not (Test-Path $certDir)) {
    New-Item -ItemType Directory -Path $certDir
}

param(
    [string]$Hostname = "localhost"
)

# Check if openssl is available
$openssl = Get-Command openssl -ErrorAction SilentlyContinue

if ($openssl) {
    Write-Host "Using OpenSSL to generate certificates for $Hostname..."
    & openssl genrsa -out "$certDir/ca.key" 4096
    & openssl req -new -x509 -days 3650 -key "$certDir/ca.key" -out "$certDir/ca.crt" -subj "/CN=UiAutomationGRPC-CA"
    & openssl genrsa -out "$certDir/server.key" 4096
    & openssl req -new -key "$certDir/server.key" -out "$certDir/server.csr" -subj "/CN=$Hostname"
    & openssl x509 -req -days 3650 -in "$certDir/server.csr" -CA "$certDir/ca.crt" -CAkey "$certDir/ca.key" -CAcreateserial -out "$certDir/server.crt"
} else {
    Write-Warning "OpenSSL not found. Falling back to New-SelfSignedCertificate (requires Admin)."
    # Fallback to PowerShell's New-SelfSignedCertificate if OpenSSL is not available
    # Note: This creates a certificate in the store and we'd need to export it to PEM for gRPC.
    # For now, we recommend installing OpenSSL or using the provided bash script in a WSL/Git Bash environment.
    Write-Error "Please install OpenSSL for Windows or run this script in an environment where 'openssl' is in the PATH."
}

Write-Host "Certificates generation process completed. Check $certDir"
