using System;
using System.Collections.Generic;
using System.Text;

namespace LedApp.Application.DTOs
{
    public class OrderDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public string UserFullName { get; set; }  // User.FullName
        public string ProductName { get; set; }   // Product.Name
    }

    // Yeni sipariş oluştururken kullanılır
    // TotalPrice servis katmanında hesaplanır: Product.Price * Quantity
    public class CreateOrderDto
    {
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    // Sadece sipariş durumu güncellenebilir
    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } // "Pending", "Shipped", "Delivered"
    }
}
