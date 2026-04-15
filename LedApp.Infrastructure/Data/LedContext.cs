
using Entity.HMD.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Entity.HMD.Context
{
    public class LedContext : DbContext
    {
        public LedContext(DbContextOptions<LedContext> options)
         : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<PanelSupportFile> PanelSupportFiles { get; set; }
        public DbSet<UpdateHexFile> UpdateHexFiles { get; set; }
        public DbSet<UpdateHexMapping> UpdateHexMappings { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PanelSupportFile>()
                .HasIndex(x => new { x.PValue, x.ChipsetValue, x.DecoderValue, x.FileType })
                .IsUnique();

            modelBuilder.Entity<PanelSupportFile>()
                .HasIndex(x => new { x.PValue, x.ChipsetValue, x.DecoderValue });

            modelBuilder.Entity<UpdateHexFile>()
                .HasIndex(x => x.VersionLabel)
                .IsUnique();

            modelBuilder.Entity<UpdateHexMapping>()
                .HasIndex(x => x.LookupKey)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}
