using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ISO8583Net.Server;

/// <summary>
/// TLS/SSL configuration for the ISO 8583 TCP server.
/// </summary>
public sealed class TlsOptions
{
    /// <summary>Path to the server certificate PEM file (.crt/.pem).</summary>
    public string? CertPath { get; set; }

    /// <summary>Path to the server private key PEM file (.key/.pem).</summary>
    public string? KeyPath { get; set; }

    /// <summary>Path to the CA certificate PEM file for client certificate validation.</summary>
    public string? CaCertPath { get; set; }

    /// <summary>Whether to require client certificates (mutual TLS).</summary>
    public bool RequireClientCert { get; set; }

    /// <summary>The loaded X509 certificate (populated at startup).</summary>
    internal X509Certificate2? Certificate { get; set; }

    /// <summary>The loaded CA certificate for client validation (populated at startup).</summary>
    internal X509Certificate2? CaCertificate { get; set; }

    /// <summary>Whether TLS is enabled (has both cert and key paths).</summary>
    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(CertPath) && !string.IsNullOrWhiteSpace(KeyPath);

    /// <summary>Loads the server certificate from PEM files.</summary>
    public void LoadCertificate()
    {
        if (!IsEnabled) return;

        Certificate = X509Certificate2.CreateFromPemFile(CertPath!, KeyPath!);

        // Load CA certificate for client cert validation if configured
        if (!string.IsNullOrWhiteSpace(CaCertPath) && System.IO.File.Exists(CaCertPath))
        {
            CaCertificate = new X509Certificate2(CaCertPath);
        }
    }
}
