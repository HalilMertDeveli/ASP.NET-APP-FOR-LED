using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace LedApp.Domain.Interfaces
{
    // Temel (Çekirdek) Kurallarımız.
    // Veritabanının ne olduğuyla ilgilenmez, sadece Add, Get gibi operasyonların VAR OLMASI GEREKTİĞİNİ söyler.
    public interface IGenericRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        
        // Şarta göre veri getirme (Örn: Email == x)
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression);
        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> expression);
        Task<bool> AnyAsync(Expression<Func<T, bool>> expression);

        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task SaveChangesAsync(); // Ya da UnitOfWork paterni kullanılabilir. Basit olması için buraya koyduk.
    }
}
