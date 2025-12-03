namespace Contracts.API.ContractsHandler.GetCustomerByContractId;

public class GetCustomerByContractIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contracts/{contractId:guid}/customer", async (Guid contractId, ISender sender) =>
        {
            var query = new GetCustomerByContractIdQuery(contractId);
            var result = await sender.Send(query);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    contractId = result.ContractId
                });
            }

            return Results.Ok(new
            {
                success = true,
                customerId = result.CustomerId,
                contractId = result.ContractId,
                contractNumber = result.ContractNumber
            });
        })
        .RequireAuthorization()
        .WithTags("Contracts")
        .WithName("GetCustomerByContractId")
        .Produces<GetCustomerByContractIdResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get customer ID by contract ID")
        .WithDescription(@"
            Retrieves the CustomerId associated with a specific ContractId.

            **Use Case:**
            This endpoint is used when you have a ContractId and need to find
            the associated Customer. Useful for navigating from contract to customer
            or for validating contract ownership.

            **Response Structure:**
            ```json
            {
              ""success"": true,
              ""customerId"": ""660e8400-e29b-41d4-a716-446655440000"",
              ""contractId"": ""770e8400-e29b-41d4-a716-446655440000"",
              ""contractNumber"": ""CTR-2025-001""
            }
            ```
        ");
    }
}
