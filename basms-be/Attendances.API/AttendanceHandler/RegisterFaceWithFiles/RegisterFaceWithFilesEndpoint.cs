namespace Attendances.API.AttendanceHandler.RegisterFaceWithFiles;

/// <summary>
/// Endpoint để đăng ký khuôn mặt guard với form-data (files)
/// Xử lý tuần tự từng ảnh
/// </summary>
public class RegisterFaceWithFilesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendances/faces/register-with-files", async (
    HttpRequest httpRequest,
    ISender sender,
    ILogger<RegisterFaceWithFilesEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation("POST /api/attendances/faces/register-with-files - Processing form-data");

    // ================================================================
    // PARSE MULTIPART FORM-DATA
    // ================================================================

    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = "Request must be multipart/form-data"
        });
    }

    var form = await httpRequest.ReadFormAsync(cancellationToken);

    // Get GuardId
    if (!Guid.TryParse(form["guardId"].ToString(), out var guardId) || guardId == Guid.Empty)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = "GuardId is required and must be a valid GUID"
        });
    }

    // Get all 6 images
    var frontImage = form.Files.GetFile("image_front");
    var leftImage = form.Files.GetFile("image_left");
    var rightImage = form.Files.GetFile("image_right");
    var upImage = form.Files.GetFile("image_up");
    var downImage = form.Files.GetFile("image_down");
    var smileImage = form.Files.GetFile("image_smile");

    // Validate all images are present
    var missingImages = new List<string>();
    if (frontImage == null) missingImages.Add("image_front");
    if (leftImage == null) missingImages.Add("image_left");
    if (rightImage == null) missingImages.Add("image_right");
    if (upImage == null) missingImages.Add("image_up");
    if (downImage == null) missingImages.Add("image_down");
    if (smileImage == null) missingImages.Add("image_smile");

    if (missingImages.Any())
    {
        return Results.BadRequest(new
        {
            success = false,
            error = $"Missing required images: {string.Join(", ", missingImages)}",
            hint = "Required fields: image_front, image_left, image_right, image_up, image_down, image_smile"
        });
    }

    // ================================================================
    // CREATE COMMAND
    // ================================================================

    var command = new RegisterFaceWithFilesCommand(
        GuardId: guardId,
        FrontImage: frontImage!,
        LeftImage: leftImage!,
        RightImage: rightImage!,
        UpImage: upImage!,
        DownImage: downImage!,
        SmileImage: smileImage!
    );

    var result = await sender.Send(command, cancellationToken);

    if (!result.Success)
    {
        logger.LogWarning(
            "Failed to register face: {Error}",
            result.ErrorMessage);

        return Results.BadRequest(new
        {
            success = false,
            error = result.ErrorMessage,
            processingSteps = result.ProcessingSteps,
            message = result.Message
        });
    }

    logger.LogInformation(
        "✓ Face registered: GuardId={GuardId}, BiometricLogId={BiometricLogId}, AvgQuality={Quality}",
        guardId,
        result.BiometricLogId,
        result.AverageQuality);

    return Results.Ok(new
    {
        success = true,
        data = new
        {
            guardId = guardId,
            biometricLogId = result.BiometricLogId,
            templateUrl = result.TemplateUrl,
            processingSteps = result.ProcessingSteps,
            qualityScores = result.QualityScores,
            averageQuality = result.AverageQuality
        },
        message = result.Message
    });
})
        .DisableAntiforgery()
        .RequireAuthorization()
        .WithName("RegisterFaceWithFiles")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .WithSummary("Register guard's face with 6 image files ");
    }
}
