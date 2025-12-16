using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Attendances.API.AttendanceHandler.GetAttendanceStatusByGuardId;


public class GetAttendanceStatusByGuardIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/attendances/status", async (
            [FromQuery] Guid guardId,
            [FromQuery] Guid shiftId,
            ISender sender,
            ILogger<GetAttendanceStatusByGuardIdEndpoint> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "GET /api/attendances/status - Getting attendance record for Guard {GuardId} and Shift {ShiftId}",
                guardId,
                shiftId);

            var query = new GetAttendanceStatusByGuardIdQuery(
                GuardId: guardId,
                ShiftId: shiftId
            );

            var result = await sender.Send(query, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Failed to get attendance record for Guard {GuardId} and Shift {ShiftId}: {Error}",
                    guardId,
                    shiftId,
                    result.ErrorMessage);

                return Results.BadRequest(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            if (result.Attendance == null)
            {
                logger.LogInformation(
                    "No attendance record found for Guard {GuardId} and Shift {ShiftId}",
                    guardId,
                    shiftId);

                return Results.Ok(new
                {
                    success = true,
                    data = (object?)null,
                    message = "No attendance record found"
                });
            }

            logger.LogInformation(
                "✓ Found attendance record with status: {Status} for Guard {GuardId} and Shift {ShiftId}",
                result.Attendance.Status,
                guardId,
                shiftId);

            return Results.Ok(new
            {
                success = true,
                data = result.Attendance
            });
        })
        .RequireAuthorization()
        .WithName("GetAttendanceStatusByGuardId")
        .WithTags("Attendances")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .WithSummary("Lấy đầy đủ thông tin attendance record theo GuardId và ShiftId")
        .WithDescription(@"
**Chức năng:**
Trả về đầy đủ thông tin của attendance record dựa trên GuardId và ShiftId.

**Request Parameters:**
- `guardId` (query, required): ID của guard
- `shiftId` (query, required): ID của shift

**Response:**
Trả về toàn bộ thông tin của attendance record bao gồm:
- **Basic Info**: Id, ShiftAssignmentId, GuardId, ShiftId
- **Check-in Info**: CheckInTime, CheckInLatitude, CheckInLongitude, CheckInLocationAccuracy, CheckInDistanceFromSite, CheckInDeviceId, CheckInFaceImageUrl, CheckInFaceMatchScore
- **Check-out Info**: CheckOutTime, CheckOutLatitude, CheckOutLongitude, CheckOutLocationAccuracy, CheckOutDistanceFromSite, CheckOutDeviceId, CheckOutFaceImageUrl, CheckOutFaceMatchScore
- **Scheduled Time**: ScheduledStartTime, ScheduledEndTime
- **Duration**: ActualWorkDurationMinutes, BreakDurationMinutes, TotalHours
- **Status & Flags**: Status, IsLate, IsEarlyLeave, HasOvertime, IsIncomplete, IsVerified
- **Late/Early Minutes**: LateMinutes, EarlyLeaveMinutes, OvertimeMinutes
- **Verification**: VerifiedBy, VerifiedAt, VerificationStatus
- **Notes**: Notes, ManagerNotes
- **Flags**: AutoDetected, FlagsForReview, FlagReason
- **Audit**: CreatedAt, UpdatedAt

**Status Values:**
- `CHECKED_IN`: Guard đã check-in
- `CHECKED_OUT`: Guard đã check-out
- `INCOMPLETE`: Thiếu check-in hoặc check-out
- `LATE_CHECKIN`: Check-in muộn
- `EARLY_CHECKOUT`: Check-out sớm

**Verification Status Values:**
- `PENDING`: Chờ xác nhận
- `APPROVED`: Đã được phê duyệt
- `REJECTED`: Bị từ chối

**Response Example (Found):**
```json
{
  ""success"": true,
  ""data"": {
    ""id"": ""123e4567-e89b-12d3-a456-426614174000"",
    ""shiftAssignmentId"": ""aaa-111"",
    ""guardId"": ""987e4567-e89b-12d3-a456-426614174001"",
    ""shiftId"": ""abc12345-e89b-12d3-a456-426614174002"",
    ""checkInTime"": ""2025-12-17T08:00:00Z"",
    ""checkInLatitude"": 10.762622,
    ""checkInLongitude"": 106.660172,
    ""checkInLocationAccuracy"": 5.5,
    ""checkInDistanceFromSite"": 10.2,
    ""checkInDeviceId"": ""device-123"",
    ""checkInFaceImageUrl"": ""https://s3.amazonaws.com/face-123.jpg"",
    ""checkInFaceMatchScore"": 98.5,
    ""checkOutTime"": ""2025-12-17T17:00:00Z"",
    ""checkOutLatitude"": 10.762633,
    ""checkOutLongitude"": 106.660183,
    ""checkOutLocationAccuracy"": 4.8,
    ""checkOutDistanceFromSite"": 8.5,
    ""checkOutDeviceId"": ""device-123"",
    ""checkOutFaceImageUrl"": ""https://s3.amazonaws.com/face-456.jpg"",
    ""checkOutFaceMatchScore"": 97.8,
    ""scheduledStartTime"": ""2025-12-17T08:00:00Z"",
    ""scheduledEndTime"": ""2025-12-17T17:00:00Z"",
    ""actualWorkDurationMinutes"": 480,
    ""breakDurationMinutes"": 60,
    ""totalHours"": 8.0,
    ""status"": ""CHECKED_OUT"",
    ""isLate"": false,
    ""isEarlyLeave"": false,
    ""hasOvertime"": false,
    ""isIncomplete"": false,
    ""isVerified"": true,
    ""lateMinutes"": 0,
    ""earlyLeaveMinutes"": 0,
    ""overtimeMinutes"": 0,
    ""verifiedBy"": ""manager-123"",
    ""verifiedAt"": ""2025-12-17T18:00:00Z"",
    ""verificationStatus"": ""APPROVED"",
    ""notes"": ""Worked smoothly"",
    ""managerNotes"": ""Good performance"",
    ""autoDetected"": false,
    ""flagsForReview"": false,
    ""flagReason"": null,
    ""createdAt"": ""2025-12-17T08:00:00Z"",
    ""updatedAt"": ""2025-12-17T17:00:00Z""
  }
}
```

**Response Example (Not Found):**
```json
{
  ""success"": true,
  ""data"": null,
  ""message"": ""No attendance record found""
}
```

**Use Cases:**
- Kiểm tra đầy đủ thông tin attendance của guard trong một ca
- Xem chi tiết check-in/check-out (vị trí, thời gian, ảnh khuôn mặt)
- Kiểm tra trạng thái verification
- Xem ghi chú và cờ cần review

**Examples:**
```
GET /api/attendances/status?guardId=987e4567-e89b-12d3-a456-426614174001&shiftId=abc12345-e89b-12d3-a456-426614174002
```
        ");
    }
}
