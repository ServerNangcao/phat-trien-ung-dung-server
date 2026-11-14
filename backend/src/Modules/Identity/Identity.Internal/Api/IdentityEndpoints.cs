using System.Security.Claims;
using FluentValidation;
using Identity.Internal.Application.DTOs;
using Identity.Internal.Application.Services;
using Identity.Internal.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Ticketing.Infrastructure.Core;
using Ticketing.SharedKernel;

namespace Identity.Internal.Api;

/// <summary>
/// 6 Minimal API endpoints cho module Identity.
/// </summary>
internal static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpointsInternal(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity")
            .WithTags("Identity");

        // ── Public endpoints (không yêu cầu xác thực) ──
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);

        // ── Protected endpoints (yêu cầu JWT) ──
        group.MapGet("/me", GetProfileAsync).RequireAuthorization();
        group.MapPut("/me", UpdateProfileAsync).RequireAuthorization();
        group.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();

        return app;
    }

    /// <summary>
    /// POST /api/identity/register — Đăng ký tài khoản mới (mặc định role Customer).
    /// </summary>
    private static async Task<IResult> RegisterAsync(
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

        // 2. Kiểm tra email đã tồn tại
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Result.Conflict("Email này đã được sử dụng.").ToHttpResult();
        }

        // 3. Tạo user
        var user = ApplicationUser.Create(request.Email, request.FullName, request.PhoneNumber);
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
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
    /// Trích xuất UserId từ JWT Claims.
    /// </summary>
    private static Guid? GetUserIdFromClaims(HttpContext httpContext)
    {
        var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}

