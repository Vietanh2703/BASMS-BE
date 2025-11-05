using BuildingBlocks.CQRS;
using Dapper.Contrib.Extensions;
using FirebaseAdmin.Auth;
using Users.API.Data;
using Users.API.Extensions;
using Users.API.Models;

namespace Users.API.UsersHandler.UpdateUser;

public record UpdateUserCommand(
    Guid Id,
    string? FullName,
    string? Email,
    string? Phone,
    string? Address,
    int? BirthDay,
    int? BirthMonth,
    int? BirthYear,
    Guid? RoleId,
    string? AvatarUrl,
    string? Status
) : ICommand<UpdateUserResult>;

public record UpdateUserResult(
    Guid Id,
    string Email,
    string FullName,
    string Message
);

internal class UpdateUserHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<UpdateUserHandler> logger,
    UpdateUserValidator validator,
    EmailHandler emailHandler)
    : ICommandHandler<UpdateUserCommand, UpdateUserResult>
{
    public async Task<UpdateUserResult> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        try
        {
            logger.LogInformation("Attempting to update user with ID: {UserId}", command.Id);

            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Step 1: Get existing user
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var existingUser = users.FirstOrDefault(u => u.Id == command.Id && !u.IsDeleted);

                if (existingUser == null)
                {
                    logger.LogWarning("User not found with ID: {UserId}", command.Id);
                    throw new InvalidOperationException($"User with ID {command.Id} not found");
                }

                // Store old email for notification
                var oldEmail = existingUser.Email;
                var emailChanged = !string.IsNullOrEmpty(command.Email) && command.Email != existingUser.Email;

                // Store old values for audit
                var oldValues = new
                {
                    existingUser.Email,
                    existingUser.FullName,
                    existingUser.Phone,
                    existingUser.Address,
                    existingUser.RoleId,
                    existingUser.Status,
                    existingUser.AvatarUrl
                };

                // Step 2: Check if email is being changed and if it's unique
                if (!string.IsNullOrEmpty(command.Email) && command.Email != existingUser.Email)
                {
                    var emailExists = users.Any(u => u.Email == command.Email && !u.IsDeleted && u.Id != command.Id);
                    if (emailExists)
                    {
                        throw new InvalidOperationException($"Email {command.Email} is already in use by another user");
                    }
                }

                // Step 3: Update Firebase user if email or phone changed
                bool firebaseUpdated = false;
                if (!string.IsNullOrEmpty(command.Email) && command.Email != existingUser.Email ||
                    !string.IsNullOrEmpty(command.Phone) && command.Phone != existingUser.Phone)
                {
                    await UpdateFirebaseUserAsync(existingUser.FirebaseUid, command.Email, command.Phone);
                    firebaseUpdated = true;
                    logger.LogInformation("Firebase user updated: {FirebaseUid}", existingUser.FirebaseUid);
                }

                // Step 4: Update user in database
                if (!string.IsNullOrEmpty(command.FullName))
                    existingUser.FullName = command.FullName;

                if (!string.IsNullOrEmpty(command.Email))
                    existingUser.Email = command.Email;

                if (!string.IsNullOrEmpty(command.Phone))
                    existingUser.Phone = command.Phone;

                if (!string.IsNullOrEmpty(command.Address))
                    existingUser.Address = command.Address;

                if (command.BirthDay.HasValue)
                    existingUser.BirthDay = command.BirthDay;

                if (command.BirthMonth.HasValue)
                    existingUser.BirthMonth = command.BirthMonth;

                if (command.BirthYear.HasValue)
                    existingUser.BirthYear = command.BirthYear;

                if (command.RoleId.HasValue)
                    existingUser.RoleId = command.RoleId.Value;

                if (!string.IsNullOrEmpty(command.AvatarUrl))
                    existingUser.AvatarUrl = command.AvatarUrl;

                if (!string.IsNullOrEmpty(command.Status))
                    existingUser.Status = command.Status;

                existingUser.UpdatedAt = DateTime.UtcNow;

                await connection.UpdateAsync(existingUser, transaction);
                logger.LogDebug("User updated in database: {UserId}", existingUser.Id);

                // Step 5: Log audit
                var newValues = new
                {
                    existingUser.Email,
                    existingUser.FullName,
                    existingUser.Phone,
                    existingUser.Address,
                    existingUser.RoleId,
                    existingUser.Status,
                    existingUser.AvatarUrl,
                    FirebaseUpdated = firebaseUpdated
                };

                await LogAuditAsync(connection, transaction, existingUser.Id, oldValues, newValues);

                transaction.Commit();

                logger.LogInformation("User updated successfully: {Email}, UserId: {UserId}",
                    existingUser.Email, existingUser.Id);

                // Step 6: Send email notifications if email was changed
                if (emailChanged)
                {
                    try
                    {
                        // Send notification to old email
                        await emailHandler.SendEmailChangeNotificationAsync(
                            existingUser.FullName, 
                            oldEmail, 
                            command.Email!, 
                            isOldEmail: true);
                        logger.LogInformation("Email change notification sent to old email: {OldEmail}", oldEmail);

                        // Send notification to new email
                        await emailHandler.SendEmailChangeNotificationAsync(
                            existingUser.FullName, 
                            oldEmail, 
                            command.Email!, 
                            isOldEmail: false);
                        logger.LogInformation("Email change notification sent to new email: {NewEmail}", command.Email);
                    }
                    catch (Exception emailEx)
                    {
                        // Log error but don't fail the update
                        logger.LogError(emailEx, "Failed to send email change notifications, but user was updated successfully");
                    }
                }

                return new UpdateUserResult(
                    existingUser.Id,
                    existingUser.Email,
                    existingUser.FullName,
                    "User updated successfully"
                );
            }
            catch
            {
                transaction.Rollback();
                logger.LogWarning("Transaction rolled back due to error");
                throw;
            }
        }
        catch (FirebaseAuthException ex)
        {
            logger.LogError(ex, "Firebase error updating user: {UserId}", command.Id);
            throw new InvalidOperationException($"Failed to update Firebase user: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user with ID: {UserId}", command.Id);
            throw;
        }
    }

    private async Task UpdateFirebaseUserAsync(string firebaseUid, string? email, string? phone)
    {
        try
        {
            var firebaseAuth = FirebaseAuth.DefaultInstance;
            if (firebaseAuth == null)
            {
                logger.LogWarning("FirebaseAuth.DefaultInstance is null. Skipping Firebase user update.");
                return;
            }

            var userRecordArgs = new UserRecordArgs
            {
                Uid = firebaseUid
            };

            if (!string.IsNullOrEmpty(email))
            {
                userRecordArgs.Email = email;
            }

            if (!string.IsNullOrEmpty(phone))
            {
                userRecordArgs.PhoneNumber = phone;
            }

            await firebaseAuth.UpdateUserAsync(userRecordArgs);
            logger.LogInformation("Successfully updated Firebase user: {FirebaseUid}", firebaseUid);
        }
        catch (FirebaseAuthException ex)
        {
            logger.LogError(ex, "Firebase error updating user: {FirebaseUid}", firebaseUid);
            throw;
        }
    }

    private async Task LogAuditAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid userId,
        object oldValues,
        object newValues)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "UPDATE_USER",
            EntityType = "User",
            EntityId = userId,
            OldValues = System.Text.Json.JsonSerializer.Serialize(oldValues),
            NewValues = System.Text.Json.JsonSerializer.Serialize(newValues),
            IpAddress = null,
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}