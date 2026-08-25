using System;
using System.Collections.Generic;
using System.Text;

namespace LedApp.Application.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CategoryName { get; set; }  // Category.Name
        public string AdminFullName { get; set; } // Admin.FullName
    }

    // Yeni ürün oluştururken kullanılır
    public class CreateProductDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int CategoryId { get; set; }
        public int AdminId { get; set; }
    }

    // Ürün güncellerken kullanılır
    public class UpdateProductDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int CategoryId { get; set; }
    }
}
