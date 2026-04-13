using System;
using System.Collections.Generic;
using System.Text;

namespace LedApp.Application.DTOs
{
    public class AdminDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    // Yeni admin oluştururken kullanılır
    // Password gelir, servis katmanında hash'lenir
    public class CreateAdminDto
    {
        public string FullName { get; set; }
        public string Password { get; set; }
    }

    // Admin bilgisi güncellenirken kullanılır
    public class UpdateAdminDto
    {
        public string FullName { get; set; }
        public string Password { get; set; } // opsiyonel, null gelirse değiştirme
    }
}
