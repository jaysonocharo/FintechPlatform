using BCrypt.Net;
using FintechBackend.Data;
using FintechBackend.DTOs;
using FintechBackend.Models;
using FintechBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // Provides the [Authorize] attribute.
using System.Security.Claims; // Provides ClaimTypes.NameIdentifier and ClaimTypes.Email constants.
using FintechBackend.Constants;
using FintechBackend.Extensions;
using FluentValidation;
using Ganss.Xss;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;


namespace FintechBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // dynamically swaps the [controller] token with the name of your class, minus the word "Controller". eg a class named AuthController, Swagger reads this literally as Auth.
    [EnableRateLimiting("AuthPolicy")]// Enforces 5 req/min
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IValidator<RegisterDto> _registerValidator;
        private readonly IValidator<LoginDto> _loginValidator;

        //Injects them into the constructor
        public AuthController(
            AppDbContext context, 
            ITokenService tokenService,
            IValidator<RegisterDto> registerValidator,
            IValidator<LoginDto> loginValidator)
        {
            _context = context;
            _tokenService = tokenService;
            _registerValidator = registerValidator;
            _loginValidator = loginValidator;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
        {
            // Validate the payload immediately
            await _registerValidator.ValidateAndThrowAsync(dto);
            // 1. Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
            {
                return BadRequest("Email address is already in use.");
            }

            // 2. Hash password using BCrypt
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // 3. Create User entity
            var user = new User
            {
                Email = dto.Email.ToLower(),
                PasswordHash = passwordHash,
                Role = Roles.User
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 4. Generate token and return
            var token = _tokenService.CreateToken(user);
            return Ok(new AuthResponseDto { Token = token, Email = user.Email });
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            // Validate the payload immediately
            await _loginValidator.ValidateAndThrowAsync(dto);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();

            // 1. Find user by email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                // Never log dto.Password in Serilog
                Log.Warning("SECURITY EVENT: Failed login attempt for email {Email} from IP {IpAddress}", dto.Email, ipAddress);

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = user?.Id.ToString(),
                    Action = "USER_LOGIN_FAILED",
                    EntityName = "User",
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    TimestampUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Unauthorized("Invalid credentials.");
            }
            
            Log.Information("SECURITY EVENT: Successful login for User ID {UserId} from IP {IpAddress}", user.Id, ipAddress);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id.ToString(),
                Action = "USER_LOGIN_SUCCESSFUL",
                EntityName = "User",
                EntityId = user.Id.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                TimestampUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // 3. Generate token
            var token = _tokenService.CreateToken(user!);
            return Ok(new AuthResponseDto { Token = token, Email = user!.Email });
        }

        // Security Test Endpoint: Requires a valid JWT token in the Authorization header
        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            // Extract claims embedded inside the JWT token
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email);

            return Ok(new
            {
                Message = "You accessed a protected route successfully!",
                UserId = userId,
                Email = email
            });
        }

        [HttpGet("admin-only")]
        [Authorize(Roles = Roles.Admin)] // This tells .NET to check the JWT for the Admin role claim
        public IActionResult AdminOnlyEndpoint()
        {
            return Ok(new { Message = "Success! You are authenticated as an Admin." });
        }

    }
}