namespace Contracts.API.ContractsHandler.SignContract;

public class SignContractFromDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contracts/sign-document",
            async (HttpRequest request,
                ISender sender,
                ILogger<SignContractFromDocumentEndpoint> logger) =>
            {
                try
                {
                    if (!request.HasFormContentType)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Request must be multipart/form-data"
                        });
                    }

                    var form = await request.ReadFormAsync();
                    var documentIdStr = form["documentId"].ToString();
                    if (string.IsNullOrWhiteSpace(documentIdStr))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "documentId is required"
                        });
                    }

                    if (!Guid.TryParse(documentIdStr, out var documentId))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Invalid documentId format"
                        });
                    }
                    if (form.Files.Count == 0)
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Signature image file is required"
                        });
                    }

                    var signatureImage = form.Files[0];

                    var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
                    var fileExtension = Path.GetExtension(signatureImage.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = "Signature image must be PNG or JPG format"
                        });
                    }

                    logger.LogInformation(
                        "Inserting signature - DocumentId: {DocumentId}, Image: {FileName} ({Size} bytes)",
                        documentId, signatureImage.FileName, signatureImage.Length);

                    var command = new SignContractFromDocumentCommand(
                        DocumentId: documentId,
                        SignatureImage: signatureImage
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
                        data = new
                        {
                            documentId = result.DocumentId,
                            fileUrl = result.FileUrl,
                            fileName = result.FileName
                        },
                        message = "Signature inserted successfully"
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error inserting signature");
                    return Results.Problem(
                        title: "Insert signature failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
            .DisableAntiforgery()
            .AllowAnonymous()
            .WithTags("Contracts")
            .WithName("InsertSignatureToDocument")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Chèn ảnh chữ ký vào hợp đồng");
    }
}
