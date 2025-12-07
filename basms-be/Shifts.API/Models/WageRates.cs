using Dapper.Contrib.Extensions;

namespace Shifts.API.Models;

/// <summary>
/// WAGE_RATES - Mức tiền công chuẩn theo cấp bậc bảo vệ
/// Mục đích: Lưu trữ mức lương chuẩn của công ty cho từng hạng chứng chỉ
/// Use case: "Gợi ý mức lương khi tạo hợp đồng lao động mới cho bảo vệ hạng II"
/// </summary>
[Table("wage_rates")]
public class WageRates
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // CẤP BẬC
    // ============================================================================

    /// <summary>
    /// Hạng chứng chỉ nghiệp vụ: I, II, III, IV, V, VI
    /// Theo Nghị định 96/2016/NĐ-CP
    /// </summary>
    public string CertificationLevel { get; set; } = string.Empty;

    // ============================================================================
    // TIỀN CÔNG CƠ BẢN (VNĐ/tháng - làm 192 giờ/tháng chuẩn)
    // ============================================================================

    /// <summary>
    /// Mức tiền công tối thiểu (VNĐ/tháng)
    /// Ví dụ: 5.500.000đ cho bảo vệ hạng I
    /// </summary>
    public decimal MinWage { get; set; }

    /// <summary>
    /// Mức tiền công tối đa (VNĐ/tháng)
    /// Ví dụ: 6.500.000đ cho bảo vệ hạng I
    /// </summary>
    public decimal MaxWage { get; set; }

    /// <summary>
    /// Mức tiền công chuẩn (VNĐ/tháng)
    /// Dùng để gợi ý khi tạo hợp đồng
    /// Ví dụ: 6.000.000đ cho bảo vệ hạng I
    /// </summary>
    public decimal StandardWage { get; set; }

    /// <summary>
    /// Số tiền chuẩn ghi bằng chữ (kèm từ "chẵn")
    /// Ví dụ: "Sáu triệu đồng chẵn" cho 6.000.000đ
    /// Dùng để in vào hợp đồng mẫu
    /// </summary>
    public string? StandardWageInWords { get; set; }

    /// <summary>
    /// Đơn vị tính tiền công
    /// Mặc định: "VNĐ" (Việt Nam Đồng)
    /// </summary>
    public string Currency { get; set; } = "VNĐ";

    // ============================================================================
    // MÔ TẢ
    // ============================================================================

    /// <summary>
    /// Mô tả chi tiết về cấp bậc và nhiệm vụ
    /// Ví dụ: "Bảo vệ hạng I - Bảo vệ thường, canh gác đơn giản"
    /// </summary>
    public string? Description { get; set; }

    // ============================================================================
    // THỜI GIAN HIỆU LỰC (để theo dõi lịch sử thay đổi mức lương)
    // ============================================================================

    /// <summary>
    /// Ngày bắt đầu áp dụng mức lương này
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Ngày kết thúc áp dụng
    /// NULL = đang áp dụng hiện tại
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Ghi chú về mức lương
    /// Ví dụ: "Tăng 10% so với năm 2024"
    /// </summary>
    public string? Notes { get; set; }

    // ============================================================================
    // METADATA
    // ============================================================================

    /// <summary>
    /// Còn hoạt động (soft delete)
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
