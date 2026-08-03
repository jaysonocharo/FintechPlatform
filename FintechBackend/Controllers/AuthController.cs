using BCrypt.Net;
using FintechBackend.Data;
using FintechBackend.DTOs;
using FintechBackend.Models;
using FintechBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // Provides the [Authorize] attribute.
using System.Security.Claims; // Provides ClaimTypes.NameIdentifier and ClaimTypes.Email constants.


namespace FintechBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // dynamically swaps the [controller] token with the name of your class, minus the word "Controller". eg 
    //cont. a class named AuthController, Swagger reads this literally as Auth.
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;

        public AuthController(AppDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
        {
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
                PasswordHash = passwordHash
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
            // 1. Find user by email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());
            
            // 2. Prevent timing attacks (Account Enumeration)
            // BCrypt hashing is intentionally slow (~100ms+). 
            // If user is null and you return immediately, an attacker can measure 
            // response times to determine if an email exists in your database.
            bool isValidPassword = user != null && BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

            if (!isValidPassword)
            {
                return Unauthorized("Invalid email or password.");
            }

            // 3. Generate token
            var token = _tokenService.CreateToken(user!);
            return Ok(new AuthResponseDto { Token = token, Email = user!.Email });
        }

        // [HttpPost("login")]
        // [ProducesResponseType(StatusCodes.Status200OK)]
        // [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        // public async Task<IActionResult> Login([FromBody] LoginDto dto)
        // {
        //     // 1. Fetch user by email (case-insensitive lookup depending on DB collation)
        //     var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        //     // 2. Validate user existence and password hash in a single check
        //     if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        //     {
        //         // Generic message avoids telling an attacker whether the email exists
        //         return Unauthorized(new { message = "Invalid email or password." });
        //     }

        //     // 3. Generate token using your existing JWT service
        //     var token = _jwtService.GenerateToken(user);

        //     // 4. Return token response
        //     return Ok(new 
        //     { 
        //         token,
        //         email = user.Email
        //     });
        // }

        // [HttpPost("login")]
        // public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        // {
        //     // 1. Find user by email
        //     var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());
        //     if (user == null)
        //     {
        //         return Unauthorized("Invalid credentials.");
        //     }

        //     // 2. Verify hashed password
        //     bool isValidPassword = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        //     if (!isValidPassword)
        //     {
        //         return Unauthorized("Invalid credentials.");
        //     }

        //     // 3. Generate token
        //     var token = _tokenService.CreateToken(user);
        //     return Ok(new AuthResponseDto { Token = token, Email = user.Email });
        // }

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
    }
}