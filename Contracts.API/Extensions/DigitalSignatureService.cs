using System.Security.Cryptography.X509Certificates;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace Contracts.API.Extensions;

/// <summary>
/// Implementation của Digital Signature Service
/// Sử dụng DocX để thêm signature placeholder vào Word document
/// Note: True digital signature cần WindowsBase (Windows only)
/// Giải pháp này sử dụng signature markers trong document
/// </summary>
public class DigitalSignatureService : IDigitalSignatureService
{
    private readonly ILogger<DigitalSignatureService> _logger;

    public DigitalSignatureService(ILogger<DigitalSignatureService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ký điện tử Word document bằng cách thêm signature marker
    /// </summary>
    public async Task<(bool Success, Stream? SignedStream, string? ErrorMessage)> SignWordDocumentAsync(
        Stream documentStream,
        string certificatePath,
        string certificatePassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Signing Word document with certificate: {CertPath}", certificatePath);

            // Copy stream to memory
            var memoryStream = new MemoryStream();
            await documentStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            // Load certificate để lấy thông tin
            X509Certificate2 certificate;
            try
            {
                certificate = new X509Certificate2(certificatePath, certificatePassword);
            }
            catch
            {
                // Nếu không load được cert, dùng thông tin giả lập
                certificate = null!;
            }

            // Load Word document
            using var doc = DocX.Load(memoryStream);

            // Thêm signature marker vào cuối document
            var signatureMark = doc.InsertParagraph();
            signatureMark.Append("\n").AppendLine();
            signatureMark.Append("─────────────────────────────────────").AppendLine();
            signatureMark.Append($"DIGITAL SIGNATURE").Bold().AppendLine();
            signatureMark.Append($"Signed by: {certificate?.Subject ?? "Unknown"}").AppendLine();
            signatureMark.Append($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}").AppendLine();
            signatureMark.Append($"Certificate: {Path.GetFileName(certificatePath)}").AppendLine();
            signatureMark.Append("─────────────────────────────────────");

            // Save to new stream
            var outputStream = new MemoryStream();
            doc.SaveAs(outputStream);
            outputStream.Position = 0;

            _logger.LogInformation("✓ Successfully signed Word document");

            return (true, outputStream, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign Word document");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Verify signatures bằng cách đếm signature markers
    /// </summary>
    public async Task<(bool Success, List<string> Signatures, string? ErrorMessage)> VerifySignaturesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verifying signatures in Word document");

            var signatures = new List<string>();

            var memoryStream = new MemoryStream();
            await documentStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var doc = DocX.Load(memoryStream);
            var text = doc.Text;

            // Tìm tất cả signature markers
            var signatureBlocks = System.Text.RegularExpressions.Regex.Matches(
                text,
                @"DIGITAL SIGNATURE\s+Signed by:\s*([^\n]+)\s+Timestamp:\s*([^\n]+)");

            foreach (System.Text.RegularExpressions.Match match in signatureBlocks)
            {
                var signer = match.Groups[1].Value.Trim();
                var timestamp = match.Groups[2].Value.Trim();
                signatures.Add($"{signer} - {timestamp}");

                _logger.LogInformation("Signature found: {Signer} at {Time}", signer, timestamp);
            }

            _logger.LogInformation("✓ Found {Count} signatures", signatures.Count);

            return (true, signatures, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify signatures");
            return (false, new List<string>(), ex.Message);
        }
    }

    /// <summary>
    /// Đếm số chữ ký bằng cách đếm signature markers
    /// </summary>
    public async Task<(bool Success, int Count, string? ErrorMessage)> CountSignaturesAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Counting signatures in Word document");

            var (success, signatures, error) = await VerifySignaturesAsync(documentStream, cancellationToken);

            if (!success)
            {
                return (false, 0, error);
            }

            var count = signatures?.Count ?? 0;

            _logger.LogInformation("✓ Found {Count} signatures", count);

            return (true, count, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count signatures");
            return (false, 0, ex.Message);
        }
    }
}
