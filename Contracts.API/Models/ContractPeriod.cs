namespace Contracts.API.Models;

/// <summary>
/// KỲ HẠN HỢP ĐỒNG
/// Tracking các kỳ hợp đồng (ban đầu, gia hạn lần 1, 2, 3...)
/// Dùng để audit history và renewal tracking
/// </summary>
[Table("contract_periods")]
public class ContractPeriod
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Thuộc hợp đồng nào
    /// </summary>
    public Guid ContractId { get; set; }

    /// <summary>
    /// Số thứ tự kỳ: 1=ban đầu, 2=gia hạn lần 1, 3=gia hạn lần 2...
    /// </summary>
    public int PeriodNumber { get; set; }

    /// <summary>
    /// Loại kỳ: initial, renewal, extension, amendment
    /// </summary>
    public string PeriodType { get; set; } = string.Empty;

    /// <summary>
    /// Ngày bắt đầu kỳ này
    /// </summary>
    public DateTime PeriodStartDate { get; set; }

    /// <summary>
    /// Ngày kết thúc kỳ này
    /// </summary>
    public DateTime PeriodEndDate { get; set; }

    /// <summary>
    /// Có phải kỳ hiện tại không?
    /// </summary>
    public bool IsCurrentPeriod { get; set; } = false;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
