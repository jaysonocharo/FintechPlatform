using FintechBackend.Models;

namespace FintechBackend.Services;

public interface ITokenService
{
    string CreateToken(User user);
}