using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence
{
    public class EfUnitOfWork : IUnitOfWork
    {
        private const string UniqueViolationSqlState = "23505";

        private readonly ApplicationDbContext _context;

        public EfUnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState } pg)
            {
                throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
            }
        }

        public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new ApplicationTransaction(transaction);
        }
    }
}
