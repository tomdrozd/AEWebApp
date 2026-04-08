namespace ActivityExplorer.Core.DTOs
{
    public class AuthStatusDto
    {
        // Prerequisites (out-of-process checks)
        public bool IsPwshAvailable { get; set; }
        public string? PwshVersion { get; set; }
        public bool AreScriptsFound { get; set; }
        public List<string> MissingScripts { get; set; } = new List<string>();

        public bool IsModuleInstalled { get; set; }
        public string? ModuleVersion { get; set; }
        public string? ModuleInstallCommand { get; set; }
        public string? ModulePath { get; set; }
        public List<string> SearchedPaths { get; set; } = new List<string>();
        public string? PowerShellVersion { get; set; }
        
        public bool IsCertificateFound { get; set; }
        public string? CertificateThumbprint { get; set; }
        public string? CertificateSubject { get; set; }
        public DateTime? CertificateExpiry { get; set; }
        public bool IsCertificateExpired { get; set; }
        public string? CertificateStore { get; set; }
        
        public bool IsConfigurationValid { get; set; }
        public List<string> ConfigurationErrors { get; set; } = new List<string>();
        
        public bool CanConnect { get; set; }
        public string? ConnectionTestResult { get; set; }
        public string? LastConnectionError { get; set; }
        
        public Dictionary<string, string> ConfiguredValues { get; set; } = new Dictionary<string, string>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }
}