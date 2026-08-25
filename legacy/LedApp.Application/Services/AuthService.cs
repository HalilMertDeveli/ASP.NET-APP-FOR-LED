using LedApp.Application.DTOs;
using LedApp.Domain.Interfaces;
using Entity.HMD.Entity;
using System.Threading.Tasks;
using BCrypt.Net;

namespace LedApp.Application.Services
{
    public class AuthService : IAuthService
    {
        // Onion mimarisi kuralı: Application katmanı Veritabanını (DbContext) bilmez!
        // Sadece Domain'deki "IUserRepository" interface'ini bilir. 
        private readonly IUserRepository _userRepository;

        public AuthService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> CheckEmailExistsAsync(string email)
        {
            return await _userRepository.AnyAsync(x => x.Email == email);
        }

        public async Task<UserDto> GetByEmailAsync(string email)
        {
            var user = await _userRepository.FirstOrDefaultAsync(x => x.Email == email);
            if (user == null)
            {
                return null;
            }

            return new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                CreatedDate = user.CreatedDate
            };
        }

        public async Task<UserDto> LoginAsync(string email, string password)
        {
            // 1. Veritabanından o emaile ait kullanıcıyı getir:
            var user = await _userRepository.FirstOrDefaultAsync(x => x.Email == email);
            if (user == null)
            {
                return null; // Kullanıcı yok
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return null;
            }

            // 2. Şifreyi DOĞRULA.
            // Bazı eski kayıtlarda PasswordHash alanında BCrypt formatı yerine düz metin/bozuk veri olabilir.
            // Bu durumda girişi patlatmak yerine güvenli şekilde kontrol edip mümkünse hash'e migrate ediyoruz.
            bool isPasswordValid;
            bool needsHashMigration = false;

            try
            {
                if (LooksLikeBcryptHash(user.PasswordHash))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                else
                {
                    isPasswordValid = string.Equals(password, user.PasswordHash, System.StringComparison.Ordinal);
                    needsHashMigration = isPasswordValid;
                }
            }
            catch (SaltParseException)
            {
                isPasswordValid = string.Equals(password, user.PasswordHash, System.StringComparison.Ordinal);
                needsHashMigration = isPasswordValid;
            }
            
            if (!isPasswordValid) return null; // Şifre yanlış

            if (needsHashMigration)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();
            }

            // 3. Güvenliyse dışarı aktarılacak DTO nesnesi dön
            return new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                CreatedDate = user.CreatedDate
            };
        }

        public async Task<UserDto> RegisterAsync(CreateUserDto dto)
        {
            // 1. DTO'dan gelen düz şifreyi GÜVENLİ (Hashed) hale getir. (Örn: $2a$11$4I...)
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // 2. Yeni User nesnesi (Entity) oluşturuyoruz
            var newUser = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = hashedPassword // Şifre artık %100 güvende
            };

            // 3. Veritabanına Ekle (Gerçek SQL insert işlemi Repository katmanına devredildi)
            await _userRepository.AddAsync(newUser);
            await _userRepository.SaveChangesAsync();

            // 4. Controller'a Claims yaratabilmesi için geri bilgi çevir
            return new UserDto
            {
                Id = newUser.Id,
                FullName = newUser.FullName,
                Email = newUser.Email,
                Phone = newUser.Phone,
                CreatedDate = newUser.CreatedDate
            };
        }

        public async Task<UserDto> GetUserByIdAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return null;

            return new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                CreatedDate = user.CreatedDate
            };
        }

        public async Task<bool> UpdateUserAsync(int id, UpdateUserDto dto)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return false;

            user.FullName = dto.FullName;
            user.Email = dto.Email;
            user.Phone = dto.Phone;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePasswordAsync(int id, string currentPassword, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return false;

            // Şifre doğrula
            bool isPasswordValid;
            try
            {
                if (LooksLikeBcryptHash(user.PasswordHash))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash);
                }
                else
                {
                    isPasswordValid = string.Equals(currentPassword, user.PasswordHash, System.StringComparison.Ordinal);
                }
            }
            catch (BCrypt.Net.SaltParseException)
            {
                isPasswordValid = string.Equals(currentPassword, user.PasswordHash, System.StringComparison.Ordinal);
            }

            if (!isPasswordValid) return false;

            // Yeni şifreyi hashle ve kaydet
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
            return true;
        }

        private static bool LooksLikeBcryptHash(string value)
        {
            return value != null && value.StartsWith("$2", System.StringComparison.Ordinal) && value.Length >= 59;
        }
    }
}
