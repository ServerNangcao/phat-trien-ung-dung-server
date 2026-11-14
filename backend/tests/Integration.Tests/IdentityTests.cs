using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Identity.Internal.Application.DTOs;
using Identity.Internal.Application.Services;
using Identity.Internal.Application.Validators;
using Identity.Internal.Domain;
using Identity.Internal.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Api.Middlewares;
using Xunit;

namespace Integration.Tests;

public class IdentityTests
{
    // ───────────────────────────────────────────────
    // 1. Validator Tests
    // ───────────────────────────────────────────────
    [Fact]
    public void RegisterRequestValidator_ValidRequest_PassesValidation()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: "user@example.com",
            FullName: "Nguyen Van A",
            Password: "SecurePassword123!",
            PhoneNumber: "0901234567");

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Nguyen Van A", "SecurePassword123!")]
    [InlineData("not-an-email", "Nguyen Van A", "SecurePassword123!")]
    [InlineData("user@example.com", "", "SecurePassword123!")]
    [InlineData("user@example.com", "Nguyen Van A", "short")]
    [InlineData("user@example.com", "Nguyen Van A", "")]
    public void RegisterRequestValidator_InvalidRequest_FailsValidation(
        string email, string fullName, string password)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            Email: email,
            FullName: fullName,
            Password: password,
            PhoneNumber: null);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void LoginRequestValidator_ValidRequest_PassesValidation()
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(
            Email: "user@example.com",
            Password: "SecurePassword123!");

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("invalid-email", "password")]
    [InlineData("user@example.com", "")]
    public void LoginRequestValidator_InvalidRequest_FailsValidation(
        string email, string password)
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest(Email: email, Password: password);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
    }

    // ───────────────────────────────────────────────
    // 2. Domain Entity Tests: ApplicationUser
    // ───────────────────────────────────────────────
    [Fact]
    public void ApplicationUser_Create_InitializesPropertiesCorrectly()
    {
        var email = "customer@example.com";
        var fullName = "Tran Van B";
        var phone = "0987654321";

        var user = ApplicationUser.Create(email, fullName, phone);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(email, user.UserName);
        Assert.Equal(fullName, user.FullName);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.True(user.IsActive);
        Assert.True((DateTime.UtcNow - user.CreatedAt).TotalSeconds < 5);
    }

    [Fact]
    public void ApplicationUser_UpdateProfile_ModifiesFullNameAndPhoneNumber()
    {
        var user = ApplicationUser.Create("test@example.com", "Old Name", "0111111111");

        user.UpdateProfile("New Name", "0222222222");

        Assert.Equal("New Name", user.FullName);
        Assert.Equal("0222222222", user.PhoneNumber);
    }

    // ───────────────────────────────────────────────
    // 3. Domain Entity Tests: RefreshToken
    // ───────────────────────────────────────────────
    [Fact]
    public void RefreshToken_Create_SetsPropertiesCorrectly()
    {
        var userId = Guid.NewGuid();
        var hash = "hashed_token_value";
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = RefreshToken.Create(userId, hash, expiresAt);

        Assert.NotEqual(Guid.Empty, token.Id);
        Assert.Equal(userId, token.UserId);
        Assert.Equal(hash, token.TokenHash);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.ReplacedByTokenId);
    }

    [Fact]
    public void RefreshToken_Revoke_MarksTokenAsRevoked()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7));
        var replacedById = Guid.NewGuid();

        token.Revoke(replacedById);

        Assert.True(token.IsRevoked);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal(replacedById, token.ReplacedByTokenId);
    }

    [Fact]
    public void RefreshToken_IsExpired_ReturnsTrueWhenPastExpiration()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(-1));

        Assert.True(token.IsExpired);
    }

    // ───────────────────────────────────────────────
    // 4. SecurityHeadersMiddleware Tests
    // ───────────────────────────────────────────────
    [Fact]
    public async Task SecurityHeadersMiddleware_AddsExpectedHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.IsHttps = true;

        var middleware = new SecurityHeadersMiddleware(next: (innerHttpContext) => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("0", context.Response.Headers["X-XSS-Protection"]);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal("max-age=31536000; includeSubDomains", context.Response.Headers["Strict-Transport-Security"]);
    }

    // ───────────────────────────────────────────────
    // 5. JwtTokenService Tests
    // ───────────────────────────────────────────────
    [Fact]
    public async Task JwtTokenService_GenerateAccessTokenAsync_CreatesValidJwtWithClaims()
    {
        const string secret = "ThisIsASecretKeyForTestingJwtTokenServiceAtLeast32Bytes!";
        const string issuer = "test-issuer";
        const string audience = "test-audience";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:ExpirationInMinutes"] = "30"
            })
            .Build();

        var user = ApplicationUser.Create("alice@example.com", "Alice Nguyen");
        var fakeUserManager = new TestUserManager(["Customer", "Organizer"]);

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        using var dbContext = new IdentityDbContext(options);
        dbContext.Database.EnsureCreated();

        var service = new JwtTokenService(
            config,
            fakeUserManager,
            dbContext,
            NullLogger<JwtTokenService>.Instance);

        var (tokenString, expiresAt) = await service.GenerateAccessTokenAsync(user);

        Assert.NotEmpty(tokenString);
        Assert.True(expiresAt > DateTime.UtcNow);

        // Xác thực token và giải mã claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        var principal = tokenHandler.ValidateToken(tokenString, validationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);

        var claims = principal.Claims.ToList();
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Customer");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Organizer");
        Assert.Contains(claims, c => (c.Type == ClaimTypes.Email || c.Type == JwtRegisteredClaimNames.Email) && c.Value == "alice@example.com");
        Assert.Contains(claims, c => (c.Type == ClaimTypes.Name || c.Type == JwtRegisteredClaimNames.Name) && c.Value == "Alice Nguyen");
        Assert.Contains(claims, c => (c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub) && c.Value == user.Id.ToString());
    }

    [Fact]
    public async Task JwtTokenService_GenerateRefreshToken_StoresHashedTokenInDb()
    {
        const string secret = "ThisIsASecretKeyForTestingJwtTokenServiceAtLeast32Bytes!";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret,
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test"
            })
            .Build();

        var user = ApplicationUser.Create("bob@example.com", "Bob Tran");
        var fakeUserManager = new TestUserManager(["Customer"]);

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        using var dbContext = new IdentityDbContext(options);
        dbContext.Database.EnsureCreated();

        // Thêm user vào db để thỏa mãn Foreign Key constraint
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new JwtTokenService(
            config,
            fakeUserManager,
            dbContext,
            NullLogger<JwtTokenService>.Instance);

        var rawRefreshToken = await service.GenerateRefreshTokenAsync(user.Id);

        Assert.NotEmpty(rawRefreshToken);

        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.UserId == user.Id);
        Assert.NotNull(storedToken);
        Assert.NotEqual(rawRefreshToken, storedToken.TokenHash); // Không được lưu raw token!
        Assert.False(storedToken.IsRevoked);
        Assert.True(storedToken.ExpiresAt > DateTime.UtcNow);
    }

    private sealed class TestUserManager : UserManager<ApplicationUser>
    {
        private readonly IList<string> _roles;

        public TestUserManager(IList<string> roles)
            : base(
                new TestUserStore(),
                null!, null!, null!, null!, null!, null!, null!, null!)
        {
            _roles = roles;
        }

        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) =>
            Task.FromResult(_roles);
    }

    private sealed class TestUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken ct) => Task.CompletedTask;
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
    }
}

