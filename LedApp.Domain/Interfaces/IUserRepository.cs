using Entity.HMD.Entity;
using System.Threading.Tasks;

namespace LedApp.Domain.Interfaces
{
    // User tablosuna özel işlemler (Eğer Generic dışı bir şeye ihtiyaç duyulursa) buraya yazılır.
    public interface IUserRepository : IGenericRepository<User>
    {
        // Örnek: IGenericRepository'de olmayan özel bir fonksiyon gerekirse:
        // Task<User> GetUserWithOrdersAsync(int id);
    }
}
