using System;
using System.Collections.Generic;
using System.Text;

namespace LedApp.Application.DTOs
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // Yeni kategori oluştururken kullanılır
    public class CreateCategoryDto
    {
        public string Name { get; set; }
    }

    // Kategori güncellerken kullanılır
    public class UpdateCategoryDto
    {
        public string Name { get; set; }
    }
}
