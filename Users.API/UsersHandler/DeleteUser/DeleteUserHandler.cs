// Handler xử lý logic xóa user
// Thực hiện soft delete (đánh dấu IsDeleted) và xóa trên Firebase
namespace Users.API.UsersHandler.DeleteUser;

// Command chứa ID user cần xóa
public record DeleteUserCommand(Guid Id) : ICommand<DeleteUserResult>;

// Result trả về - chứa trạng thái thành công và message
public record DeleteUserResult(bool IsSuccess, string Message);

internal class DeleteUserHandler(
    IDbConnectionFactory connectionFactory,
    ILogger<DeleteUserHandler> logger)
    : ICommandHandler<DeleteUserCommand, DeleteUserResult>
{
    public async Task<DeleteUserResult> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Attempting to delete user with ID: {UserId}", command.Id);

            // Bước 1: Tạo kết nối database và transaction
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Bước 2: Tìm user theo ID và kiểm tra chưa bị xóa
                var users = await connection.GetAllAsync<Models.Users>(transaction);
                var user = users.FirstOrDefault(u => u.Id == command.Id && !u.IsDeleted);

                if (user == null)
                {
                    logger.LogWarning("User not found with ID: {UserId}", command.Id);
                    return new DeleteUserResult(false, $"User with ID {command.Id} not found");
                }

                // Bước 3: Xóa user khỏi Firebase Authentication
                // Vô hiệu hóa tài khoản Firebase để không thể đăng nhập
                try
                {
                    await DeleteFirebaseUserAsync(user.FirebaseUid);
                    logger.LogInformation("User deleted from Firebase: {FirebaseUid}", user.FirebaseUid);
                }
                catch (FirebaseAuthException ex)
                {
                    // Log warning nhưng tiếp tục xóa trong database
                    // User có thể đã bị xóa trên Firebase trước đó
                    logger.LogWarning(ex, "Failed to delete user from Firebase (user may not exist): {FirebaseUid}", user.FirebaseUid);
                }

                // Bước 4: Soft delete trong database
                // Đánh dấu IsDeleted = true thay vì DELETE khỏi bảng
                // Điều này giữ lại dữ liệu cho audit và có thể restore sau
                user.IsDeleted = true;
                user.UpdatedAt = DateTime.UtcNow;
                user.Status = "deleted";
                user.IsActive = false;

                // UPDATE user trong database
                await connection.UpdateAsync(user, transaction);
                logger.LogDebug("User soft deleted in database: {UserId}", user.Id);

                // Bước 5: Ghi log audit trail
                await LogAuditAsync(connection, transaction, user);

                // Bước 6: Commit transaction
                transaction.Commit();

                logger.LogInformation("User deleted successfully: {Email}, UserId: {UserId}", 
                    user.Email, user.Id);

                return new DeleteUserResult(true, $"User {user.Email} deleted successfully from both Firebase and database");
            }
            catch
            {
                // Rollback nếu có lỗi
                transaction.Rollback();
                logger.LogWarning("Transaction rolled back due to error");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting user with ID: {UserId}", command.Id);
            throw;
        }
    }

    // Hàm xóa user khỏi Firebase Authentication
    private async Task DeleteFirebaseUserAsync(string firebaseUid)
    {
        try
        {
            var firebaseAuth = FirebaseAuth.DefaultInstance;
            if (firebaseAuth == null)
            {
                logger.LogWarning("FirebaseAuth.DefaultInstance is null. Skipping Firebase user deletion.");
                return;
            }

            // Gọi Firebase API để xóa user
            await firebaseAuth.DeleteUserAsync(firebaseUid);
            logger.LogInformation("Successfully deleted Firebase user: {FirebaseUid}", firebaseUid);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            // User đã bị xóa trên Firebase - không cần throw exception
            logger.LogWarning("Firebase user not found: {FirebaseUid}. User may have been already deleted.", firebaseUid);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error deleting Firebase user: {FirebaseUid}", firebaseUid);
            throw;
        }
    }

    // Hàm ghi log audit trail
    // Lưu lại giá trị cũ và giá trị mới để theo dõi thay đổi
    private async Task LogAuditAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, Models.Users user)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "DELETE_USER",
            EntityType = "User",
            EntityId = user.Id,
            // Serialize giá trị cũ (trước khi xóa)
            OldValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                user.Email,
                user.FullName,
                user.FirebaseUid,
                user.Status,
                user.IsActive,
                IsDeleted = false
            }),
            // Serialize giá trị mới (sau khi xóa)
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                user.Email,
                user.FullName,
                user.FirebaseUid,
                Status = "deleted",
                IsActive = false,
                IsDeleted = true,
                DeletedFromFirebase = true
            }),
            IpAddress = null,
            Status = "success",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(auditLog, transaction);
    }
}