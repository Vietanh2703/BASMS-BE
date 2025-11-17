namespace Contracts.API.ContractsHandler.SignContract;

public class SignContractFromDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/sign-document",
            async ([FromBody] SignContractFromS3Request request,
                ISender sender,
                ILogger<SignContractFromDocumentEndpoint> logger) =>
            {
                try
                {
                    logger.LogInformation(
                        "Signing contract from S3 - DocumentId: {DocumentId}",
                        request.DocumentId);

                    var command = new SignContractFromDocumentCommand(
                        DocumentId: request.DocumentId
                    );

                    var result = await sender.Send(command);

                    if (!result.Success)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = result.ErrorMessage
                        });
                    }

                    return Results.Ok(new
                    {
                        success = true,
                        signedDocumentId = result.SignedDocumentId,
                        signedFileUrl = result.SignedFileUrl,
                        signedFileName = result.SignedFileName,
                        message = "Contract signed successfully"
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error signing contract from S3");
                    return Results.Problem(
                        title: "Sign contract failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .WithTags("Contracts")
            .WithName("SignContractFromS3")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Ký điện tử hợp đồng từ AWS S3")
            .WithDescription(@"
Ký điện tử hợp đồng đã được fill và lưu trên S3.

**Request body:**
```json
{
  ""documentId"": ""guid-of-filled-document""
}
```

**Response:**
```json
{
  ""success"": true,
  ""signedDocumentId"": ""guid"",
  ""signedFileUrl"": ""s3://bucket/contracts/signed/SIGNED_XXX.docx"",
  ""signedFileName"": ""SIGNED_HOP_DONG_XXX.docx"",
  ""message"": ""Contract signed successfully""
}
```

**Flow:**
1. GET filled document từ S3
2. Sign với company certificate (D:\Projects\ImportantDoc\certificate.pfx)
3. Upload SIGNED file lên S3
4. Create ContractDocument record
5. Return signed document info

**Certificate:**
- Path: D:\Projects\ImportantDoc\certificate.pfx
- Password: From appsettings.json SignatureCertificate:Password
");
    }
}

public record SignContractFromS3Request
{
    public Guid DocumentId { get; init; }
}
