namespace Contracts.API.ContractsHandler.SignContract;

public record SignContractFromDocumentCommand(
    Guid DocumentId,  
    IFormFile SignatureImage 
) : ICommand<SignContractFromDocumentResult>;

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
            
            var originalPath = document.FileUrl;
            var pathParts = originalPath.Split('/');

            string folderName = "Hợp đồng khác";
            string originalFileName = document.DocumentName;

            if (pathParts.Length >= 3 && pathParts[0] == "contracts" && pathParts[1] == "filled")
            {
                folderName = pathParts[2];
            }
            var newFileName = originalFileName.StartsWith("FILLED_")
                ? originalFileName.Replace("FILLED_", "SIGNED_")
                : $"SIGNED_{originalFileName}";
            
            var newS3Key = $"contracts/signed/{folderName}/{newFileName}";

            logger.LogInformation("Moving signed document: {OldPath} => {NewPath}", originalPath, newS3Key);
            
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
            
            var vietnamTime = DateTimeExtensions.GetVietnamTime();

            var updateSql = @"
                UPDATE contract_documents
                SET
                    DocumentType = 'signed_contract',
                    DocumentName = @DocumentName,
                    FileUrl = @FileUrl,
                    Version = @Version,
                    Tokens = NULL,
                    TokenExpiredDay = NULL,
                    SignDate = @SignDate,
                    CreatedAt = @CreatedAt
                WHERE Id = @Id AND IsDeleted = 0";

            var rowsAffected = await connection.ExecuteAsync(updateSql, new
            {
                Id = document.Id,
                DocumentType = "signed_contract",
                DocumentName = newFileName,
                FileUrl = newS3Key,
                Version = "signed",
                SignDate = vietnamTime,
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

            logger.LogInformation("✓ Updated database: DocumentId={DocumentId}, NewFileName={FileName}, NewPath={Path}, Version=signed, SignDate={SignDate}, Tokens=NULL",
                document.Id, newFileName, newS3Key, vietnamTime);
            
            if (!string.IsNullOrEmpty(document.DocumentEmail) &&
                !string.IsNullOrEmpty(document.DocumentCustomerName))
            {
                try
                {
                    logger.LogInformation("Sending contract signed confirmation email to {Email}", document.DocumentEmail);
                    
                    var contractNumber = ExtractContractNumberFromFileName(document.DocumentName);

                    await emailHandler.SendContractSignedConfirmationEmailAsync(
                        document.DocumentCustomerName,
                        document.DocumentEmail,
                        contractNumber,
                        DateTime.UtcNow,
                        newS3Key);

                    logger.LogInformation("✓ Sent confirmation email successfully to {Email}", document.DocumentEmail);
                }
                catch (Exception emailEx)
                {
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
    
    private string ExtractContractNumberFromFileName(string fileName)
    {
        try
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var parts = nameWithoutExt.Split('_');
            if (parts.Length > 1)
            {
                var guidPart = parts[1];
                if (guidPart.Length >= 8)
                {
                    return $"HĐ-{DateTime.Now.Year}-{guidPart.Substring(0, 8)}";
                }
            }
            return nameWithoutExt;
        }
        catch
        {
            return fileName;
        }
    }
}