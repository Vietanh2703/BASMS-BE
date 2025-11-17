using Contracts.API.ContractsHandler.FillContractTemplate;
using Contracts.API.ContractsHandler.SignContract;
using Contracts.API.ContractsHandler.CheckContractSignatures;
using Contracts.API.ContractsHandler.ImportContractFromDocument;
using Contracts.API.ContractsHandler.ImportWorkingContract;
using Contracts.API.ContractsHandler.ImportManagerWorkingContract;
using Contracts.API.Extensions;
using MediatR;

namespace Contracts.API.BackgroundJobs;

/// <summary>
/// Loại hợp đồng để xác định handler phù hợp
/// </summary>
public enum ContractType
{
    ServiceContract,           // Hợp đồng dịch vụ bảo vệ
    WorkingContractEmployee,   // Hợp đồng lao động nhân viên bảo vệ
    WorkingContractManager     // Hợp đồng lao động quản lý
}

/// <summary>
/// Background Job để xử lý contract tự động - THAY THẾ AWS Lambda
/// Chạy ngay trong API, không cần deploy riêng
/// </summary>
public class ContractProcessingJob
{
    private readonly ISender _mediator;
    private readonly ILogger<ContractProcessingJob> _logger;
    private readonly IS3Service _s3Service;
    private readonly IDbConnectionFactory _connectionFactory;

    public ContractProcessingJob(
        ISender mediator,
        ILogger<ContractProcessingJob> logger,
        IS3Service s3Service,
        IDbConnectionFactory connectionFactory)
    {
        _mediator = mediator;
        _logger = logger;
        _s3Service = s3Service;
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Process contract workflow: Fill → Sign → Verify → Import
    /// </summary>
    public async Task<ContractProcessingJobResult> ProcessContractAsync(
        Guid templateDocumentId,
        Dictionary<string, object>? data = null,
        IFormFile? certificateFile = null,
        string? certificatePassword = null)
    {
        try
        {
            _logger.LogInformation("=== CONTRACT PROCESSING JOB START ===");
            _logger.LogInformation("Template: {TemplateId}", templateDocumentId);

            // ============================================
            // STEP 1: FILL CONTRACT TEMPLATE
            // ============================================
            _logger.LogInformation("[1/4] Filling contract template...");

            var fillCommand = new FillContractFromTemplateCommand(
                TemplateDocumentId: templateDocumentId,
                Data: data
            );

            var fillResult = await _mediator.Send(fillCommand);

            if (!fillResult.Success)
            {
                throw new Exception($"Fill failed: {fillResult.ErrorMessage}");
            }

            _logger.LogInformation("✓ [1/4] Complete - Filled File: {FileName}", fillResult.FilledFileName);

            // ============================================
            // STEP 2: SIGN FILLED CONTRACT
            // ============================================
            _logger.LogInformation("[2/4] Signing contract...");

            var signCommand = new SignContractFromDocumentCommand(
                DocumentId: fillResult.FilledDocumentId!.Value,
                CertificateFile: certificateFile,
                CertificatePassword: certificatePassword
            );

            var signResult = await _mediator.Send(signCommand);

            if (!signResult.Success)
            {
                throw new Exception($"Sign failed: {signResult.ErrorMessage}");
            }

            _logger.LogInformation("✓ [2/4] Complete - Signed File: {FileName}", signResult.SignedFileName);

            // ============================================
            // STEP 3: CHECK SIGNATURES
            // ============================================
            _logger.LogInformation("[3/4] Verifying signatures...");

            var checkCommand = new CheckContractSignaturesCommand(
                DocumentId: signResult.SignedDocumentId!.Value
            );

            var checkResult = await _mediator.Send(checkCommand);

            if (!checkResult.Success)
            {
                throw new Exception($"Signature verification failed: {checkResult.ErrorMessage}");
            }

            _logger.LogInformation("✓ [3/4] Complete - {Count} signature(s) verified", checkResult.SignatureCount);

            // ============================================
            // STEP 4: IMPORT CONTRACT DATA (FLEXIBLE HANDLER SELECTION)
            // ============================================
            _logger.LogInformation("[4/4] Importing contract data...");

            // Xác định handler phù hợp dựa trên tên file
            var contractType = DetermineContractType(fillResult.FilledFileName);
            _logger.LogInformation("Detected contract type: {ContractType}", contractType);

            object importResult;
            string? contractNumber = null;
            Guid? contractId = null;

            switch (contractType)
            {
                case ContractType.WorkingContractEmployee:
                    _logger.LogInformation("Using ImportWorkingContractHandler...");
                    var workingCommand = new ImportWorkingContractCommand(
                        DocumentId: signResult.SignedDocumentId!.Value
                    );
                    var workingResult = await _mediator.Send(workingCommand);
                    if (!workingResult.Success)
                    {
                        throw new Exception($"Import working contract failed: {workingResult.ErrorMessage}");
                    }
                    contractNumber = workingResult.ContractNumber;
                    contractId = workingResult.ContractId;
                    importResult = workingResult;
                    break;

                case ContractType.WorkingContractManager:
                    _logger.LogInformation("Using ImportManagerWorkingContractHandler...");
                    var managerCommand = new ImportManagerWorkingContractCommand(
                        DocumentId: signResult.SignedDocumentId!.Value
                    );
                    var managerResult = await _mediator.Send(managerCommand);
                    if (!managerResult.Success)
                    {
                        throw new Exception($"Import manager contract failed: {managerResult.ErrorMessage}");
                    }
                    contractNumber = managerResult.ContractNumber;
                    contractId = managerResult.ContractId;
                    importResult = managerResult;
                    break;

                case ContractType.ServiceContract:
                default:
                    _logger.LogInformation("Using ImportContractFromDocumentHandler...");
                    var serviceCommand = new ImportContractFromDocumentCommand(
                        DocumentId: signResult.SignedDocumentId!.Value
                    );
                    var serviceResult = await _mediator.Send(serviceCommand);
                    if (!serviceResult.Success)
                    {
                        throw new Exception($"Import service contract failed: {serviceResult.ErrorMessage}");
                    }
                    contractNumber = serviceResult.ContractNumber;
                    contractId = serviceResult.ContractId;
                    importResult = serviceResult;
                    break;
            }

            _logger.LogInformation("✓ [4/4] Complete - Contract: {ContractNumber}", contractNumber);

            // ============================================
            // STEP 5: CLEANUP & FINALIZE FILES
            // ============================================
            _logger.LogInformation("[5/5] Cleaning up and finalizing files...");

            string? finalFileUrl = null;

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();

                // Lấy thông tin filled document để xóa
                var filledDoc = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                    "SELECT * FROM contract_documents WHERE Id = @Id",
                    new { Id = fillResult.FilledDocumentId });

                // Lấy thông tin signed document để rename/move
                var signedDoc = await connection.QueryFirstOrDefaultAsync<ContractDocument>(
                    "SELECT * FROM contract_documents WHERE Id = @Id",
                    new { Id = signResult.SignedDocumentId });

                if (filledDoc != null && signedDoc != null && !string.IsNullOrEmpty(contractNumber))
                {
                    // 5.1: Xóa hoàn toàn file FILLED từ S3 và database
                    _logger.LogInformation("Deleting filled file: {FileUrl}", filledDoc.FileUrl);
                    var deleteFilledSuccess = await _s3Service.DeleteFileAsync(filledDoc.FileUrl);

                    if (deleteFilledSuccess)
                    {
                        _logger.LogInformation("✓ Deleted filled file from S3");

                        // XÓA HOÀN TOÀN record của filled document trong DB
                        await connection.ExecuteAsync(
                            "DELETE FROM contract_documents WHERE Id = @Id",
                            new { Id = fillResult.FilledDocumentId });

                        _logger.LogInformation("✓ Deleted filled document record from database");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete filled file from S3");
                    }

                    // 5.2: Rename và move file SIGNED sang folder Completed
                    // Format: {NewDocumentId}_HOP_DONG_LAO_DONG_NV_BAO_VE_{ContractNumber}.docx
                    var newDocumentId = Guid.NewGuid();
                    var contractTypeKey = GetContractTypeKey(contractType);
                    var newFileName = $"{newDocumentId}_{contractTypeKey}_{contractNumber}.docx";
                    var completedFolderPath = GetCompletedFolderPath(contractType);
                    var newS3Key = $"{completedFolderPath}/{newFileName}";

                    _logger.LogInformation("Moving signed file to completed folder: {NewPath}", newS3Key);

                    // Download file từ S3
                    var (downloadSuccess, signedStream, downloadError) = await _s3Service.DownloadFileAsync(
                        signedDoc.FileUrl);

                    if (downloadSuccess && signedStream != null)
                    {
                        // Upload với tên mới
                        var (uploadSuccess, newFileUrl, uploadError) = await _s3Service.UploadFileWithCustomKeyAsync(
                            signedStream,
                            newS3Key,
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

                        signedStream.Dispose();

                        if (uploadSuccess && !string.IsNullOrEmpty(newFileUrl))
                        {
                            _logger.LogInformation("✓ Uploaded file to completed folder: {NewUrl}", newFileUrl);

                            // Xóa file SIGNED cũ từ S3
                            await _s3Service.DeleteFileAsync(signedDoc.FileUrl);
                            _logger.LogInformation("✓ Deleted old signed file from S3");

                            // XÓA HOÀN TOÀN record của signed document cũ
                            await connection.ExecuteAsync(
                                "DELETE FROM contract_documents WHERE Id = @Id",
                                new { Id = signResult.SignedDocumentId });

                            _logger.LogInformation("✓ Deleted old signed document record from database");

                            // INSERT MỚI document record cho file completed
                            var completedDocument = new ContractDocument
                            {
                                Id = newDocumentId,
                                DocumentName = newFileName,
                                FileUrl = newS3Key,
                                FileSize = 0,
                                DocumentType = "completed_contract",
                                Version = "final",
                                UploadedBy = signedDoc.UploadedBy,
                                IsDeleted = false,
                                CreatedAt = DateTime.UtcNow
                            };

                            await connection.InsertAsync(completedDocument);
                            _logger.LogInformation("✓ Created new completed document record: {DocumentId}", newDocumentId);

                            // Update contract với DocumentId mới và URL mới
                            if (contractId.HasValue)
                            {
                                await connection.ExecuteAsync(
                                    @"UPDATE contracts
                                      SET ContractFileUrl = @NewFileUrl,
                                          DocumentId = @NewDocumentId,
                                          UpdatedAt = @UpdatedAt
                                      WHERE Id = @ContractId",
                                    new
                                    {
                                        NewFileUrl = newFileUrl,
                                        NewDocumentId = newDocumentId,
                                        UpdatedAt = DateTime.UtcNow,
                                        ContractId = contractId.Value
                                    });

                                _logger.LogInformation("✓ Updated contract with new DocumentId and FileUrl");
                            }

                            finalFileUrl = newFileUrl;
                            _logger.LogInformation("✓ [5/5] Complete - File finalized: {FileName}", newFileName);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to upload file to completed folder: {Error}", uploadError);
                            finalFileUrl = signResult.SignedFileUrl; // Fallback to original
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to download signed file for moving: {Error}", downloadError);
                        finalFileUrl = signResult.SignedFileUrl; // Fallback to original
                    }
                }
                else
                {
                    _logger.LogWarning("Cannot cleanup files - missing document info or contract number");
                    finalFileUrl = signResult.SignedFileUrl; // Fallback to original
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file cleanup - continuing with original signed file");
                finalFileUrl = signResult.SignedFileUrl; // Fallback to original
            }

            _logger.LogInformation("=== CONTRACT PROCESSING JOB COMPLETE ===");

            return new ContractProcessingJobResult
            {
                Success = true,
                ContractId = contractId,
                ContractNumber = contractNumber,
                FilledFileUrl = fillResult.FilledFileUrl,
                SignedFileUrl = finalFileUrl ?? signResult.SignedFileUrl,
                FilledDocumentId = fillResult.FilledDocumentId,
                SignedDocumentId = signResult.SignedDocumentId,
                SignatureCount = checkResult.SignatureCount,
                Message = "Contract processing completed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contract processing job failed: {Message}", ex.Message);

            return new ContractProcessingJobResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Xác định loại contract dựa trên tên file
    /// </summary>
    private ContractType DetermineContractType(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            _logger.LogWarning("File name is empty, defaulting to ServiceContract");
            return ContractType.ServiceContract;
        }

        var upperFileName = fileName.ToUpperInvariant()
            .Replace("-", "_");

        // Hợp đồng lao động nhân viên bảo vệ
        if (upperFileName.Contains("HOP_DONG_LAO_DONG_NV_BAO_VE") ||
            upperFileName.Contains("WORKING_CONTRACT_EMPLOYEE") ||
            upperFileName.Contains("NHAN_VIEN_BAO_VE"))
        {
            return ContractType.WorkingContractEmployee;
        }

        // Hợp đồng lao động quản lý
        if (upperFileName.Contains("HOP_DONG_LAO_DONG_QUAN_LY") ||
            upperFileName.Contains("WORKING_CONTRACT_MANAGER") ||
            upperFileName.Contains("QUAN_LY"))
        {
            return ContractType.WorkingContractManager;
        }

        // Hợp đồng dịch vụ bảo vệ (default)
        if (upperFileName.Contains("HOP_DONG_DICH_VU_BAO_VE") ||
            upperFileName.Contains("SERVICE_CONTRACT") ||
            upperFileName.Contains("DICH_VU"))
        {
            return ContractType.ServiceContract;
        }

        // Default: Service contract
        _logger.LogWarning("Could not determine contract type from file name: {FileName}, defaulting to ServiceContract", fileName);
        return ContractType.ServiceContract;
    }

    /// <summary>
    /// Lấy key của loại hợp đồng để đặt tên file
    /// </summary>
    private string GetContractTypeKey(ContractType contractType)
    {
        return contractType switch
        {
            ContractType.WorkingContractEmployee => "HOP_DONG_LAO_DONG_NV_BAO_VE",
            ContractType.WorkingContractManager => "HOP_DONG_LAO_DONG_QUAN_LY",
            ContractType.ServiceContract => "HOP_DONG_DICH_VU_BAO_VE",
            _ => "HOP_DONG_KHAC"
        };
    }

    /// <summary>
    /// Lấy folder path cho file đã hoàn thành
    /// </summary>
    private string GetCompletedFolderPath(ContractType contractType)
    {
        return contractType switch
        {
            ContractType.WorkingContractEmployee => "contracts/completed/Hợp đồng lao động nhân viên bảo vệ",
            ContractType.WorkingContractManager => "contracts/completed/Hợp đồng lao động quản lý",
            ContractType.ServiceContract => "contracts/completed/Hợp đồng dịch vụ bảo vệ",
            _ => "contracts/completed/Hợp đồng khác"
        };
    }
}

public record ContractProcessingJobResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ContractId { get; init; }
    public string? ContractNumber { get; init; }
    public string? FilledFileUrl { get; init; }
    public string? SignedFileUrl { get; init; }
    public Guid? FilledDocumentId { get; init; }
    public Guid? SignedDocumentId { get; init; }
    public int SignatureCount { get; init; }
    public string? Message { get; init; }
}
