namespace FintechBackend.DTOs;

public record UserRegistrationDto(string Email, string Password);

public record UserLoginDto(string Email, string Password);