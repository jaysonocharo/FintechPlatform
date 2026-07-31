using BCrypt.Net;
using FintechBackend.Data;
using FintechBackend.DTOs;
using FintechBackend.Models;
using FintechBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace FintechBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            if (user == null)
            {
                return Unauthorized("Invalid credentials.");
            }

            // 2. Verify hashed password
            bool isValidPassword = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isValidPassword)
            {
                return Unauthorized("Invalid credentials.");
            }

            // 3. Generate token
            var token = _tokenService.CreateToken(user);
            return Ok(new AuthResponseDto { Token = token, Email = user.Email });
        }
    }
}