using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyProject.Domain.Entities;
using MyProject.Infrastructure.Persistence;

namespace MyProject.WebAPI.Controllers;

public sealed record LoginRequest(
    [Required, MaxLength(100)] string Username,
    [Required, MinLength(12), MaxLength(256)] string Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);

public sealed record RegisterCustomerRequest(
    [Required, MaxLength(100)] string Username,
    [Required, MinLength(12), MaxLength(256)] string Password,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [MaxLength(30)] string? PhoneNumber);

public sealed record RegisterCustomerResponse(Guid CustomerId, string Username);

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    AppDbContext dbContext,
    IPasswordHasher<InitialUser> passwordHasher,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return Unauthorized();

        var normalizedUsername = NormalizeUsername(request.Username);
        var user = await dbContext.Set<InitialUser>()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedUsername, cancellationToken);

        if (user is null)
        {
            var bootstrapResult = await TryCreateInitialUserAsync(
                request, normalizedUsername, cancellationToken);
            if (bootstrapResult is not null)
                return bootstrapResult;

            user = await dbContext.Set<InitialUser>()
                .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedUsername, cancellationToken);
        }

        if (user is null || !user.IsActive)
            return Unauthorized();

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized();

        if (user.CustomerId is Guid customerId &&
            !await dbContext.Customers.AnyAsync(customer => customer.Id == customerId && customer.IsActive, cancellationToken))
            return Unauthorized();

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, request.Password));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(CreateToken(user));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var normalizedUsername = NormalizeUsername(username);
        var email = request.Email.Trim().ToLowerInvariant();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        if (await dbContext.InitialUsers.IgnoreQueryFilters().AnyAsync(
                user => user.NormalizedUsername == normalizedUsername, cancellationToken) ||
            await dbContext.Customers.IgnoreQueryFilters().AnyAsync(
                customer => customer.Email == email, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Username or email is already in use."
            });
        }

        var customer = new Customer(
            request.FirstName.Trim(), request.LastName.Trim(), email, request.PhoneNumber?.Trim());
        var user = new InitialUser(username, normalizedUsername, string.Empty, customer.Id);
        user.UpdatePasswordHash(passwordHasher.HashPassword(user, request.Password));

        dbContext.Customers.Add(customer);
        dbContext.InitialUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Created($"/api/customers/{customer.Id}", new RegisterCustomerResponse(customer.Id, user.Username));
    }

    private async Task<IActionResult?> TryCreateInitialUserAsync(
        LoginRequest request,
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var existingUser = await dbContext.Set<InitialUser>()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedUsername == normalizedUsername, cancellationToken);
        if (existingUser is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (await dbContext.Set<InitialUser>().AnyAsync(cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return Unauthorized();
        }

        var bootstrapUsername = configuration["InitialUser:Username"]?.Trim();
        var bootstrapPassword = configuration["InitialUser:Password"];
        if (string.IsNullOrWhiteSpace(bootstrapUsername) || string.IsNullOrEmpty(bootstrapPassword))
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Initial user is not configured.",
                detail: "Configure InitialUser:Username and InitialUser:Password using User Secrets or environment variables.");

        if (bootstrapPassword.Length < 12)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Initial user configuration is invalid.",
                detail: "The initial user password must be at least 12 characters long.");

        if (!string.Equals(request.Username.Trim(), bootstrapUsername, StringComparison.OrdinalIgnoreCase) ||
            !FixedTimeEquals(request.Password, bootstrapPassword))
        {
            await transaction.CommitAsync(cancellationToken);
            return Unauthorized();
        }

        var normalizedBootstrapUsername = NormalizeUsername(bootstrapUsername);
        var initialUser = new InitialUser(
            bootstrapUsername, normalizedBootstrapUsername, string.Empty, isAdmin: true);
        initialUser.UpdatePasswordHash(passwordHasher.HashPassword(initialUser, bootstrapPassword));
        dbContext.Add(initialUser);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    private LoginResponse CreateToken(InitialUser user)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var secretKey = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
        {
            throw new InvalidOperationException("JWT issuer, audience, and a secret key of at least 32 bytes must be configured.");
        }

        var expiresAt = DateTime.UtcNow.AddHours(1);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "Customer"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (user.CustomerId is Guid customerId)
            claims = [.. claims, new Claim("customer_id", customerId.ToString())];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt, signingCredentials: credentials);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expiresAt);
    }

    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    private static bool FixedTimeEquals(string provided, string configured)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        return providedBytes.Length == configuredBytes.Length &&
            CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
