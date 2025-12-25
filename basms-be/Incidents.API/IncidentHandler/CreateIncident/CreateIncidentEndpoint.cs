namespace Incidents.API.IncidentHandler.CreateIncident;

public class CreateIncidentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/incidents/create", async (
            HttpRequest request,
            ISender sender,
            ILogger<CreateIncidentEndpoint> logger) =>
        {
            try
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Request must be multipart/form-data"
                    });
                }

                var form = await request.ReadFormAsync();

                var title = form["title"].ToString();
                var description = form["description"].ToString();
                var incidentType = form["incidentType"].ToString();
                var severity = form["severity"].ToString();
                var incidentTimeStr = form["incidentTime"].ToString();
                var location = form["location"].ToString();
                var shiftLocation = form["shiftLocation"].ToString();
                var shiftIdStr = form["shiftId"].ToString();
                var shiftAssignmentIdStr = form["shiftAssignmentId"].ToString();
                var reporterIdStr = form["reporterId"].ToString();

                DateTime incidentTime;
                if (!string.IsNullOrEmpty(incidentTimeStr))
                {
                    if (TimeOnly.TryParse(incidentTimeStr, out var timeOnly))
                    {
                        var today = Incidents.API.Extensions.DateTimeExtensions.GetVietnamTime().Date;
                        incidentTime = today.Add(timeOnly.ToTimeSpan());
                    }
                    else if (!DateTime.TryParse(incidentTimeStr, out incidentTime))
                    {
                        incidentTime = Incidents.API.Extensions.DateTimeExtensions.GetVietnamTime();
                    }
                }
                else
                {
                    incidentTime = Incidents.API.Extensions.DateTimeExtensions.GetVietnamTime();
                }

                Guid? shiftId = Guid.TryParse(shiftIdStr, out var sId) ? sId : null;
                Guid? shiftAssignmentId = Guid.TryParse(shiftAssignmentIdStr, out var saId) ? saId : null;

                if (!Guid.TryParse(reporterIdStr, out var reporterId))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Invalid reporterId"
                    });
                }

                var mediaFiles = new List<MediaFileDto>();
                var files = form.Files;

                if (files.Any())
                {
                    logger.LogInformation("Processing {Count} uploaded files", files.Count);

                    foreach (var file in files)
                    {
                        if (file.Length > 0)
                        {
                            var stream = new MemoryStream();
                            await file.CopyToAsync(stream);
                            stream.Position = 0;

                            var mediaType = file.ContentType?.StartsWith("video/") == true ? "VIDEO" :
                                          file.ContentType?.StartsWith("audio/") == true ? "AUDIO" :
                                          "IMAGE";

                            mediaFiles.Add(new MediaFileDto
                            {
                                FileStream = stream,
                                FileName = file.FileName,
                                ContentType = file.ContentType ?? "application/octet-stream",
                                MediaType = mediaType,
                                Caption = file.Name
                            });
                        }
                    }
                }

                var command = new CreateIncidentCommand(
                    title,
                    description,
                    incidentType,
                    severity,
                    incidentTime,
                    location,
                    string.IsNullOrEmpty(shiftLocation) ? null : shiftLocation,
                    shiftId,
                    shiftAssignmentId,
                    reporterId,
                    mediaFiles.Any() ? mediaFiles : null
                );

                var result = await sender.Send(command);

                foreach (var mediaFile in mediaFiles)
                {
                    mediaFile.FileStream.Dispose();
                }

                if (!result.Success)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = result.ErrorMessage
                    });
                }

                return Results.Ok(new
                {
                    success = true,
                    incidentId = result.IncidentId,
                    incidentCode = result.IncidentCode,
                    mediaFilesUploaded = result.MediaFilesUploaded,
                    uploadedFileUrls = result.UploadedFileUrls,
                    message = $"Incident created successfully with {result.MediaFilesUploaded} media file(s)"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in CreateIncident endpoint");
                return Results.BadRequest(new
                {
                    success = false,
                    message = $"Error creating incident: {ex.Message}"
                });
            }
        })
        .WithName("CreateIncident")
        .WithTags("Incidents")
        .RequireAuthorization()
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces(200)
        .Produces(400);
    }
}
