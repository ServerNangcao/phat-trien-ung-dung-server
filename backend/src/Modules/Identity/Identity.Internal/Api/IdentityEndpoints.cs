using System.Security.Claims;
using FluentValidation;
using Identity.Internal.Application.DTOs;
using Identity.Internal.Application.Services;
using Identity.Internal.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Contracts;
using Ticketing.Infrastructure.Core;
using Ticketing.SharedKernel;

namespace Identity.Internal.Api;

/// <summary>
/// 9 Minimal API endpoints cho module Identity.
/// </summary>
internal static class IdentityEndpoints
{
    internal const string PasswordResetRateLimitPolicy = "auth-password-reset";
    private static readonly string[] ValidRoles = ["Customer", "Staff", "Organizer", "Admin"];

    public static IEndpointRouteBuilder MapIdentityEndpointsInternal(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity")
            .WithTags("Identity");

        // ── Public endpoints (không yêu cầu xác thực) ──
        group.MapPost("/register", RegisterAsync)
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .RequireRateLimiting(PasswordResetRateLimitPolicy);
        group.MapPost("/reset-password", ResetPasswordAsync)
            .RequireRateLimiting(PasswordResetRateLimitPolicy);

        // ── Protected endpoints (yêu cầu JWT) ──
        group.MapGet("/me", GetProfileAsync).RequireAuthorization();
        group.MapPut("/me", UpdateProfileAsync).RequireAuthorization();
        group.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();

        // ── Admin endpoints (yêu cầu JWT + Role Admin) ──
        group.MapGet("/users", GetUsersAsync).RequireAuthorization(policy => policy.RequireRole("Admin"));
        group.MapPut("/users/{id:guid}/role", UpdateUserRoleAsync).RequireAuthorization(policy => policy.RequireRole("Admin"));
        group.MapPut("/users/{id:guid}/status", UpdateUserStatusAsync).RequireAuthorization(policy => policy.RequireRole("Admin"));

        return app;
    }

    /// <summary>
    /// POST /api/identity/register — Đăng ký tài khoản mới (mặc định role Customer).
    /// </summary>
    internal static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IValidator<RegisterRequest> validator,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService)
    {
        // 1. Validate đầu vào
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var error = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Validation(error).ToHttpResult();
        }

        // 2. Kiểm tra email đã tồn tại (pre-check)
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Result.Conflict("Email này đã được sử dụng.").ToHttpResult();
        }

        // 3. Tạo user & xử lý race condition mức database
        var user = ApplicationUser.Create(request.Email, request.FullName, request.PhoneNumber);
        IdentityResult createResult;
        try
        {
            createResult = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException)
        {
            // Bắt xung đột mức DB (Unique Index violation) khi có 2 request chạy đồng thời
            return Result.Conflict("Email này đã được sử dụng.").ToHttpResult();
        }

        if (!createResult.Succeeded)
        {
            // Nếu UserManager báo lỗi trùng Email hoặc UserName
            if (createResult.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Result.Conflict("Email này đã được sử dụng.").ToHttpResult();
            }

            var error = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result.Validation(error).ToHttpResult();
        }

        // 4. Gán role Customer
        await userManager.AddToRoleAsync(user, "Customer");

        // 5. Sinh token
        var authResponse = await jwtTokenService.BuildAuthResponseAsync(user);

        return Results.Created($"/api/identity/me", authResponse);
    }

    /// <summary>
    /// POST /api/identity/login — Đăng nhập bằng email/mật khẩu.
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService)
    {
        // 1. Validate đầu vào
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var error = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Validation(error).ToHttpResult();
        }

        // 2. Tìm user
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Result.Unauthorized("Email hoặc mật khẩu không chính xác.").ToHttpResult();
        }

        // 3. Kiểm tra tài khoản bị khóa
        if (!user.IsActive)
        {
            return Result.Forbidden("Tài khoản đã bị khóa.").ToHttpResult();
        }

        // 4. Kiểm tra mật khẩu
        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return Result.Unauthorized("Email hoặc mật khẩu không chính xác.").ToHttpResult();
        }

        // 5. Sinh token
        var authResponse = await jwtTokenService.BuildAuthResponseAsync(user);

        return Results.Ok(authResponse);
    }

    /// <summary>
    /// POST /api/identity/refresh — Làm mới cặp token (Token Rotation).
    /// </summary>
    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        JwtTokenService jwtTokenService)
    {
        var authResponse = await jwtTokenService.RefreshAsync(request.RefreshToken);
        if (authResponse is null)
        {
            return Result.Unauthorized("Refresh token không hợp lệ hoặc đã hết hạn.").ToHttpResult();
        }

        return Results.Ok(authResponse);
    }

    /// <summary>
    /// GET /api/identity/me — Xem hồ sơ cá nhân (yêu cầu JWT).
    /// </summary>
    private static async Task<IResult> GetProfileAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager)
    {
        var userId = GetUserIdFromClaims(httpContext);
        if (userId is null)
        {
            return Result.Unauthorized("Token không hợp lệ.").ToHttpResult();
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.NotFound("Không tìm thấy người dùng.").ToHttpResult();
        }

        var roles = await userManager.GetRolesAsync(user);

        var profile = new UserProfileResponse(
            Id: user.Id,
            Email: user.Email!,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            Roles: roles.ToList().AsReadOnly());

        return Results.Ok(profile);
    }

    /// <summary>
    /// PUT /api/identity/me — Cập nhật hồ sơ cá nhân (FullName, PhoneNumber).
    /// </summary>
    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager)
    {
        var userId = GetUserIdFromClaims(httpContext);
        if (userId is null)
        {
            return Result.Unauthorized("Token không hợp lệ.").ToHttpResult();
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.NotFound("Không tìm thấy người dùng.").ToHttpResult();
        }

        user.UpdateProfile(request.FullName, request.PhoneNumber);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var error = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return Result.Validation(error).ToHttpResult();
        }

        var roles = await userManager.GetRolesAsync(user);

        var profile = new UserProfileResponse(
            Id: user.Id,
            Email: user.Email!,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            Roles: roles.ToList().AsReadOnly());

        return Results.Ok(profile);
    }

    /// <summary>
    /// POST /api/identity/change-password — Đổi mật khẩu (yêu cầu mật khẩu cũ).
    /// </summary>
    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService)
    {
        var userId = GetUserIdFromClaims(httpContext);
        if (userId is null)
        {
            return Result.Unauthorized("Token không hợp lệ.").ToHttpResult();
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Result.NotFound("Không tìm thấy người dùng.").ToHttpResult();
        }

        var changeResult = await userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!changeResult.Succeeded)
        {
            var error = string.Join("; ", changeResult.Errors.Select(e => e.Description));
            return Result.Validation(error).ToHttpResult();
        }

        // Thu hồi toàn bộ RefreshToken — buộc đăng nhập lại
        await jwtTokenService.RevokeAllRefreshTokensAsync(user.Id);

        return Results.NoContent();
    }

    /// <summary>
    /// GET /api/identity/users — Lấy danh sách tất cả người dùng (chỉ Admin).
    /// </summary>
    private static async Task<IResult> GetUsersAsync(
        UserManager<ApplicationUser> userManager)
    {
        var users = await userManager.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var result = new List<AdminUserResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new AdminUserResponse(
                Id: user.Id,
                Email: user.Email!,
                FullName: user.FullName,
                PhoneNumber: user.PhoneNumber,
                IsActive: user.IsActive,
                CreatedAt: user.CreatedAt,
                Roles: roles.ToList().AsReadOnly()));
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// PUT /api/identity/users/{id}/role — Đổi vai trò người dùng (chỉ Admin).
    /// </summary>
    private static async Task<IResult> UpdateUserRoleAsync(
        Guid id,
        UpdateUserRoleRequest request,
        UserManager<ApplicationUser> userManager)
    {
        // 1. Chuẩn hóa và validate tên role
        var normalized = ValidRoles
            .FirstOrDefault(r => r.Equals(request.Role, StringComparison.OrdinalIgnoreCase));

        if (normalized is null)
        {
            return Result.Validation($"Vai trò '{request.Role}' không hợp lệ. Cho phép: {string.Join(", ", ValidRoles)}.").ToHttpResult();
        }

        // 2. Tìm user
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result.NotFound("Không tìm thấy người dùng.").ToHttpResult();
        }

        // 3. Xóa toàn bộ role cũ, gán role mới
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        var addResult = await userManager.AddToRoleAsync(user, normalized);
        if (!addResult.Succeeded)
        {
            var error = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return Result.Validation(error).ToHttpResult();
        }

        // 4. Trả về thông tin mới
        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new AdminUserResponse(
            Id: user.Id,
            Email: user.Email!,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            Roles: roles.ToList().AsReadOnly()));
    }

    /// <summary>
    /// PUT /api/identity/users/{id}/status — Khóa hoặc mở khóa tài khoản (chỉ Admin).
    /// </summary>
    private static async Task<IResult> UpdateUserStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
        UserManager<ApplicationUser> userManager)
    {
        // 1. Tìm user
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result.NotFound("Không tìm thấy người dùng.").ToHttpResult();
        }

        // 2. Cập nhật trạng thái
        user.SetActive(request.IsActive);
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var error = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return Result.Validation(error).ToHttpResult();
        }

        // 3. Trả về thông tin mới
        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new AdminUserResponse(
            Id: user.Id,
            Email: user.Email!,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            Roles: roles.ToList().AsReadOnly()));
    }

    /// <summary>
    /// POST /api/identity/forgot-password — Gửi mã OTP 6 chữ số đặt lại mật khẩu về email.
    /// </summary>
    internal static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        IValidator<ForgotPasswordRequest> validator,
        UserManager<ApplicationUser> userManager,
        PasswordResetOtpService otpService,
        INotificationsModuleApi notificationsApi,
        IOptions<PasswordResetOptions> passwordResetOptions,
        ILoggerFactory loggerFactory,
        IWebHostEnvironment env,
        CancellationToken cancellationToken = default)
    {
        const string neutralMessage = "Nếu email tồn tại trong hệ thống, mã xác nhận đặt lại mật khẩu đã được gửi.";

        var logger = loggerFactory.CreateLogger("Identity.ForgotPassword");
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var error = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Validation(error).ToHttpResult();
        }

        var options = passwordResetOptions.Value;
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return Results.Ok(new ForgotPasswordResponse(
                neutralMessage,
                ResetToken: null,
                CooldownSeconds: Math.Max(0, options.ResendCooldownSeconds)));
        }

        var issueResult = await otpService.IssueOtpAsync(user, cancellationToken);
        if (!issueResult.IsThrottled && issueResult.OtpCode is not null)
        {
            try
            {
                var expirationMinutes = Math.Clamp(options.ExpirationMinutes, 1, 60);
                await notificationsApi.SendPasswordResetOtpAsync(
                    user.Email!,
                    user.FullName,
                    issueResult.OtpCode,
                    expirationMinutes,
                    cancellationToken);

                logger.LogInformation("Password reset OTP sent to {Email}", user.Email);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to send password reset OTP email to {Email}", user.Email);
                if (issueResult.OtpId.HasValue)
                {
                    await otpService.RevokeOtpAsync(issueResult.OtpId.Value, cancellationToken);
                }

                return Results.Problem(
                    title: "Service Unavailable",
                    detail: "Không thể gửi email xác nhận lúc này. Vui lòng thử lại sau.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }
        else if (issueResult.IsThrottled)
        {
            logger.LogInformation(
                "Password reset OTP request throttled for {Email}; retry after {CooldownSeconds}s",
                user.Email,
                issueResult.CooldownSeconds);
        }

        var isDevOrTest = string.Equals(env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(env.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);
        var exposeDevOtp = options.ExposeDevOtpInResponse && isDevOrTest;

        return Results.Ok(new ForgotPasswordResponse(
            neutralMessage,
            ResetToken: exposeDevOtp ? issueResult.OtpCode : null,
            CooldownSeconds: issueResult.CooldownSeconds));
    }

    /// <summary>
    /// POST /api/identity/reset-password — Đặt lại mật khẩu bằng mã OTP 6 chữ số.
    /// </summary>
    internal static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        IValidator<ResetPasswordRequest> validator,
        UserManager<ApplicationUser> userManager,
        PasswordResetOtpService otpService,
        JwtTokenService jwtTokenService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger("Identity.ResetPassword");
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var error = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Validation(error).ToHttpResult();
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return Result.Validation("Mã xác nhận không hợp lệ hoặc đã hết hạn.").ToHttpResult();
        }

        var verifyResult = await otpService.VerifyOtpAsync(user, request.Token, cancellationToken);
        if (!verifyResult.IsValid || !verifyResult.OtpId.HasValue)
        {
            return Result.Validation(verifyResult.ErrorMessage ?? "Mã xác nhận không hợp lệ.").ToHttpResult();
        }

        var identityResetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, identityResetToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            var error = string.Join("; ", resetResult.Errors.Select(e => e.Description));
            return Result.Validation(error).ToHttpResult();
        }

        await otpService.MarkOtpUsedAsync(verifyResult.OtpId.Value, cancellationToken);
        await jwtTokenService.RevokeAllRefreshTokensAsync(user.Id);

        logger.LogInformation("Password successfully reset via OTP for user {Email}", user.Email);
        return Results.Ok(new ResetPasswordResponse("Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới."));
    }

    /// <summary>
    /// Trích xuất UserId từ JWT Claims.
    /// </summary>
    private static Guid? GetUserIdFromClaims(HttpContext httpContext)
    {
        var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}

