using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Identity.Internal.Api;
using Identity.Internal.Application.DTOs;
using Identity.Internal.Application.Services;
using Identity.Internal.Application.Validators;
using Identity.Internal.Domain;
using Identity.Internal.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Notifications.Contracts;
using Ticketing.Api.Middlewares;
using Ticketing.SharedKernel;
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

    [Fact]
    public void ForgotPasswordRequestValidator_ValidRequest_PassesValidation()
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest(Email: "user@example.com");

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("   ")]
    public void ForgotPasswordRequestValidator_InvalidEmail_FailsValidation(string email)
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest(Email: email);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResetPasswordRequestValidator_ValidRequest_PassesValidation()
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest(
            Email: "user@example.com",
            Token: "123456",
            NewPassword: "NewSecurePassword123!");

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "123456", "NewSecurePassword123!")]
    [InlineData("invalid-email", "123456", "NewSecurePassword123!")]
    [InlineData("user@example.com", "", "NewSecurePassword123!")]
    [InlineData("user@example.com", "12345", "NewSecurePassword123!")]
    [InlineData("user@example.com", "abcdef", "NewSecurePassword123!")]
    [InlineData("user@example.com", "1234567", "NewSecurePassword123!")]
    [InlineData("user@example.com", "123456", "short")]
    [InlineData("user@example.com", "123456", "")]
    public void ResetPasswordRequestValidator_InvalidRequest_FailsValidation(
        string email, string token, string newPassword)
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest(Email: email, Token: token, NewPassword: newPassword);

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

    // ───────────────────────────────────────────────
    // 6. RegisterAsync Conflict (409) & Race Condition Tests
    // ───────────────────────────────────────────────
    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsConflict409()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("existing@example.com", "Existing User", "SecurePassword123!", PhoneNumber: null);
        var existingUser = ApplicationUser.Create("existing@example.com", "Existing User");
        var userManager = new RegisterTestUserManager(existingUser: existingUser);

        var result = await IdentityEndpoints.RegisterAsync(request, validator, userManager, null!);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusCodeResult.StatusCode);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal("Email này đã được sử dụng.", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task RegisterAsync_WhenConcurrentRaceConditionDuplicateError_ReturnsConflict409()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("race@example.com", "Race User", "SecurePassword123!", PhoneNumber: null);
        var duplicateError = IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email is taken." });
        var userManager = new RegisterTestUserManager(existingUser: null, createResult: duplicateError);

        var result = await IdentityEndpoints.RegisterAsync(request, validator, userManager, null!);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusCodeResult.StatusCode);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal("Email này đã được sử dụng.", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task RegisterAsync_WhenConcurrentRaceConditionDbUpdateException_ReturnsConflict409()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("race@example.com", "Race User", "SecurePassword123!", PhoneNumber: null);
        var userManager = new RegisterTestUserManager(existingUser: null, throwDbUpdateOnCreate: true);

        var result = await IdentityEndpoints.RegisterAsync(request, validator, userManager, null!);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusCodeResult.StatusCode);

        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal("Email này đã được sử dụng.", problemResult.ProblemDetails.Detail);
    }

    // ───────────────────────────────────────────────
    // 7. PasswordResetOtp Domain & End-to-End OTP Flow Tests
    // ───────────────────────────────────────────────
    [Fact]
    public void PasswordResetOtp_DomainLifecycle_TracksExpirationAttemptsAndUsage()
    {
        var now = new DateTime(2026, 11, 27, 8, 0, 0, DateTimeKind.Utc);
        var otp = PasswordResetOtp.Create(
            Guid.NewGuid(),
            "USER@EXAMPLE.COM",
            "HASHED",
            now,
            TimeSpan.FromMinutes(10),
            maxAttempts: 2);

        Assert.True(otp.CanVerify(now.AddMinutes(5)));
        Assert.False(otp.IsExpired(now.AddMinutes(9)));
        Assert.True(otp.IsExpired(now.AddMinutes(10)));

        otp.RecordFailedAttempt(now.AddMinutes(1));
        Assert.False(otp.IsExhausted);
        Assert.True(otp.CanVerify(now.AddMinutes(2)));

        otp.RecordFailedAttempt(now.AddMinutes(2));
        Assert.True(otp.IsExhausted);
        Assert.False(otp.CanVerify(now.AddMinutes(3)));

        otp.MarkUsed(now.AddMinutes(3));
        Assert.True(otp.IsUsed);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ValidUser_IssuesHashedOtpAndSendsEmailWithoutLeakingOtpInResponse()
    {
        using var harness = CreateOtpTestHarness();
        var user = await harness.SeedUserAsync("otp-user@example.com", "Otp User");

        var result = await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<ForgotPasswordResponse>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Null(okResult.Value.ResetToken);
        Assert.Equal(60, okResult.Value.CooldownSeconds);

        Assert.Single(harness.NotificationsApi.SentOtps);
        var sent = harness.NotificationsApi.SentOtps[0];
        Assert.Equal(user.Email, sent.ToEmail);
        Assert.Matches(@"^\d{6}$", sent.OtpCode);
        Assert.Equal(10, sent.ExpiresInMinutes);

        var storedOtp = await harness.DbContext.PasswordResetOtps.SingleAsync(x => x.UserId == user.Id);
        Assert.NotEqual(sent.OtpCode, storedOtp.CodeHash);
        Assert.Equal(
            PasswordResetOtpService.HashCode(PasswordResetOtpService.NormalizeEmail(user.Email!), sent.OtpCode),
            storedOtp.CodeHash);
        Assert.Null(storedOtp.UsedAt);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnknownUser_ReturnsNeutralOkWithoutSendingEmail()
    {
        using var harness = CreateOtpTestHarness();

        var result = await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest("missing@example.com"),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<ForgotPasswordResponse>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Null(okResult.Value.ResetToken);
        Assert.Empty(harness.NotificationsApi.SentOtps);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithinCooldown_DoesNotSendDuplicateEmail()
    {
        using var harness = CreateOtpTestHarness();
        var user = await harness.SeedUserAsync("cooldown@example.com", "Cooldown User");

        await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        harness.Clock.Advance(TimeSpan.FromSeconds(20));

        var secondResult = await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<ForgotPasswordResponse>>(secondResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal(40, okResult.Value.CooldownSeconds);
        Assert.Single(harness.NotificationsApi.SentOtps);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenEmailSendFails_RevokesOtpAndReturns503()
    {
        using var harness = CreateOtpTestHarness();
        var user = await harness.SeedUserAsync("smtp-fail@example.com", "Smtp Fail");
        harness.NotificationsApi.ShouldThrow = true;

        var result = await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCodeResult.StatusCode);
        Assert.Empty(await harness.DbContext.PasswordResetOtps.ToListAsync());

        // Khi email lỗi, OTP đã được thu hồi nên user có thể thử lại ngay khi SMTP hoạt động lại
        harness.NotificationsApi.ShouldThrow = false;
        var retryResult = await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        Assert.IsType<Ok<ForgotPasswordResponse>>(retryResult);
        Assert.Single(harness.NotificationsApi.SentOtps);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidOtp_ResetsPasswordMarksOtpUsedAndRevokesRefreshTokens()
    {
        using var harness = CreateOtpTestHarness();
        var user = await harness.SeedUserAsync("reset-ok@example.com", "Reset OK");
        var refreshToken = RefreshToken.Create(user.Id, "active-refresh-hash", harness.Clock.UtcNow.AddDays(7));
        harness.DbContext.RefreshTokens.Add(refreshToken);
        await harness.DbContext.SaveChangesAsync();

        await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var otpCode = harness.NotificationsApi.SentOtps.Single().OtpCode;

        var resetResult = await IdentityEndpoints.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email!, otpCode, "NewSecurePassword123!"),
            new ResetPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.JwtTokenService,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var okResult = Assert.IsType<Ok<ResetPasswordResponse>>(resetResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal(1, harness.UserManager.ResetPasswordCallCount);

        var storedOtp = await harness.DbContext.PasswordResetOtps.SingleAsync(x => x.UserId == user.Id);
        Assert.True(storedOtp.IsUsed);

        var storedRefresh = await harness.DbContext.RefreshTokens.SingleAsync(x => x.Id == refreshToken.Id);
        Assert.True(storedRefresh.IsRevoked);

        // Dùng lại chính OTP đó lần nữa phải bị từ chối
        var replayResult = await IdentityEndpoints.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email!, otpCode, "AnotherSecurePass123!"),
            new ResetPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.JwtTokenService,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var replayStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(replayResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, replayStatus.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidOtp_IncrementsAttemptsAndLocksAfterMaxAttempts()
    {
        using var harness = CreateOtpTestHarness();
        var user = await harness.SeedUserAsync("lockout@example.com", "Lockout User");

        await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var realOtp = harness.NotificationsApi.SentOtps.Single().OtpCode;
        var wrongOtp = realOtp == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            var failResult = await IdentityEndpoints.ResetPasswordAsync(
                new ResetPasswordRequest(user.Email!, wrongOtp, "NewSecurePassword123!"),
                new ResetPasswordRequestValidator(),
                harness.UserManager,
                harness.OtpService,
                harness.JwtTokenService,
                NullLoggerFactory.Instance,
                CancellationToken.None);

            var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(failResult);
            Assert.Equal(StatusCodes.Status422UnprocessableEntity, status.StatusCode);
        }

        // Sau 5 lần sai, ngay cả OTP đúng cũng bị khóa
        var lockedResult = await IdentityEndpoints.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email!, realOtp, "NewSecurePassword123!"),
            new ResetPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.JwtTokenService,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(lockedResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        Assert.Contains("quá nhiều lần", problem.ProblemDetails.Detail);
        Assert.Equal(0, harness.UserManager.ResetPasswordCallCount);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredOtp_FailsValidation()
    {
        using var harness = CreateOtpTestHarness();
        var user = await harness.SeedUserAsync("expired@example.com", "Expired User");

        await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var otpCode = harness.NotificationsApi.SentOtps.Single().OtpCode;
        harness.Clock.Advance(TimeSpan.FromMinutes(11));

        var result = await IdentityEndpoints.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email!, otpCode, "NewSecurePassword123!"),
            new ResetPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.JwtTokenService,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        Assert.Contains("hết hạn", problem.ProblemDetails.Detail);
        Assert.Equal(0, harness.UserManager.ResetPasswordCallCount);
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPasswordWithValidOtp_DoesNotConsumeOtp()
    {
        using var harness = CreateOtpTestHarness();
        var user = await harness.SeedUserAsync("weak-pass@example.com", "Weak Pass");
        harness.UserManager.NextResetResult = IdentityResult.Failed(
            new IdentityError { Code = "PasswordRequiresUpper", Description = "Password requires uppercase." });

        await IdentityEndpoints.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            new ForgotPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.NotificationsApi,
            harness.Options,
            NullLoggerFactory.Instance,
            harness.HostEnvironment,
            CancellationToken.None);

        var otpCode = harness.NotificationsApi.SentOtps.Single().OtpCode;

        var failedReset = await IdentityEndpoints.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email!, otpCode, "lowercaseonly1!"),
            new ResetPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.JwtTokenService,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var failStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(failedReset);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, failStatus.StatusCode);

        var otpBeforeRetry = await harness.DbContext.PasswordResetOtps.SingleAsync(x => x.UserId == user.Id);
        Assert.False(otpBeforeRetry.IsUsed);

        // Thử lại với mật khẩu hợp lệ vẫn dùng được chính mã OTP đó
        harness.UserManager.NextResetResult = IdentityResult.Success;
        var successReset = await IdentityEndpoints.ResetPasswordAsync(
            new ResetPasswordRequest(user.Email!, otpCode, "ValidStrongPassword123!"),
            new ResetPasswordRequestValidator(),
            harness.UserManager,
            harness.OtpService,
            harness.JwtTokenService,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        Assert.IsType<Ok<ResetPasswordResponse>>(successReset);
        var otpAfterSuccess = await harness.DbContext.PasswordResetOtps.SingleAsync(x => x.UserId == user.Id);
        Assert.True(otpAfterSuccess.IsUsed);
    }

    private static OtpTestHarness CreateOtpTestHarness() => new();

    private sealed class OtpTestHarness : IDisposable
    {
        private readonly SqliteConnection _connection;

        public IdentityDbContext DbContext { get; }
        public MutableClock Clock { get; }
        public IOptions<PasswordResetOptions> Options { get; }
        public PasswordResetOtpService OtpService { get; }
        public FakeNotificationsModuleApi NotificationsApi { get; }
        public OtpTestUserManager UserManager { get; }
        public JwtTokenService JwtTokenService { get; }
        public IWebHostEnvironment HostEnvironment { get; }

        public OtpTestHarness()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(_connection)
                .Options;

            DbContext = new IdentityDbContext(dbOptions);
            DbContext.Database.EnsureCreated();

            Clock = new MutableClock(new DateTime(2026, 11, 27, 8, 0, 0, DateTimeKind.Utc));
            Options = Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions
            {
                ExpirationMinutes = 10,
                MaxAttempts = 5,
                ResendCooldownSeconds = 60,
                ExposeDevOtpInResponse = false
            });

            OtpService = new PasswordResetOtpService(DbContext, Clock, Options);
            NotificationsApi = new FakeNotificationsModuleApi();
            UserManager = new OtpTestUserManager();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "ThisIsASecretKeyForTestingJwtTokenServiceAtLeast32Bytes!",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience"
                })
                .Build();

            JwtTokenService = new JwtTokenService(
                config,
                UserManager,
                DbContext,
                NullLogger<JwtTokenService>.Instance);

            HostEnvironment = new FakeHostEnvironment("Development");
        }

        public async Task<ApplicationUser> SeedUserAsync(string email, string fullName)
        {
            var user = ApplicationUser.Create(email, fullName);
            DbContext.Users.Add(user);
            await DbContext.SaveChangesAsync();
            UserManager.AddUser(user);
            return user;
        }

        public void Dispose()
        {
            DbContext.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class MutableClock(DateTime initialUtcNow) : IClock
    {
        public DateTime UtcNow { get; private set; } = initialUtcNow;
        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    private sealed class FakeNotificationsModuleApi : INotificationsModuleApi
    {
        public List<(string ToEmail, string FullName, string OtpCode, int ExpiresInMinutes)> SentOtps { get; } = [];
        public bool ShouldThrow { get; set; }

        public Task SendPasswordResetOtpAsync(
            string toEmail,
            string fullName,
            string otpCode,
            int expiresInMinutes,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated SMTP outage.");
            }

            SentOtps.Add((toEmail, fullName, otpCode, expiresInMinutes));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Ticketing.Api";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class OtpTestUserManager : UserManager<ApplicationUser>
    {
        private readonly Dictionary<string, ApplicationUser> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);

        public IdentityResult NextResetResult { get; set; } = IdentityResult.Success;
        public int ResetPasswordCallCount { get; private set; }

        public OtpTestUserManager()
            : base(new TestUserStore(), null!, null!, null!, null!, null!, null!, null!, null!)
        {
        }

        public void AddUser(ApplicationUser user)
        {
            if (user.Email is not null)
            {
                _usersByEmail[user.Email] = user;
            }
        }

        public override Task<ApplicationUser?> FindByEmailAsync(string email) =>
            Task.FromResult(_usersByEmail.GetValueOrDefault(email));

        public override Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user) =>
            Task.FromResult("internal-identity-reset-token");

        public override Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
        {
            ResetPasswordCallCount++;
            return Task.FromResult(NextResetResult);
        }

        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) =>
            Task.FromResult<IList<string>>(["Customer"]);
    }

    private sealed class RegisterTestUserManager : UserManager<ApplicationUser>
    {
        private readonly ApplicationUser? _existingUser;
        private readonly IdentityResult _createResult;
        private readonly bool _throwDbUpdateOnCreate;

        public RegisterTestUserManager(
            ApplicationUser? existingUser = null,
            IdentityResult? createResult = null,
            bool throwDbUpdateOnCreate = false)
            : base(new TestUserStore(), null!, null!, null!, null!, null!, null!, null!, null!)
        {
            _existingUser = existingUser;
            _createResult = createResult ?? IdentityResult.Success;
            _throwDbUpdateOnCreate = throwDbUpdateOnCreate;
        }

        public override Task<ApplicationUser?> FindByEmailAsync(string email) =>
            Task.FromResult(_existingUser);

        public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
        {
            if (_throwDbUpdateOnCreate)
            {
                throw new DbUpdateException("Duplicate key violation", (Exception?)null);
            }
            return Task.FromResult(_createResult);
        }

        public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role) =>
            Task.FromResult(IdentityResult.Success);

        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) =>
            Task.FromResult<IList<string>>(["Customer"]);
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

