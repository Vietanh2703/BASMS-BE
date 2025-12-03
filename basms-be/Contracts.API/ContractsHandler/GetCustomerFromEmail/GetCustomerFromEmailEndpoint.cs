namespace Contracts.API.ContractsHandler.GetCustomerFromEmail;

public class GetCustomerFromEmailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Route: GET /api/contracts/customers/by-email?email={email}
        app.MapGet("/api/contracts/customers/by-email", async (string email, ISender sender) =>
        {
            var query = new GetCustomerFromEmailQuery(email);
            var result = await sender.Send(query);

            if (!result.Success)
            {
                return Results.NotFound(new
                {
                    success = false,
                    message = result.ErrorMessage,
                    email = email
                });
            }

            return Results.Ok(new
            {
                success = true,
                customer = new
                {
                    id = result.Customer!.Id,
                    customerCode = result.Customer.CustomerCode,
                    companyName = result.Customer.CompanyName,
                    contactPersonName = result.Customer.ContactPersonName,
                    contactPersonTitle = result.Customer.ContactPersonTitle,
                    identityNumber = result.Customer.IdentityNumber,
                    identityIssueDate = result.Customer.IdentityIssueDate,
                    identityIssuePlace = result.Customer.IdentityIssuePlace,
                    email = result.Customer.Email,
                    phone = result.Customer.Phone,
                    avatarUrl = result.Customer.AvatarUrl,
                    gender = result.Customer.Gender,
                    dateOfBirth = result.Customer.DateOfBirth,
                    address = result.Customer.Address,
                    city = result.Customer.City,
                    district = result.Customer.District,
                    industry = result.Customer.Industry,
                    companySize = result.Customer.CompanySize,
                    status = result.Customer.Status,
                    customerSince = result.Customer.CustomerSince,
                    followsNationalHolidays = result.Customer.FollowsNationalHolidays,
                    notes = result.Customer.Notes,
                    createdAt = result.Customer.CreatedAt
                }
            });
        })
        .RequireAuthorization()
        .WithTags("Contracts")
        .WithName("GetCustomerFromEmail")
        .Produces<GetCustomerFromEmailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Get customer by email")
        .WithDescription(@"
            Retrieves customer information by email address.

            **Use Case:**
            This endpoint is used when you need to find a customer using their
            email address. Useful for customer lookup, login flows, or email-based
            customer identification.

            **Features:**
            - Case-sensitive email matching
            - Returns only non-deleted customers
            - Full customer details in response

            **Response Structure:**
            ```json
            {
              ""success"": true,
              ""customer"": {
                ""id"": ""660e8400-e29b-41d4-a716-446655440000"",
                ""customerCode"": ""CUST-001"",
                ""companyName"": ""ABC Company Ltd."",
                ""contactPersonName"": ""Nguyen Van A"",
                ""email"": ""contact@abc.com"",
                ""phone"": ""0901234567"",
                ""status"": ""active"",
                ""address"": ""123 Main St, District 1"",
                ""city"": ""Ho Chi Minh"",
                ""industry"": ""retail"",
                ""customerSince"": ""2024-01-15T00:00:00Z""
              }
            }
            ```
        ");
    }
}
