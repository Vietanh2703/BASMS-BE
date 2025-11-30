// Handler xử lý logic đăng xuất user
// Thực hiện: Lấy userId từ JWT -> Kiểm tra token tồn tại -> Xóa tất cả tokens -> Vô hiệu hóa session -> Log audit
namespace Users.API.UsersHandler.LogoutUser;

// Command để đăng xuất - không cần tham số vì lấy userId từ JWT token
public record LogoutUserCommand : ICommand<LogoutUserResult>;

// Result trả về - chứa trạng thái thành công và message
public record LogoutUserResult(bool Success, string Message);

public class LogoutUserHandler(
    IDbConnectionFactory connectionFactory,        // Factory để tạo kết nối database
    ILogger<LogoutUserHandler> logger,              // Logger để ghi log
    IHttpContextAccessor httpContextAccessor)       // Accessor để lấy thông tin từ HTTP context (JWT claims)
    : ICommandHandler<LogoutUserCommand, LogoutUserResult>
{
    public async Task<LogoutUserResult> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Bước 1: Lấy userId từ JWT token trong Authorization header
            // ClaimTypes.NameIdentifier chứa userId được mã hóa trong token
            var userIdClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            // Kiểm tra token có hợp lệ không (có userId và parse được thành Guid)
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            // Bước 2: Tạo kết nối database và transaction
            using var connection = await connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Bước 3: Kiểm tra user còn token nào trong database không
                // Lấy tất cả access tokens của user từ bảng user_tokens
                var userTokens = await connection.GetAllAsync<UserTokens>(transaction);
                var userAccessTokens = userTokens.Where(t => 
                    t.UserId == userId).ToList();

                // Lấy tất cả refresh tokens của user từ bảng refresh_tokens
                var refreshTokens = await connection.GetAllAsync<RefreshTokens>(transaction);
                var userRefreshTokens = refreshTokens.Where(t => 
                    t.UserId == userId).ToList();

                // Bước 4: Nếu không còn token nào -> User đã logout trước đó
                // Throw exception để ngăn spam logout nhiều lần
                if (!userAccessTokens.Any() && !userRefreshTokens.Any())
                {
                    logger.LogWarning("User already logged out: UserId: {UserId}", userId);
                    throw new InvalidOperationException("User is already logged out");
                }

                // Bước 5: XÓA tất cả access tokens của user
                // DELETE thay vì revoke để hoàn toàn xóa khỏi database
                foreach (var token in userAccessTokens)
                {
                    await connection.DeleteAsync(token, transaction);
                }

                // Bước 6: XÓA tất cả refresh tokens của user
                // Ngăn không cho user refresh để lấy access token mới
                foreach (var token in userRefreshTokens)
                {
                    await connection.DeleteAsync(token, transaction);
                }

                // Bước 7: Vô hiệu hóa user session trong bảng user_sessions
                // Tìm session đang active và chưa bị xóa
                var sessions = await connection.GetAllAsync<UserSessions>(transaction);
                var activeSession = sessions.FirstOrDefault(s => 
                    s.UserId == userId && 
                    s.IsActive && 
                    !s.IsDeleted);

                if (activeSession != null)
                {
                    // Set IsActive = false để đánh dấu session đã kết thúc
                    activeSession.IsActive = false;
                    activeSession.UpdatedAt = DateTime.UtcNow;
                    await connection.UpdateAsync(activeSession, transaction);
                }

                // Bước 8: Ghi log audit trail vào bảng audit_logs
                // Lưu lại hành động LOGOUT để theo dõi
                await LogAuditAsync(connection, transaction, userId, "LOGOUT");

                // Bước 9: Commit transaction - lưu tất cả thay đổi
                transaction.Commit();

                logger.LogInformation("User logged out successfully: UserId: {UserId}", userId);

                // Bước 10: Trả về kết quả thành công
                return new LogoutUserResult(true, "Logged out successfully");
            }
            catch
            {
                // Rollback transaction nếu có lỗi
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during logout");
            throw;
        }
    }

    // Hàm ghi log audit trail
    // Lưu lại hành động LOGOUT để có thể audit và theo dõi lịch sử
    private async Task LogAuditAsync(IDbConnection connection, IDbTransaction transaction, Guid userId, string action)
    {
        var auditLog = new AuditLogs
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,              // "LOGOUT"
            EntityType = "User",          // Loại entity
            EntityId = userId,            // ID của user
            Status = "success",           // Trạng thái thành công
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // INSERT audit log vào database
        await connection.InsertAsync(auditLog, transaction);
    }
}