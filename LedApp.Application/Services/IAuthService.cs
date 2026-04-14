using LedApp.Application.DTOs;
using System.Threading.Tasks;

namespace LedApp.Application.Services
{
    public interface IAuthService
    {
        // Login başarılıysa geriye UserDto döner, hatalıysa null.
        Task<UserDto> LoginAsync(string email, string password);

        // Şifreyi güvenlikten (BCrypt) geçirerek kaydeder ve sonucu döndürür.
        Task<UserDto> RegisterAsync(CreateUserDto createUserDto);

        Task<bool> CheckEmailExistsAsync(string email);
        Task<UserDto> GetByEmailAsync(string email);

        Task<UserDto> GetUserByIdAsync(int id);

        Task<bool> UpdateUserAsync(int id, UpdateUserDto dto);

        Task<bool> ChangePasswordAsync(int id, string currentPassword, string newPassword);
    }
}
