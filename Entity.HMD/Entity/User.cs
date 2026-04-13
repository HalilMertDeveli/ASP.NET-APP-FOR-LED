using System;
using System.Collections.Generic;
using System.Text;

namespace Entity.HMD.Entity
{
    public class User
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; }

        public string Phone { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public List<CartItem> CartItems { get; set; } = new();

        public List<Order> Orders { get; set; } = new();
    }
}
