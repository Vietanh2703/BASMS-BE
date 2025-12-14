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
        .DisableAntiforgery() // Required for form-data
        // .RequireAuthorization()
        .WithName("RegisterFaceWithFiles")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .WithSummary("Register guard's face with 6 image files (form-data)")
        .WithDescription(@"
            Registers a guard's face using multipart/form-data with 6 image files.
            Processes images sequentially and creates BiometricLog entry.

            ** POSTMAN USAGE **

            1. Set request type: POST
            2. URL: http://localhost:5004/api/attendances/faces/register-with-files
            3. Go to 'Body' tab
            4. Select 'form-data'
            5. Add the following fields:

            | KEY             | TYPE | VALUE                          |
            |-----------------|------|--------------------------------|
            | guardId         | Text | {your-guard-uuid}              |
            | image_front     | File | [Select front view image]      |
            | image_left      | File | [Select left view image]       |
            | image_right     | File | [Select right view image]      |
            | image_up        | File | [Select head up image]         |
            | image_down      | File | [Select head down image]       |
            | image_smile     | File | [Select smiling image]         |

            ** SEQUENTIAL PROCESSING **

            The handler processes images in this order:
            1. ✓ Front (nhìn thẳng) → Validate → Convert to Base64
            2. ✓ Left (quay trái) → Validate → Convert to Base64
            3. ✓ Right (quay phải) → Validate → Convert to Base64
            4. ✓ Up (ngẩn cao) → Validate → Convert to Base64
            5. ✓ Down (cúi xuống) → Validate → Convert to Base64
            6. ✓ Smile (cười) → Validate → Convert to Base64
            7. → Send all 6 images to Python Face Recognition API
            8. → Python processes and uploads to AWS S3
            9. → Create BiometricLog with template URL
            10. ✅ Complete

            ** RESPONSE **

            {
                ""success"": true,
                ""data"": {
                    ""guardId"": ""uuid"",
                    ""biometricLogId"": ""uuid"",
                    ""templateUrl"": ""s3://bucket/templates/{guardId}/template.pkl"",
                    ""processingSteps"": [
                        {
                            ""poseType"": ""front"",
                            ""status"": ""completed"",
                            ""message"": ""✓ Đã chuyển đổi (245KB)""
                        },
                        {
                            ""poseType"": ""left"",
                            ""status"": ""completed"",
                            ""message"": ""✓ Đã chuyển đổi (238KB)""
                        },
                        // ... 4 images more
                    ],
                    ""qualityScores"": [95.5, 92.3, 94.1, 88.7, 90.2, 93.8],
                    ""averageQuality"": 92.4
                },
                ""message"": ""Face registered successfully with sequential processing""
            }

            ** IMAGE REQUIREMENTS **

            - Format: JPG or PNG
            - Max size: 10MB per image
            - Must clearly show face from specified angle
            - Total: 6 images required

            ** CURL EXAMPLE **

            curl -X POST http://localhost:5004/api/attendances/faces/register-with-files \
              -F ""guardId=550e8400-e29b-41d4-a716-446655440000"" \
              -F ""image_front=@front.jpg"" \
              -F ""image_left=@left.jpg"" \
              -F ""image_right=@right.jpg"" \
              -F ""image_up=@up.jpg"" \
              -F ""image_down=@down.jpg"" \
              -F ""image_smile=@smile.jpg""
        ");
    }
}
