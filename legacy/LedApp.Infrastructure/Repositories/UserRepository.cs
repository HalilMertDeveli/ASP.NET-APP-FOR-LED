using Entity.HMD.Context;
using Entity.HMD.Entity;
using LedApp.Domain.Interfaces;

namespace LedApp.Infrastructure.Repositories
{
    // Spesifik olarak User tablosuna bakan repository.
    // Tüm EF Core CRUD (Ekle/Sil/Bul) işlemlerini otomatik miras aldı!
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(LedContext context) : base(context)
        {
        }
    }
}
