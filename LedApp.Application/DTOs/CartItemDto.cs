using System;
using System.Collections.Generic;
using System.Text;

namespace LedApp.Application.DTOs
{
    public class CartItemDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public string ProductName { get; set; }   // Product.Name
        public decimal ProductPrice { get; set; } // Product.Price
        public decimal TotalPrice { get; set; }   // Quantity * Product.Price
    }

    // Sepete ürün eklerken kullanılır
    public class AddToCartDto
    {
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    // Sepetteki ürün miktarı güncellenirken kullanılır
    public class UpdateCartItemDto
    {
        public int Quantity { get; set; }
    }
}
