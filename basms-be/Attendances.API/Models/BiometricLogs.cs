namespace Attendances.API.Models;

[Table("biometric_logs")]
public class BiometricLogs
{
    [ExplicitKey]
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string DeviceType { get; set; } = "FACE_RECOGNITION";
    public string? DeviceLocation { get; set; }
    public string? DeviceIpAddress { get; set; }
    public Guid? GuardId { get; set; }
    public string BiometricUserId { get; set; } = string.Empty;
    public string? BiometricTemplateId { get; set; }
    public decimal? MatchScore { get; set; }
    public string AuthenticationMethod { get; set; } = "FACE";
    public string? FaceImageUrl { get; set; }
    public string? RegisteredFaceTemplateUrl { get; set; }
    public decimal? FaceMatchConfidence { get; set; }
    public string? AiModelVersion { get; set; }
    public string? AiResponseMetadata { get; set; }
    public decimal? LivenessScore { get; set; }
    public decimal? FaceQualityScore { get; set; }
    public DateTime DeviceTimestamp { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string EventType { get; set; } = "CHECK_IN";
    public bool IsVerified { get; set; } = true;
    public string VerificationStatus { get; set; } = "SUCCESS";
    public string? FailureReason { get; set; }
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
    public Guid? AttendanceRecordId { get; set; }
    public string ProcessingStatus { get; set; } = "PENDING";
    public string? ProcessingNotes { get; set; }
    public string? RawData { get; set; }
    public string? PhotoUrl { get; set; }
    public decimal? BodyTemperature { get; set; }
    public bool IsSynced { get; set; } = true;
    public Guid? SyncBatchId { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? LastRetryAt { get; set; }
    public bool IsDuplicate { get; set; } = false;
    public bool IsAnomaly { get; set; } = false;
    public string? AnomalyReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

}
