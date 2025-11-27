namespace Contracts.API.ContractsHandler.SignContract;

/// <summary>
/// Command để chèn ảnh chữ ký vào hợp đồng
/// </summary>
public record SignContractFromDocumentCommand(
    Guid DocumentId,  // FilledDocumentId from S3
    IFormFile SignatureImage  // Signature image to insert into content control
) : ICommand<SignContractFromDocumentResult>;

/// <summary>
/// Result của việc chèn chữ ký vào hợp đồng
/// </summary>
public record SignContractFromDocumentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? DocumentId { get; init; }
    public string? FileUrl { get; init; }
    public string? FileName { get; init; }
}

internal class SignContractFromDocumentHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    IWordContractService wordContractService,
    EmailHandler emailHandler,
    ILogger<SignContractFromDocumentHandler> logger)
    : ICommandHandler<SignContractFromDocumentCommand, SignContractFromDocumentResult>
{
    public async Task<SignContractFromDocumentResult> Handle(
        SignContractFromDocumentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Inserting signature image for DocumentId: {DocumentId}", request.DocumentId);

            using var connection = await connectionFactory.CreateConnectionAsync();

            // ================================================================
            // BƯỚC 1: LẤY THÔNG TIN DOCUMENT
            // ================================================================
            var document = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                "SELECT * FROM contract_documents WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.DocumentId });

            if (document == null)
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Document {request.DocumentId} not found"
                };
            }

            logger.LogInformation("Found document: {DocumentName}", document.DocumentName);

            // ================================================================
            // BƯỚC 2: DOWNLOAD FILE TỪ S3
            // ================================================================
            var (downloadSuccess, fileStream, downloadError) = await s3Service.DownloadFileAsync(
                document.FileUrl,
                cancellationToken);

            if (!downloadSuccess || fileStream == null)
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = downloadError ?? "Failed to download document from S3"
                };
            }

            logger.LogInformation("Downloaded document from S3: {FileUrl}", document.FileUrl);

            // ================================================================
            // BƯỚC 3: INSERT SIGNATURE IMAGE
            // ================================================================
            logger.LogInformation("Inserting signature image into content control 'DigitalSignature'");

            Stream documentWithSignature;
            try
            {
                using var signatureImageStream = request.SignatureImage.OpenReadStream();

                var (insertSuccess, modifiedStream, insertError) =
                    await wordContractService.InsertSignatureImageAsync(
                        fileStream,
                        "DigitalSignature",
                        signatureImageStream,
                        request.SignatureImage.FileName,
                        cancellationToken);

                fileStream.Dispose();

                if (!insertSuccess || modifiedStream == null)
                {
                    return new SignContractFromDocumentResult
                    {
                        Success = false,
                        ErrorMessage = insertError ?? "Failed to insert signature image into document"
                    };
                }

                documentWithSignature = modifiedStream;
                logger.LogInformation("✓ Signature image inserted successfully");
            }
            catch (Exception ex)
            {
                fileStream.Dispose();
                logger.LogError(ex, "Error inserting signature image");
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to insert signature image: {ex.Message}"
                };
            }

            // ================================================================
            // BƯỚC 4: TẠO ĐƯỜNG DẪN MỚI CHO SIGNED DOCUMENT
            // ================================================================
            // Lấy folder name từ đường dẫn hiện tại
            // VD: contracts/filled/Hợp đồng lao động nhân viên bảo vệ/FILLED_xxx.docx
            // => contracts/signed/Hợp đồng lao động nhân viên bảo vệ/Signed_xxx.docx
            var originalPath = document.FileUrl;
            var pathParts = originalPath.Split('/');

            string folderName = "Hợp đồng khác"; // Default
            string originalFileName = document.DocumentName;

            // Extract folder name từ path
            if (pathParts.Length >= 3 && pathParts[0] == "contracts" && pathParts[1] == "filled")
            {
                // pathParts[2] là folder name (VD: "Hợp đồng lao động nhân viên bảo vệ")
                folderName = pathParts[2];
            }

            // Tạo tên file mới với prefix "Signed_"
            var newFileName = originalFileName.StartsWith("FILLED_")
                ? originalFileName.Replace("FILLED_", "SIGNED_")
                : $"SIGNED_{originalFileName}";

            // Tạo S3 key mới
            var newS3Key = $"contracts/signed/{folderName}/{newFileName}";

            logger.LogInformation("Moving signed document: {OldPath} => {NewPath}", originalPath, newS3Key);

            // ================================================================
            // BƯỚC 5: UPLOAD FILE VÀO ĐƯỜNG DẪN MỚI (SIGNED FOLDER)
            // ================================================================
            var (uploadSuccess, fileUrl, uploadError) = await s3Service.UploadFileWithCustomKeyAsync(
                documentWithSignature,
                newS3Key,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                cancellationToken);

            documentWithSignature.Dispose();

            if (!uploadSuccess || string.IsNullOrEmpty(fileUrl))
            {
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = uploadError ?? "Failed to upload signed document to S3"
                };
            }

            logger.LogInformation("Uploaded signed document to S3: {FileUrl}", fileUrl);

            // ================================================================
            // BƯỚC 6: CẬP NHẬT DATABASE
            // ================================================================
            var updateSql = @"
                UPDATE contract_documents
                SET
                    DocumentType = 'signed_contract',
                    DocumentName = @DocumentName,
                    FileUrl = @FileUrl,
                    Version = @Version,
                    Tokens = NULL,
                    TokenExpiredDay = NULL,
                    CreatedAt = @CreatedAt
                WHERE Id = @Id AND IsDeleted = 0";

            var rowsAffected = await connection.ExecuteAsync(updateSql, new
            {
                Id = document.Id,
                DocumentType = "signed_contract",
                DocumentName = newFileName,
                FileUrl = newS3Key,
                Version = "signed",
                CreatedAt = DateTime.UtcNow
            });

            if (rowsAffected == 0)
            {
                logger.LogWarning("Failed to update document record in database for DocumentId: {DocumentId}", document.Id);
                return new SignContractFromDocumentResult
                {
                    Success = false,
                    ErrorMessage = "Failed to update document record in database"
                };
            }

            logger.LogInformation("✓ Updated database: DocumentId={DocumentId}, NewFileName={FileName}, NewPath={Path}, Version=signed, Tokens=NULL",
                document.Id, newFileName, newS3Key);

            // ================================================================
            // BƯỚC 7: GỬI EMAIL XÁC NHẬN (NẾU CÓ THÔNG TIN CUSTOMER TRONG DATABASE)
            // ================================================================
            if (!string.IsNullOrEmpty(document.DocumentEmail) &&
                !string.IsNullOrEmpty(document.DocumentCustomerName))
            {
                try
                {
                    logger.LogInformation("Sending contract signed confirmation email to {Email}", document.DocumentEmail);

                    // Sử dụng DocumentName làm contract number (hoặc có thể extract từ filename)
                    var contractNumber = ExtractContractNumberFromFileName(document.DocumentName);

                    await emailHandler.SendContractSignedConfirmationEmailAsync(
                        document.DocumentCustomerName,
                        document.DocumentEmail,
                        contractNumber,
                        DateTime.UtcNow,
                        document.Id);

                    logger.LogInformation("✓ Sent confirmation email successfully to {Email}", document.DocumentEmail);
                }
                catch (Exception emailEx)
                {
                    // Email error không làm fail toàn bộ process
                    logger.LogWarning(emailEx, "Failed to send confirmation email to {Email}, but signature was successful", document.DocumentEmail);
                }
            }
            else
            {
                logger.LogInformation("Skipping email notification (missing customer information in document: Email={Email}, Name={Name})",
                    document.DocumentEmail ?? "NULL", document.DocumentCustomerName ?? "NULL");
            }

            return new SignContractFromDocumentResult
            {
                Success = true,
                DocumentId = document.Id,
                FileUrl = fileUrl,
                FileName = newFileName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inserting signature into document");
            return new SignContractFromDocumentResult
            {
                Success = false,
                ErrorMessage = $"Insert signature failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extract contract number từ filename
    /// VD: "FILLED_abc123_HOP_DONG_LAO_DONG_22_11_2025.docx" => "HĐ-2025-abc123"
    /// </summary>
    private string ExtractContractNumberFromFileName(string fileName)
    {
        try
        {
            // Remove extension
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // Try to extract GUID from filename (usually after FILLED_ or SIGNED_)
            var parts = nameWithoutExt.Split('_');
            if (parts.Length > 1)
            {
                // parts[0] = "FILLED" or "SIGNED"
                // parts[1] = GUID or date
                var guidPart = parts[1];

                // Check if it looks like a GUID (has dashes or is 32+ chars)
                if (guidPart.Length >= 8)
                {
                    return $"HĐ-{DateTime.Now.Year}-{guidPart.Substring(0, 8)}";
                }
            }

            // Fallback: use filename as-is
            return nameWithoutExt;
        }
        catch
        {
            // Fallback: return filename
            return fileName;
        }
    }
}