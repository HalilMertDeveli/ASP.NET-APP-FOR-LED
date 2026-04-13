using System;
using System.Collections.Generic;
using System.Text;

namespace Entity.HMD.Entity
{
    public class Admin
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string PasswordHash { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public List<Product> Products { get; set; } = new();
    }
}
