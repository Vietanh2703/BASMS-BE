using Incidents.API.Extensions;

namespace Incidents.API.IncidentHandler.CreateIncident;

public class CreateIncidentHandler(
    IDbConnectionFactory connectionFactory,
    IS3Service s3Service,
    ILogger<CreateIncidentHandler> logger)
    : ICommandHandler<CreateIncidentCommand, CreateIncidentResult>
{
    public async Task<CreateIncidentResult> Handle(
        CreateIncidentCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Creating incident: {Title} by {ReporterName}",
            command.Title,
            command.ReporterName);

        using var connection = await connectionFactory.CreateConnectionAsync();

        try
        {
            var validator = new CreateIncidentValidator();
            var validationResult = await validator.ValidateAsync(command, cancellationToken);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                logger.LogWarning("Validation failed: {Errors}", errors);

                return new CreateIncidentResult
                {
                    IncidentId = Guid.Empty,
                    IncidentCode = string.Empty,
                    Success = false,
                    ErrorMessage = $"Validation failed: {errors}"
                };
            }

            var incidentCode = await GenerateIncidentCodeAsync(connection);

            var incident = new Models.Incidents
            {
                Id = Guid.NewGuid(),
                IncidentCode = incidentCode,
                Title = command.Title,
                Description = command.Description,
                IncidentType = command.IncidentType.ToUpper(),
                Severity = command.Severity.ToUpper(),
                IncidentTime = command.IncidentTime,
                Location = command.Location,
                ShiftLocation = command.ShiftLocation,
                ShiftId = command.ShiftId,
                ShiftAssignmentId = command.ShiftAssignmentId,
                ReporterId = command.ReporterId,
                ReporterName = command.ReporterName,
                ReporterEmail = command.ReporterEmail,
                ReportedTime = DateTime.UtcNow,
                Status = "REPORTED",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = command.ReporterId
            };

            await connection.InsertAsync(incident);

            logger.LogInformation(
                "Incident created successfully: {IncidentCode} (ID: {IncidentId})",
                incident.IncidentCode,
                incident.Id);

            var uploadedFileUrls = new List<string>();
            var mediaFilesUploaded = 0;

            if (command.MediaFiles != null && command.MediaFiles.Any())
            {
                logger.LogInformation(
                    "Uploading {Count} media files for incident {IncidentCode}",
                    command.MediaFiles.Count,
                    incident.IncidentCode);

                foreach (var mediaFile in command.MediaFiles)
                {
                    try
                    {
                        var uploadResult = await s3Service.UploadFileAsync(
                            mediaFile.FileStream,
                            mediaFile.FileName,
                            mediaFile.ContentType,
                            cancellationToken);

                        if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.FileUrl))
                        {
                            var incidentMedia = new IncidentMedia
                            {
                                Id = Guid.NewGuid(),
                                IncidentId = incident.Id,
                                MediaType = mediaFile.MediaType.ToUpper(),
                                FileUrl = uploadResult.FileUrl,
                                FileName = mediaFile.FileName,
                                FileSize = mediaFile.FileStream.Length,
                                MimeType = mediaFile.ContentType,
                                Caption = mediaFile.Caption,
                                DisplayOrder = mediaFilesUploaded + 1,
                                UploadedBy = command.ReporterId,
                                UploadedByName = command.ReporterName,
                                IsDeleted = false,
                                CreatedAt = DateTime.UtcNow
                            };

                            await connection.InsertAsync(incidentMedia);

                            uploadedFileUrls.Add(uploadResult.FileUrl);
                            mediaFilesUploaded++;

                            logger.LogInformation(
                                "Media file uploaded: {FileName} -> {FileUrl}",
                                mediaFile.FileName,
                                uploadResult.FileUrl);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Failed to upload media file {FileName}: {Error}",
                                mediaFile.FileName,
                                uploadResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Error uploading media file {FileName}",
                            mediaFile.FileName);
                    }
                }

                logger.LogInformation(
                    "Uploaded {UploadedCount}/{TotalCount} media files for incident {IncidentCode}",
                    mediaFilesUploaded,
                    command.MediaFiles.Count,
                    incident.IncidentCode);
            }

            return new CreateIncidentResult
            {
                IncidentId = incident.Id,
                IncidentCode = incident.IncidentCode,
                MediaFilesUploaded = mediaFilesUploaded,
                UploadedFileUrls = uploadedFileUrls,
                Success = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating incident");

            return new CreateIncidentResult
            {
                IncidentId = Guid.Empty,
                IncidentCode = string.Empty,
                Success = false,
                ErrorMessage = $"Error creating incident: {ex.Message}"
            };
        }
    }

    private async Task<string> GenerateIncidentCodeAsync(IDbConnection connection)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM incidents WHERE IsDeleted = 0");

        var nextNumber = count + 1;
        return $"INC{nextNumber:D5}";
    }
}
