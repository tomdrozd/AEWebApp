# Import Exchange Online Certificate
$pfxPath = "C:\Users\DrozdTomas(KPCSCZ)\OneDrive\Home_online\PowerShell\!Certs\ActivityExplorerEXO-CBAcert.pfx"

# Prompt for password
$password = Read-Host -AsSecureString "Enter PFX password"

try {
    # Import the certificate
    $cert = Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $password -Exportable
    
    Write-Host "Certificate imported successfully!" -ForegroundColor Green
    Write-Host "Thumbprint: $($cert.Thumbprint)"
    Write-Host "Subject: $($cert.Subject)"
    
    # Test the private key access
    Write-Host "`nTesting private key access..."
    $key = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    if ($key) {
        Write-Host "Private key is accessible!" -ForegroundColor Green
        
        # Test Connect-IPPSSession
        Write-Host "`nTesting Connect-IPPSSession..."
        Connect-IPPSSession -CertificateThumbprint $cert.Thumbprint -AppId "f88ad2f0-dd16-4b4f-ab8b-5061605bd939" -Organization "drozdovo.cz"
        
        Write-Host "Connection successful!" -ForegroundColor Green
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}