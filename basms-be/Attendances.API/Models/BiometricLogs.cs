using Dapper.Contrib.Extensions;

namespace Attendances.API.Models;

/// <summary>
/// BIOMETRIC_LOGS - Bảng lưu log từ thiết bị vân tay/khuôn mặt
/// Chức năng: Lưu raw data từ biometric devices, sync với attendance records
/// Use case: "Device ghi nhận vân tay guard X lúc 8:03, tạo attendance record"
/// </summary>
[Table("biometric_logs")]
public class BiometricLogs
{
    [ExplicitKey]
    public Guid Id { get; set; }

    // ============================================================================
    // DEVICE INFORMATION
    // ============================================================================

    /// <summary>
    /// ID thiết bị biometric
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Tên thiết bị: "Main Gate - FP Scanner 01"
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Loại thiết bị: FINGERPRINT | FACE_RECOGNITION | IRIS | CARD | PIN
    /// </summary>
    public string DeviceType { get; set; } = "FACE_RECOGNITION";

    /// <summary>
    /// Vị trí thiết bị
    /// </summary>
    public string? DeviceLocation { get; set; }

    /// <summary>
    /// IP address của thiết bị
    /// </summary>
    public string? DeviceIpAddress { get; set; }

    // ============================================================================
    // AUTHENTICATION DATA
    // ============================================================================

    /// <summary>
    /// Guard ID (mapped từ biometric user ID)
    /// </summary>
    public Guid? GuardId { get; set; }

    /// <summary>
    /// User ID từ thiết bị biometric (employee code)
    /// </summary>
    public string BiometricUserId { get; set; } = string.Empty;

    /// <summary>
    /// Template ID vân tay/khuôn mặt (nếu có)
    /// </summary>
    public string? BiometricTemplateId { get; set; }

    /// <summary>
    /// Độ chính xác match (0-100%)
    /// </summary>
    public decimal? MatchScore { get; set; }

    /// <summary>
    /// Phương thức xác thực: FINGERPRINT | FACE | CARD | PIN | MANUAL
    /// </summary>
    public string AuthenticationMethod { get; set; } = "FACE";

    // ============================================================================
    // FACE RECOGNITION SPECIFIC FIELDS
    // ============================================================================

    /// <summary>
    /// S3 URL ảnh khuôn mặt chụp khi check-in/out
    /// </summary>
    public string? FaceImageUrl { get; set; }

    /// <summary>
    /// S3 URL ảnh khuôn mặt template đã đăng ký
    /// </summary>
    public string? RegisteredFaceTemplateUrl { get; set; }

    /// <summary>
    /// Điểm tương đồng khuôn mặt từ AI (0-100%)
    /// </summary>
    public decimal? FaceMatchConfidence { get; set; }

    /// <summary>
    /// AI Model version sử dụng
    /// </summary>
    public string? AiModelVersion { get; set; }

    /// <summary>
    /// Metadata từ AI response (JSON)
    /// Chứa: face_landmarks, face_quality, liveness_score
    /// </summary>
    public string? AiResponseMetadata { get; set; }

    /// <summary>
    /// Liveness detection score (chống ảnh giả)
    /// </summary>
    public decimal? LivenessScore { get; set; }

    /// <summary>
    /// Face quality score từ AI
    /// </summary>
    public decimal? FaceQualityScore { get; set; }

    // ============================================================================
    // TIMESTAMP & EVENT TYPE
    // ============================================================================

    /// <summary>
    /// Thời gian ghi nhận từ thiết bị (device time)
    /// </summary>
    public DateTime DeviceTimestamp { get; set; }

    /// <summary>
    /// Thời gian nhận được log (server time)
    /// </summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>
    /// Loại event: CHECK_IN | CHECK_OUT | BREAK_START | BREAK_END | UNKNOWN
    /// </summary>
    public string EventType { get; set; } = "CHECK_IN";

    // ============================================================================
    // VERIFICATION STATUS
    // ============================================================================

    /// <summary>
    /// Xác thực thành công
    /// </summary>
    public bool IsVerified { get; set; } = true;

    /// <summary>
    /// Trạng thái: SUCCESS | FAILED | DUPLICATE | INVALID | PENDING
    /// </summary>
    public string VerificationStatus { get; set; } = "SUCCESS";

    /// <summary>
    /// Lý do thất bại (nếu có)
    /// </summary>
    public string? FailureReason { get; set; }

    // ============================================================================
    // PROCESSING STATUS
    // ============================================================================

    /// <summary>
    /// Đã xử lý và tạo attendance record
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    /// Thời gian xử lý
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Attendance record được tạo (nếu có)
    /// </summary>
    public Guid? AttendanceRecordId { get; set; }

    /// <summary>
    /// Trạng thái xử lý: PENDING | PROCESSED | FAILED | IGNORED | DUPLICATE
    /// </summary>
    public string ProcessingStatus { get; set; } = "PENDING";

    /// <summary>
    /// Ghi chú xử lý
    /// </summary>
    public string? ProcessingNotes { get; set; }

    // ============================================================================
    // ADDITIONAL DATA (JSON)
    // ============================================================================

    /// <summary>
    /// Raw data từ thiết bị (JSON)
    /// Chứa: device info, environmental data, photo (nếu face recognition)
    /// </summary>
    public string? RawData { get; set; }

    /// <summary>
    /// URL ảnh chụp (nếu có - face recognition)
    /// </summary>
    public string? PhotoUrl { get; set; }

    /// <summary>
    /// Nhiệt độ cơ thể (nếu thiết bị có cảm biến)
    /// </summary>
    public decimal? BodyTemperature { get; set; }

    // ============================================================================
    // AUDIT & SYNC
    // ============================================================================

    /// <summary>
    /// Log đã được đồng bộ từ thiết bị
    /// </summary>
    public bool IsSynced { get; set; } = true;

    /// <summary>
    /// Batch ID (nếu sync theo batch)
    /// </summary>
    public Guid? SyncBatchId { get; set; }

    /// <summary>
    /// Số lần retry xử lý
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Lần retry cuối
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    // ============================================================================
    // FLAGS
    // ============================================================================

    /// <summary>
    /// Log trùng lặp (cùng user, cùng time window)
    /// </summary>
    public bool IsDuplicate { get; set; } = false;

    /// <summary>
    /// Log bất thường (cần kiểm tra)
    /// </summary>
    public bool IsAnomaly { get; set; } = false;

    /// <summary>
    /// Lý do đánh dấu anomaly
    /// </summary>
    public string? AnomalyReason { get; set; }

    // ============================================================================
    // AUDIT
    // ============================================================================

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
