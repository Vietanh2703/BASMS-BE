namespace Attendances.API.AttendanceHandler.RegisterFace;

/// <summary>
/// Endpoint để đăng ký khuôn mặt guard mới
/// </summary>
public class RegisterFaceEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/attendances/faces/register", async (
    RegisterFaceRequest request,
    ISender sender,
    ILogger<RegisterFaceEndpoint> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation(
        "POST /api/attendances/faces/register - Registering face for Guard={GuardId}",
        request.GuardId);

    // Validate request
    if (request.GuardId == Guid.Empty)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = "GuardId is required"
        });
    }

    if (request.Images == null || request.Images.Count != 6)
    {
        return Results.BadRequest(new
        {
            success = false,
            error = "Exactly 6 images are required (front, left, right, up, down, smile)"
        });
    }

    // Convert to FaceImageData
    var images = request.Images.Select(img => new FaceImageData(
        ImageBase64: img.ImageBase64,
        PoseType: img.PoseType,
        Angle: img.Angle
    )).ToList();

    // Create command
    var command = new RegisterFaceCommand(
        GuardId: request.GuardId,
        Images: images
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
            message = result.Message
        });
    }

    logger.LogInformation(
        "✓ Face registered: GuardId={GuardId}, BiometricLogId={BiometricLogId}, TemplateUrl={TemplateUrl}, AvgQuality={Quality}",
        request.GuardId,
        result.BiometricLogId,
        result.TemplateUrl,
        result.AverageQuality);

    return Results.Ok(new
    {
        success = true,
        data = new
        {
            guardId = request.GuardId,
            biometricLogId = result.BiometricLogId,
            templateUrl = result.TemplateUrl,
            qualityScores = result.QualityScores,
            averageQuality = result.AverageQuality
        },
        message = result.Message
    });
})
        // .RequireAuthorization()
        .WithName("RegisterFace")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .WithSummary("Register guard's face with 6 pose images")
        .WithDescription(@"
            Registers a guard's face by capturing 6 images from different angles.
            After successful registration, creates a BiometricLog entry.

            Required Images (in order):
            1. Front view (pose_type: ""front"") - Guard looking straight at camera
            2. Left view (pose_type: ""left"") - Guard turning head to the left
            3. Right view (pose_type: ""right"") - Guard turning head to the right
            4. Head up (pose_type: ""up"") - Guard tilting head upward
            5. Head down (pose_type: ""down"") - Guard tilting head downward
            6. Smiling (pose_type: ""smile"") - Guard smiling at camera

            This endpoint:
            1. Validates all 6 images are provided with correct pose types
            2. Calls the Face Recognition API (Python) to process and register the face
            3. Python API saves images to AWS S3 and returns template URL
            4. Creates a new BiometricLog entry with GuardId and RegisteredFaceTemplateUrl
            5. Returns BiometricLogId, template URL and quality scores

            Face Registration Process:
            1. Mobile app guides guard to capture 6 images from different angles
            2. C# API sends 6 images to Python Face Recognition API
            3. Python API processes images, extracts face embeddings
            4. Python API saves template to AWS S3 (folder path: s3://bucket/templates/{guardId}/)
            5. Python API returns S3 template URL
            6. C# API creates BiometricLog with GuardId and template URL
            7. Template URL will be used for future face verification during check-in/out

            Request Body:
            {
                ""guardId"": ""uuid"",
                ""images"": [
                    {
                        ""imageBase64"": ""base64-encoded-front-image"",
                        ""poseType"": ""front"",
                        ""angle"": 0
                    },
                    {
                        ""imageBase64"": ""base64-encoded-left-image"",
                        ""poseType"": ""left"",
                        ""angle"": -45
                    },
                    {
                        ""imageBase64"": ""base64-encoded-right-image"",
                        ""poseType"": ""right"",
                        ""angle"": 45
                    },
                    {
                        ""imageBase64"": ""base64-encoded-up-image"",
                        ""poseType"": ""up"",
                        ""angle"": 20
                    },
                    {
                        ""imageBase64"": ""base64-encoded-down-image"",
                        ""poseType"": ""down"",
                        ""angle"": -20
                    },
                    {
                        ""imageBase64"": ""base64-encoded-smile-image"",
                        ""poseType"": ""smile"",
                        ""angle"": 0
                    }
                ]
            }

            Response:
            {
                ""success"": true,
                ""data"": {
                    ""guardId"": ""uuid"",
                    ""biometricLogId"": ""uuid"",
                    ""templateUrl"": ""s3://bucket/templates/guard-uuid/template.pkl"",
                    ""qualityScores"": [95.5, 92.3, 94.1, 88.7, 90.2, 93.8],
                    ""averageQuality"": 92.4
                },
                ""message"": ""Face registered successfully and biometric log created""
            }

            Examples:
            POST /api/attendances/faces/register
        ");
    }
}

/// <summary>
/// Request model for face registration
/// </summary>
public record RegisterFaceRequest
{
    public Guid GuardId { get; init; }
    public List<ImageRequest> Images { get; init; } = new();
}

public record ImageRequest
{
    public string ImageBase64 { get; init; } = string.Empty;
    public string PoseType { get; init; } = string.Empty;
    public float Angle { get; init; } = 0;
}
