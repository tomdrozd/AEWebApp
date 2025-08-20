namespace ActivityExplorer.Services
{
    public class PurviewSettings
    {
        public string Organization { get; set; } = "yourdomain.onmicrosoft.com";
        public string AppId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string CertificateThumbprint { get; set; } = string.Empty;
        public string CertificateStoreName { get; set; } = "My";
        public string CertificateStoreLocation { get; set; } = "CurrentUser";
    }
}