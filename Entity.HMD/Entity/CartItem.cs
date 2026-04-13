using System;
using System.Collections.Generic;
using System.Text;

namespace Entity.HMD.Entity
{
    public class CartItem
    {
        public int Id { get; set; }

        public int Quantity { get; set; }

        // User
        public int UserId { get; set; }
        public User User { get; set; }

        // Product
        public int ProductId { get; set; }
        public Product Product { get; set; }
    }
}
