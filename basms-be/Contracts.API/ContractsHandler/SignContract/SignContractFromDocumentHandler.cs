namespace Contracts.API.ContractsHandler.SignContract;

/// <summary>
/// Command để ký điện tử hợp đồng từ DocumentId hoặc Token (S3-based workflow)
/// </summary>
public record SignContractFromDocumentCommand(
    Guid? DocumentId = null,  // FilledDocumentId from S3 (admin mode)
    string? Token = null,  // Security token (user mode)
    IFormFile? CertificateFile = null,  // File PFX upload (optional)
    string? CertificatePassword = null  // Password for PFX (optional)
) : ICommand<SignContractFromDocumentResult>;

/// <summary>
/// Result của việc ký hợp đồng từ S3 document
/// </summary>
public record SignContractFromDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? SignedDocumentId { get; init; }
    public string? SignedFileUrl { get; init; }
    public string? SignedFileName { get; init; }
}

internal class SignContractFromDocumentHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    IDigitalSignatureService signatureService,
    IConfiguration configuration,
    ILogger<SignContractFromDocumentHandler> logger)
    : ICommandHandler<SignContractFromDocumentCommand, SignContractFromDocumentResult>
{
    public async Task<SignContractFromDocumentResult> Handle(
        SignContractFromDocumentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Starting sign contract - DocumentId: {DocumentId}, HasToken: {HasToken}",
                request.DocumentId, !string.IsNullOrEmpty(request.Token));

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY THÔNG TIN FILLED DOCUMENT (VIA DOCUMENTID HOẶC TOKEN)
            // ================================================================
            ContractDocument? filledDoc = null;

            if (!string.IsNullOrWhiteSpace(request.Token))
            {
                // MODE 1: Ký bằng Token (người dùng qua link email)
                logger.LogInformation("Signing with token");

                filledDoc = await connection.QueryFirstOrDefaultAsync<ContractDocument>(@"
                    SELECT * FROM contract_documents
                    WHERE Tokens = @Token AND IsDeleted = 0
                ", new { Token = request.Token });

                if (filledDoc == null)
                {
                    return new SignContractFromDocumentResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid security token or document not found"
                    };
                }

                // Validate token expiry
                if (filledDoc.TokenExpiredDay < DateTime.UtcNow)
                {
                    return new SignContractFromDocumentResult
                    {
                        Success = false,
                        ErrorMessage = $"Security token has expired on {filledDoc.TokenExpiredDay:yyyy-MM-dd HH:mm:ss} UTC"
                    };
                }

                logger.LogInformation("Token validated successfully. Document: {DocumentId}", filledDoc.Id);
            }
            else if (request.DocumentId.HasValue)
            {
                // MODE 2: Ký bằng DocumentId (admin mode)
                logger.LogInformation("Signing with documentId: {DocumentId}", request.DocumentId);

                filledDoc = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                    "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = request.DocumentId.Value });

                if (filledDoc == null)
                {
                    return new SignContractFromDocumentResult
                    {
                        Success = false,
                        ErrorMessage = $"Filled document {request.DocumentId} not found"
                    };
                }
            }
            else
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Either DocumentId or Token must be provided"
                };
            }

            logger.LogInformation("Filled document: {DocumentName}", filledDoc.DocumentName);

            // ================================================================
            // BƯỚC 2: DOWNLOAD FILLED FILE TỪ S3
            // ================================================================
            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                filledDoc.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download filled document from S3"
                };
            }

            logger.LogInformation("Downloaded filled document from S3: {FileUrl}", filledDoc.FileUrl);

            // ================================================================
            // BƯỚC 3: LẤY CERTIFICATE (ƯU TIÊN FILE UPLOAD, SAU ĐÓ CONFIG)
            // ================================================================
            string certPath;
            string certPassword;

            if (request.CertificateFile != null && !string.IsNullOrEmpty(request.CertificatePassword))
            {
                // Sử dụng certificate từ file upload
                logger.LogInformation("Using uploaded certificate file: {FileName}", request.CertificateFile.FileName);

                // Save temp file
                var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
                using (var tempFileStream = File.Create(tempPath))
                {
                    await request.CertificateFile.CopyToAsync(tempFileStream, cancellationToken);
                }

                certPath = tempPath;
                certPassword = request.CertificatePassword;
            }
            else
            {
                // Sử dụng certificate từ configuration
                certPath = configuration["SignatureCertificate:Path"]
                    ?? Environment.GetEnvironmentVariable("SIGNATURE_CERT_PATH")
                    ?? string.Empty;

                certPassword = configuration["SignatureCertificate:Password"]
                    ?? Environment.GetEnvironmentVariable("SIGNATURE_CERT_PASSWORD")
                    ?? string.Empty;

                if (string.IsNullOrEmpty(certPath) || string.IsNullOrEmpty(certPassword))
                {
                    fileStream.Dispose();
                    return new SignContractFromDocumentResult
                    {
                        Success = false,
                        ErrorMessage = "Certificate not provided. Please upload certificate file or configure SignatureCertificate in appsettings.json"
                    };
                }

                logger.LogInformation("Using certificate from config: {CertPath}", certPath);
            }

            // ================================================================
            // BƯỚC 4: KÝ DOCUMENT
            // ================================================================
            var (signSuccess, signedStream, signError) = await signatureService.SignWordDocumentAsync(
                fileStream,
                certPath,
                certPassword,
                cancellationToken);

            fileStream.Dispose();

            // Clean up temp file if uploaded
            if (request.CertificateFile != null && File.Exists(certPath))
            {
                try
                {
                    File.Delete(certPath);
                    logger.LogInformation("Deleted temporary certificate file");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete temporary certificate file: {Path}", certPath);
                }
            }

            if (!signSuccess || signedStream == null)
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = signError ?? "Failed to sign document"
                };
            }

            logger.LogInformation("Successfully signed document");

            // ================================================================
            // BƯỚC 5: GENERATE SIGNED FILENAME VÀ FOLDER PATH
            // ================================================================
            // FILLED_HOP_DONG_XXX_16_01_2025.docx -> SIGNED_HOP_DONG_XXX_16_01_2025.docx
            var signedFileName = GenerateSignedFileName(filledDoc.DocumentName);

            // contracts/filled/Hợp đồng dịch vụ bảo vệ/ -> contracts/signed/Hợp đồng dịch vụ bảo vệ/
            var signedFolderPath = DetermineSignedFolderPath(filledDoc.FileUrl);

            logger.LogInformation("Target path: {FolderPath}/{FileName}", signedFolderPath, signedFileName);

            // ================================================================
            // BƯỚC 6: UPLOAD SIGNED FILE LÊN S3
            // ================================================================
            var signedS3Key = $"{signedFolderPath}/{signedFileName}";
            var (uploadSuccess, signedUrl, uploadError) = await s3Service.UploadFileWithCustomKeyAsync(
                signedStream,
                signedS3Key,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                cancellationToken);

            signedStream.Dispose();

            if (!uploadSuccess || string.IsNullOrEmpty(signedUrl))
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = uploadError ?? "Failed to upload signed document to S3"
                };
            }

            logger.LogInformation("Uploaded signed document to S3: {S3Url}", signedUrl);

            // ================================================================
            // BƯỚC 7: TẠO RECORD TRONG CONTRACT_DOCUMENTS CHO FILE ĐÃ KÝ
            // ================================================================
            var signedDocumentId = Guid.NewGuid();
            var signedDocument = new ContractDocument
            {
                Id = signedDocumentId,
                DocumentName = signedFileName,
                FileUrl = signedS3Key,  // LƯU S3 KEY THAY VÌ FULL URL để tránh encoding issues
                FileSize = 0,
                DocumentType = "signed_contract", // Loại document: file đã ký
                Version = "signed", // Trạng thái: đã ký
                UploadedBy = filledDoc.UploadedBy,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            await connection.InsertAsync(signedDocument);
            logger.LogInformation("Created ContractDocument record for signed file: {DocumentId} with S3 key: {S3Key}",
                signedDocumentId, signedS3Key);

            // ================================================================
            // BƯỚC 8: CẬP NHẬT FILLED DOCUMENT → SIGNED_DOCUMENT & XÓA TOKEN
            // ================================================================
            await connection.ExecuteAsync(@"
                UPDATE contract_documents
                SET DocumentType = 'signed_document',
                    Version = 'signed',
                    Tokens = NULL,
                    TokenExpiredDay = NULL
                WHERE Id = @DocumentId
            ", new { DocumentId = filledDoc.Id });

            logger.LogInformation(
                "Updated filled document {DocumentId} to signed_document and cleared tokens",
                filledDoc.Id);

            // ================================================================
            // BƯỚC 9: CẬP NHẬT CONTRACT VỚI SIGNED DOCUMENT ID (nếu có)
            // ================================================================
            // Lấy ContractId từ filled document (nếu có)
            var contractId = await connection.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT Id FROM contracts WHERE DocumentId = @FilledDocumentId AND IsDeleted = 0 LIMIT 1",
                new { FilledDocumentId = request.DocumentId });

            if (contractId.HasValue && contractId.Value != Guid.Empty)
            {
                await connection.ExecuteAsync(
                    @"UPDATE contracts
                      SET ContractFileUrl = @FileUrl,
                          DocumentId = @SignedDocumentId,
                          UpdatedAt = @UpdatedAt
                      WHERE Id = @ContractId",
                    new
                    {
                        FileUrl = signedUrl,
                        SignedDocumentId = signedDocumentId,
                        UpdatedAt = DateTime.UtcNow,
                        ContractId = contractId.Value
                    });

                logger.LogInformation(
                    "Updated contract {ContractId} with signed file URL and DocumentId {DocumentId}",
                    contractId.Value, signedDocumentId);
            }
            else
            {
                logger.LogInformation("No contract found linked to filled document - skipping contract update");
            }

            return new SignContractFromDocumentResult
            {
                Success = true,
                SignedDocumentId = signedDocumentId,
                SignedFileUrl = signedUrl,
                SignedFileName = signedFileName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error signing contract from document");
            return new SignContractFromDocumentResult
            {
                Success = false,
                ErrorMessage = $"Sign contract failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate signed filename từ filled filename
    /// FILLED_HOP_DONG_XXX_16_01_2025.docx -> SIGNED_HOP_DONG_XXX_16_01_2025.docx
    /// </summary>
    private string GenerateSignedFileName(string filledFileName)
    {
        if (filledFileName.StartsWith("FILLED_", StringComparison.OrdinalIgnoreCase))
        {
            return "SIGNED_" + filledFileName.Substring(7); // Remove "FILLED_" and add "SIGNED_"
        }

        // Fallback: nếu không bắt đầu với FILLED_, thêm SIGNED_ vào đầu
        return "SIGNED_" + filledFileName;
    }

    /// <summary>
    /// Xác định signed folder path từ filled S3 key hoặc URL
    /// contracts/filled/Hợp đồng dịch vụ bảo vệ/FILLED_XXX.docx -> contracts/signed/Hợp đồng dịch vụ bảo vệ/
    /// </summary>
    private string DetermineSignedFolderPath(string filledFileUrlOrKey)
    {
        try
        {
            // Extract S3 key (support both URL and key)
            string s3Key;
            if (filledFileUrlOrKey.StartsWith("http://") || filledFileUrlOrKey.StartsWith("https://"))
            {
                // Full URL: extract key from URL
                var uri = new Uri(filledFileUrlOrKey);
                s3Key = uri.AbsolutePath.TrimStart('/');
            }
            else
            {
                // Already an S3 key
                s3Key = filledFileUrlOrKey;
            }

            // Parse key: contracts/filled/Hợp đồng dịch vụ bảo vệ/FILLED_XXX.docx
            var parts = s3Key.Split('/');
            var contractsIndex = Array.FindIndex(parts, p => p == "contracts");

            if (contractsIndex >= 0 && parts.Length > contractsIndex + 2)
            {
                // Get category folder (e.g., "Hợp đồng dịch vụ bảo vệ")
                var categoryFolder = parts[contractsIndex + 2];
                return $"contracts/signed/{categoryFolder}";
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse folder path from S3 key/URL: {Value}", filledFileUrlOrKey);
        }

        // Fallback: default folder
        return "contracts/signed/Hợp đồng khác";
    }
}