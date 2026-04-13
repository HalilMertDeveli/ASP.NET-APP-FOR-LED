using System;
using System.Collections.Generic;
using System.Text;

namespace Entity.HMD.Entity
{
    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public decimal Price { get; set; }

        public int Stock { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Admin ilişkisi
        public int AdminId { get; set; }
        public Admin Admin { get; set; }

        // Category ilişkisi
        public int CategoryId { get; set; }
        public Category Category { get; set; }

        public List<CartItem> CartItems { get; set; } = new();

        public List<Order> Orders { get; set; } = new();
    }
}
