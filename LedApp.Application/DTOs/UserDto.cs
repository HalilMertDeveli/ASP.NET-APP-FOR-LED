using System;
using System.Collections.Generic;
using System.Text;

namespace LedApp.Application.DTOs
{
    // Kullanıcı bilgilerini dışarı verirken kullanılır
    public class UserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    // Kayıt olurken kullanılır
    public class CreateUserDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Phone { get; set; }
    }

    // Profil güncellerken kullanılır
    public class UpdateUserDto
    {
        public string FullName { get; set; }
        public string Phone { get; set; }
    }

    // Login için kullanılır
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
