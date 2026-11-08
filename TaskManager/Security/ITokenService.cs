using TaskManager.DTO;
using TaskManager.Models;

namespace TaskManager.Security
{
    public interface ITokenService
    {
        TokenDTO GenerateToken(User user);    
    }
}