using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Internal.Domain;
using Identity.Internal.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Identity.Internal.Application.DTOs;

namespace Identity.Internal.Application.Services;

/// <summary>
/// Service sinh JWT Access Token và quản lý Refresh Token (Token Rotation).
/// Refresh Token được hash SHA-256 trước khi lưu DB — không bao giờ lưu plaintext.
/// </summary>
internal sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IdentityDbContext _dbContext;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        IdentityDbContext dbContext,
        ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Sinh JWT Access Token chứa Claims: sub, email, name, roles.
    /// Hạn mặc định 30 phút (cấu hình qua Jwt:ExpirationInMinutes).
    /// </summary>
    public async Task<(string Token, DateTime ExpiresAt)> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secret = jwtSection["Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured.");
        var issuer = jwtSection["Issuer"] ?? "ticketing-api";
        var audience = jwtSection["Audience"] ?? "ticketing-client";
        var expirationMinutes = int.TryParse(jwtSection["ExpirationInMinutes"], out var m) ? m : 30;

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>
    /// Sinh Refresh Token: chuỗi ngẫu nhiên 64 byte → Base64,
    /// hash SHA-256 rồi lưu vào bảng RefreshTokens.
    /// Thời gian sống: 7 ngày.
    /// </summary>
    public async Task<string> GenerateRefreshTokenAsync(Guid userId)
    {
        // Sinh token ngẫu nhiên
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes);

        // Hash trước khi lưu DB
        var tokenHash = HashToken(rawToken);

        var refreshToken = RefreshToken.Create(
            userId,
            tokenHash,
            DateTime.UtcNow.AddDays(7));

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        return rawToken;
    }

    /// <summary>
    /// Xây dựng AuthResponse đầy đủ từ user.
    /// </summary>
    public async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user)
    {
        var (accessToken, expiresAt) = await GenerateAccessTokenAsync(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);
        var roles = await _userManager.GetRolesAsync(user);

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: expiresAt,
            UserId: user.Id,
            Email: user.Email!,
            FullName: user.FullName,
            Roles: roles.ToList().AsReadOnly());
    }

    /// <summary>
    /// Làm mới cặp token (Token Rotation):
    /// 1. Tìm token cũ trong DB theo SHA256(rawToken).
    /// 2. Kiểm tra chưa hết hạn và chưa bị thu hồi.
    /// 3. Thu hồi token cũ, ghi ReplacedByTokenId.
    /// 4. Sinh cặp token mới.
    /// </summary>
    public async Task<AuthResponse?> RefreshAsync(string rawRefreshToken)
    {
        var tokenHash = HashToken(rawRefreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (existingToken is null)
        {
            _logger.LogWarning("Refresh token not found in database");
            return null;
        }

        if (existingToken.IsRevoked)
        {
            // Phát hiện tái sử dụng token bị đánh cắp → thu hồi toàn bộ token của user
            _logger.LogWarning(
                "Reuse of revoked refresh token detected for user {UserId}. Revoking all tokens.",
                existingToken.UserId);
            await RevokeAllRefreshTokensAsync(existingToken.UserId);
            return null;
        }

        if (existingToken.IsExpired)
        {
            _logger.LogWarning("Refresh token expired for user {UserId}", existingToken.UserId);
            return null;
        }

        // Tìm user
        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return null;
        }

        // Sinh token mới
        var (newAccessToken, expiresAt) = await GenerateAccessTokenAsync(user);
        var newRawRefreshToken = await GenerateRefreshTokenAsync(user.Id);

        // Lấy ID của token mới vừa tạo để ghi vào ReplacedByTokenId
        var newTokenHash = HashToken(newRawRefreshToken);
        var newToken = await _dbContext.RefreshTokens
            .FirstAsync(x => x.TokenHash == newTokenHash);

        // Thu hồi token cũ (Rotation)
        existingToken.Revoke(newToken.Id);
        await _dbContext.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);

        return new AuthResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRawRefreshToken,
            ExpiresAt: expiresAt,
            UserId: user.Id,
            Email: user.Email!,
            FullName: user.FullName,
            Roles: roles.ToList().AsReadOnly());
    }

    /// <summary>
    /// Thu hồi toàn bộ Refresh Token của user (dùng khi đổi mật khẩu, logout).
    /// </summary>
    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Revoked {Count} refresh tokens for user {UserId}",
            tokens.Count, userId);
    }

    /// <summary>
    /// Hash token bằng SHA-256 để lưu an toàn trong DB.
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}

