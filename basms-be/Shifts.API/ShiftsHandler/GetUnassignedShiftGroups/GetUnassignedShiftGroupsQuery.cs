using Dapper;

namespace Shifts.API.ShiftsHandler.GetUnassignedShiftGroups;

/// <summary>
/// Query để lấy danh sách ca trực chưa được phân công (AssignedGuardsCount = 0)
/// Nhóm theo TemplateId và ContractId, chỉ hiện 1 đại diện cho mỗi nhóm
/// </summary>
public record GetUnassignedShiftGroupsQuery(
    Guid ManagerId,
    Guid? ContractId = null
) : IQuery<GetUnassignedShiftGroupsResult>;

/// <summary>
/// Result chứa danh sách nhóm ca trực chưa được phân công
/// </summary>
public record GetUnassignedShiftGroupsResult
{
    public bool Success { get; init; }
    public List<UnassignedShiftGroupDto> ShiftGroups { get; init; } = new();
    public int TotalGroups { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO đại diện cho một nhóm ca trực chưa phân công
/// Nhóm theo cặp (ShiftTemplateId, ContractId)
/// </summary>
public record UnassignedShiftGroupDto
{
    /// <summary>
    /// ID của shift đại diện (shift gần nhất trong nhóm)
    /// </summary>
    public Guid RepresentativeShiftId { get; init; }

    /// <summary>
    /// Template ID của nhóm ca này
    /// </summary>
    public Guid? ShiftTemplateId { get; init; }

    /// <summary>
    /// Contract ID của nhóm ca này
    /// </summary>
    public Guid? ContractId { get; init; }

    /// <summary>
    /// Tên của template (ví dụ: "Ca Sáng", "Ca Chiều")
    /// </summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// Mã code của template (ví dụ: "MORNING-8H")
    /// </summary>
    public string? TemplateCode { get; init; }

    /// <summary>
    /// Thông tin về contract
    /// </summary>
    public string? ContractNumber { get; init; }
    public string? ContractTitle { get; init; }

    /// <summary>
    /// Thông tin về địa điểm
    /// </summary>
    public Guid? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string? LocationAddress { get; init; }

    /// <summary>
    /// Thời gian ca trực
    /// </summary>
    public DateTime? ShiftStart { get; init; }
    public DateTime? ShiftEnd { get; init; }
    public decimal? WorkDurationHours { get; init; }

    /// <summary>
    /// Số lượng ca trực chưa phân công trong nhóm này
    /// </summary>
    public int UnassignedShiftCount { get; init; }

    /// <summary>
    /// Số bảo vệ cần thiết (của shift đại diện)
    /// </summary>
    public int RequiredGuards { get; init; }

    /// <summary>
    /// Ngày shift gần nhất trong nhóm
    /// </summary>
    public DateTime? NearestShiftDate { get; init; }

    /// <summary>
    /// Ngày shift xa nhất trong nhóm
    /// </summary>
    public DateTime? FarthestShiftDate { get; init; }

    /// <summary>
    /// Phân loại ca
    /// </summary>
    public bool IsNightShift { get; init; }
    public string? ShiftType { get; init; }
}
