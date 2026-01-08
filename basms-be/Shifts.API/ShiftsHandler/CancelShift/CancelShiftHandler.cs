using Shifts.API.Utilities;

namespace Shifts.API.ShiftsHandler.CancelShift;

public record CancelShiftCommand(
    Guid ShiftId,
    string CancellationReason,
    Guid CancelledBy
) : ICommand<CancelShiftResult>;

public record CancelShiftResult(
    bool Success,
    string Message,
    int AffectedGuards
);

internal class CancelShiftHandler(
    IDbConnectionFactory dbFactory,
    ISender sender,
    IPublishEndpoint publishEndpoint,
    IRequestClient<GetCustomerByContractRequest> customerClient,
    ILogger<CancelShiftHandler> logger)
    : ICommandHandler<CancelShiftCommand, CancelShiftResult>
{
    public async Task<CancelShiftResult> Handle(
        CancelShiftCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Cancelling shift {ShiftId}", request.ShiftId);

            using var connection = await dbFactory.CreateConnectionAsync();

            var shift = await connection.GetShiftByIdOrThrowAsync(request.ShiftId);
            
            if (shift.Status == "CANCELLED")
            {
                logger.LogWarning("Shift {ShiftId} is already cancelled", request.ShiftId);
                return new CancelShiftResult(
                    false,
                    "Shift đã bị hủy trước đó",
                    0);
            }
            
            if (shift.Status == "COMPLETED")
            {
                logger.LogWarning("Cannot cancel completed shift {ShiftId}", request.ShiftId);
                throw new InvalidOperationException("Không thể hủy ca trực đã hoàn thành");
            }

            logger.LogInformation(
                "Found shift {ShiftId} with status {Status} on {ShiftDate:yyyy-MM-dd}",
                shift.Id,
                shift.Status,
                shift.ShiftDate);
            
            var sql = @"
                SELECT sa.*, g.Email, g.FullName, g.PhoneNumber
                FROM shift_assignments sa
                INNER JOIN guards g ON sa.GuardId = g.Id
                WHERE sa.ShiftId = @ShiftId
                    AND sa.IsDeleted = 0
                    AND sa.Status NOT IN ('CANCELLED', 'COMPLETED')";

            var assignments = await connection.QueryAsync<AssignmentWithGuardInfo>(
                sql,
                new { ShiftId = request.ShiftId });

            var assignmentsList = assignments.ToList();

            logger.LogInformation(
                "Found {Count} active guard assignments for shift {ShiftId}",
                assignmentsList.Count,
                request.ShiftId);

            shift.Status = "CANCELLED";
            shift.CancelledAt = DateTime.UtcNow;
            shift.CancellationReason = request.CancellationReason;
            shift.UpdatedAt = DateTime.UtcNow;
            shift.UpdatedBy = request.CancelledBy;
            shift.Version++;

            await connection.UpdateAsync(shift);

            logger.LogInformation("Shift {ShiftId} status updated to CANCELLED", shift.Id);

            var updateAssignmentsSql = @"
                UPDATE shift_assignments
                SET
                    Status = 'CANCELLED',
                    CancelledAt = @CancelledAt,
                    CancellationReason = @CancellationReason,
                    UpdatedAt = @UpdatedAt
                WHERE ShiftId = @ShiftId
                    AND IsDeleted = 0
                    AND Status NOT IN ('CANCELLED', 'COMPLETED')";

            var affectedRows = await connection.ExecuteAsync(
                updateAssignmentsSql,
                new
                {
                    ShiftId = request.ShiftId,
                    CancelledAt = DateTime.UtcNow,
                    CancellationReason = request.CancellationReason,
                    UpdatedAt = DateTime.UtcNow
                });

            logger.LogInformation(
                "Updated {Count} shift assignments to CANCELLED",
                affectedRows);

            logger.LogInformation("Publishing ShiftAssignmentCancelledEvent...");

            foreach (var assignment in assignmentsList)
            {
                await publishEndpoint.Publish(new ShiftAssignmentCancelledEvent
                {
                    ShiftAssignmentId = assignment.Id,
                    ShiftId = assignment.ShiftId,
                    GuardId = assignment.GuardId,
                    CancellationReason = request.CancellationReason,
                    LeaveType = "OTHER", 
                    CancelledAt = DateTime.UtcNow,
                    CancelledBy = request.CancelledBy,
                    EvidenceImageUrl = null
                }, cancellationToken);
            }

            logger.LogInformation(
                "Published {Count} events to sync with Attendances.API",
                assignmentsList.Count);
            
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));

            var issueRecord = new
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                GuardId = (Guid?)null, 
                IssueType = "CANCEL_SHIFT",
                Reason = request.CancellationReason,
                StartDate = (DateTime?)null,
                EndDate = (DateTime?)null,
                IssueDate = vietnamNow,
                EvidenceFileUrl = (string?)null,
                TotalShiftsAffected = 1,
                TotalGuardsAffected = assignmentsList.Count,
                CreatedAt = vietnamNow,
                CreatedBy = request.CancelledBy,
                UpdatedAt = (DateTime?)null,
                UpdatedBy = (Guid?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedBy = (Guid?)null
            };

            await connection.ExecuteAsync(@"
                INSERT INTO shift_issues (
                    Id, ShiftId, GuardId, IssueType, Reason,
                    StartDate, EndDate, IssueDate, EvidenceFileUrl,
                    TotalShiftsAffected, TotalGuardsAffected,
                    CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
                    IsDeleted, DeletedAt, DeletedBy
                ) VALUES (
                    @Id, @ShiftId, @GuardId, @IssueType, @Reason,
                    @StartDate, @EndDate, @IssueDate, @EvidenceFileUrl,
                    @TotalShiftsAffected, @TotalGuardsAffected,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy,
                    @IsDeleted, @DeletedAt, @DeletedBy
                )", issueRecord);

            logger.LogInformation(
                "Saved shift issue record: {IssueId}, Type: {IssueType}",
                issueRecord.Id,
                issueRecord.IssueType);
            
            logger.LogInformation("Sending notifications and emails to guards");

            var emailsSent = 0;
            foreach (var assignment in assignmentsList)
            {
                var notificationCommand = new SendNotificationCommand(
                    ShiftId: shift.Id,
                    ContractId: shift.ContractId,
                    RecipientId: assignment.GuardId,
                    RecipientType: "GUARD",
                    Action: "SHIFT_CANCELLED",
                    Title: "Ca trực đã bị hủy",
                    Message: $"Ca trực ngày {shift.ShiftDate:dd/MM/yyyy} ({shift.ShiftStart.TimeOfDay:hh\\:mm}-{shift.ShiftEnd.TimeOfDay:hh\\:mm}) đã bị hủy. Lý do: {request.CancellationReason}",
                    Metadata: null,
                    Priority: "HIGH"
                );

                await sender.Send(notificationCommand, cancellationToken);
                
                if (!string.IsNullOrEmpty(assignment.Email))
                {
                    var emailCommand = new SendEmailNotificationCommand(
                        GuardName: assignment.FullName,
                        GuardEmail: assignment.Email,
                        ShiftDate: shift.ShiftDate,
                        StartTime: shift.ShiftStart.TimeOfDay,
                        EndTime: shift.ShiftEnd.TimeOfDay,
                        Location: "Location", 
                        EmailType: "CANCELLATION",
                        AdditionalInfo: request.CancellationReason
                    );

                    await sender.Send(emailCommand, cancellationToken);
                    emailsSent++;
                }
            }

            logger.LogInformation(
                "Sent notifications to {Count} guards ({EmailCount} emails)",
                assignmentsList.Count,
                emailsSent);
            
            if (shift.ContractId.HasValue)
            {
                logger.LogInformation("Sending emails to customer and director for shift cancellation");

                try
                {
                    var manager = await connection.GetAsync<Managers>(shift.ManagerId);
                    var managerName = manager?.FullName ?? "Unknown Manager";
                    var managerEmail = manager?.Email ?? "Unknown";
                    var guardsList = string.Join(", ", assignmentsList.Select(a => a.FullName));
                    
                    await sender.Send(new SendEmailNotificationCommand(
                        GuardName: "Director",
                        GuardEmail: "director@basms.com",
                        ShiftDate: shift.ShiftDate,
                        StartTime: shift.ShiftStart.TimeOfDay,
                        EndTime: shift.ShiftEnd.TimeOfDay,
                        Location: shift.LocationName ?? "Unknown Location",
                        EmailType: "DIRECTOR_CANCELLATION",
                        AdditionalInfo: $"{shift.LocationAddress ?? ""}|{request.CancellationReason}|{shift.ContractId}|{managerName}|{managerEmail}|{assignmentsList.Count}|{guardsList}"
                    ), cancellationToken);

                    logger.LogInformation(
                        "Director cancellation email sent to director@basms.com for shift {ShiftId}",
                        shift.Id);

   
                    try
                    {
                        logger.LogInformation(
                            "Querying customer info from Contracts.API for ContractId: {ContractId}",
                            shift.ContractId);
                        
                        var customerResponse = await customerClient.GetResponse<GetCustomerByContractResponse>(
                            new GetCustomerByContractRequest { ContractId = shift.ContractId.Value },
                            cancellationToken,
                            timeout: RequestTimeout.After(s: 10));

                        var customerData = customerResponse.Message;

                        if (customerData.Success && customerData.Customer != null)
                        {
                            var customer = customerData.Customer;
                            
                            await sender.Send(new SendEmailNotificationCommand(
                                GuardName: customer.CompanyName,
                                GuardEmail: customer.Email,
                                ShiftDate: shift.ShiftDate,
                                StartTime: shift.ShiftStart.TimeOfDay,
                                EndTime: shift.ShiftEnd.TimeOfDay,
                                Location: shift.LocationName ?? "Unknown Location",
                                EmailType: "CUSTOMER_CANCELLATION",
                                AdditionalInfo: $"{request.CancellationReason}|{shift.ContractId}|{managerName}"
                            ), cancellationToken);

                            logger.LogInformation(
                                "Customer cancellation email sent to {CompanyName} ({Email})",
                                customer.CompanyName,
                                customer.Email);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Could not get customer info for ContractId {ContractId}: {ErrorMessage}",
                                shift.ContractId,
                                customerData.ErrorMessage ?? "Unknown error");
                        }
                    }
                    catch (Exception customerEx)
                    {
                        logger.LogError(
                            customerEx,
                            "Failed to query customer info or send customer email for ContractId {ContractId}",
                            shift.ContractId);
                    }
                }
                catch (Exception emailEx)
                {
                    logger.LogError(
                        emailEx,
                        "Failed to send emails to customer/director for shift {ShiftId}",
                        shift.Id);
                }
            }
            
            logger.LogInformation(
                "Successfully cancelled shift {ShiftId}, affected {Count} guards",
                shift.Id,
                assignmentsList.Count);

            return new CancelShiftResult(
                true,
                $"Shift đã được hủy thành công. Đã thông báo cho {assignmentsList.Count} bảo vệ.",
                assignmentsList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling shift {ShiftId}", request.ShiftId);
            throw;
        }
    }
}

internal class AssignmentWithGuardInfo
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Guid GuardId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
