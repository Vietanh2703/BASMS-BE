using BuildingBlocks.Messaging.Events;
using MassTransit;

namespace Users.API.Messaging;

/// <summary>
/// Centralized service for publishing user-related events to message bus
/// Used by: CreateUserHandler, UpdateUserHandler, DeleteUserHandler
/// </summary>
public class UserEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UserEventPublisher> _logger;

    public UserEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<UserEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Publish UserCreatedEvent when a new user is registered
    /// </summary>
    public async Task PublishUserCreatedAsync(Models.Users user, Roles role, CancellationToken cancellationToken = default)
    {
        try
        {
            DateTime dateOfBirth = default;
        
            if (user.BirthYear.HasValue && user.BirthMonth.HasValue && user.BirthDay.HasValue)
            {
                try
                {
                    dateOfBirth = new DateTime(
                        user.BirthYear.Value,
                        user.BirthMonth.Value,
                        user.BirthDay.Value
                    );
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    _logger.LogWarning(ex,
                        "Invalid birth date for User {UserId}: {Year}/{Month}/{Day}",
                        user.Id,
                        user.BirthYear,
                        user.BirthMonth,
                        user.BirthDay);
                    // Keep dateOfBirth as null if invalid
                }
            }
            
            var @event = new UserCreatedEvent
            {
                UserId = user.Id,
                FirebaseUid = user.FirebaseUid,
                IdentityNumber = user.IdentityNumber,
                IdentityIssueDate = user.IdentityIssueDate,
                IdentityIssuePlace = user.IdentityIssuePlace,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                AvatarUrl = user.AvatarUrl,

                RoleId = user.RoleId,
                RoleName = role.Name,

                // These would come from additional user fields if they exist
                // For now, using placeholders - adjust based on your Users model
                EmployeeCode = null, 
                Position = null,
                Department = null,
                DateOfBirth = dateOfBirth,
                Gender = user.Gender,
                Address = user.Address,
                HireDate = null,
                ContractType = null,

                CreatedAt = user.CreatedAt,
                CreatedBy = user.CreatedBy,
                Version = 1
            };

            await _publishEndpoint.Publish(@event, cancellationToken);

            _logger.LogInformation(
                "Published UserCreatedEvent for User {UserId} with Role {RoleName}",
                user.Id,
                role.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish UserCreatedEvent for User {UserId}",
                user.Id);
            // Don't throw - user creation should succeed even if event publishing fails
        }
    }

    /// <summary>
    /// Publish UserUpdatedEvent when user information changes
    /// </summary>
    public async Task PublishUserUpdatedAsync(
        Models.Users user,
        Roles role,
        List<string> changedFields,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var @event = new UserUpdatedEvent
            {
                UserId = user.Id,
                FullName = user.FullName,
                Phone = user.Phone,
                AvatarUrl = user.AvatarUrl,
                Address = user.Address,
                Status = user.Status,

                ContractType = null,
                TerminationDate = null,
                TerminationReason = null,

                UpdatedAt = user.UpdatedAt ?? DateTime.UtcNow,
                UpdatedBy = user.UpdatedBy,
                Version = 1, 

                ChangedFields = changedFields
            };

            await _publishEndpoint.Publish(@event, cancellationToken);

            _logger.LogInformation(
                "Published UserUpdatedEvent for User {UserId}. Changed fields: {Fields}",
                user.Id,
                string.Join(", ", changedFields));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish UserUpdatedEvent for User {UserId}",
                user.Id);
        }
    }

    /// <summary>
    /// Publish UserDeletedEvent when user is soft-deleted
    /// </summary>
    public async Task PublishUserDeletedAsync(
        Guid userId,
        string roleName,
        Guid? deletedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var @event = new UserDeletedEvent
            {
                UserId = userId,
                RoleName = roleName,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = deletedBy,
                DeletionReason = reason,
                IsSoftDelete = true
            };

            await _publishEndpoint.Publish(@event, cancellationToken);

            _logger.LogInformation(
                "Published UserDeletedEvent for User {UserId} with Role {RoleName}",
                userId,
                roleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish UserDeletedEvent for User {UserId}",
                userId);
        }
    }

    /// <summary>
    /// Publish UserRoleChangedEvent when user's role changes
    /// Example: Guard promoted to Manager
    /// </summary>
    public async Task PublishUserRoleChangedAsync(
        Models.Users user,
        Roles oldRole,
        Roles newRole,
        Guid? changedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var @event = new UserRoleChangedEvent
            {
                UserId = user.Id,
                OldRoleName = oldRole.Name,
                NewRoleName = newRole.Name,
                OldRoleId = oldRole.Id,
                NewRoleId = newRole.Id,

                FullName = user.FullName,
                Email = user.Email,
                EmployeeCode = null, // Add to Users model

                ChangedAt = DateTime.UtcNow,
                ChangedBy = changedBy,
                ChangeReason = reason,
                Version = 1
            };

            await _publishEndpoint.Publish(@event, cancellationToken);

            _logger.LogInformation(
                "Published UserRoleChangedEvent for User {UserId}: {OldRole} → {NewRole}",
                user.Id,
                oldRole.Name,
                newRole.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish UserRoleChangedEvent for User {UserId}",
                user.Id);
        }
    }
}
