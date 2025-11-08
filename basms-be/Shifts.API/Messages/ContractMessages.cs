namespace Shifts.API.Messages;

/// <summary>
/// Request message để lấy thông tin contract từ Contracts.API qua RabbitMQ
/// </summary>
public record GetContractRequest
{
    public Guid ContractId { get; init; }
}

/// <summary>
/// Response message chứa thông tin contract
/// </summary>
public record GetContractResponse
{
    public bool Success { get; init; }
    public ContractData? Contract { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Contract data transfer object
/// </summary>
public record ContractData
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractTitle { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

/// <summary>
/// Request để lấy thông tin location
/// </summary>
public record GetLocationRequest
{
    public Guid LocationId { get; init; }
}

/// <summary>
/// Response chứa thông tin location
/// </summary>
public record GetLocationResponse
{
    public bool Success { get; init; }
    public LocationData? Location { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Location data transfer object
/// </summary>
public record LocationData
{
    public Guid Id { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public Guid? ContractId { get; init; }
}

/// <summary>
/// Request để lấy thông tin customer
/// </summary>
public record GetCustomerRequest
{
    public Guid CustomerId { get; init; }
}

/// <summary>
/// Response chứa thông tin customer
/// </summary>
public record GetCustomerResponse
{
    public bool Success { get; init; }
    public CustomerData? Customer { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Customer data transfer object
/// </summary>
public record CustomerData
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
}
