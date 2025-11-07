namespace Contracts.API.Models;

/// <summary>
/// NGÀY LÀM BÙ
/// Các ngày phải đi làm để bù cho ngày lễ
/// Ví dụ: Làm việc Thứ 7 để được nghỉ T2-T3-T4 Tết liên tục
/// </summary>
[Table("holiday_substitute_work_days")]
public class HolidaySubstituteWorkDay
{
    [ExplicitKey]
    public Guid Id { get; set; }

    /// <summary>
    /// Liên kết tới ngày lễ nào
    /// </summary>
    public Guid HolidayId { get; set; }

    /// <summary>
    /// Ngày phải đi làm: 2025-01-25 (Thứ 7)
    /// </summary>
    public DateTime SubstituteDate { get; set; }

    /// <summary>
    /// Lý do: "Work Saturday to get Monday-Tuesday-Wednesday off for Tet"
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Năm
    /// </summary>
    public int Year { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
