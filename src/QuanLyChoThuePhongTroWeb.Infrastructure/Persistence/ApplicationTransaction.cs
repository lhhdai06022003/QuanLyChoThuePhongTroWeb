using Microsoft.EntityFrameworkCore.Storage;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence
{
    public sealed class ApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public ApplicationTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            _transaction.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await _transaction.DisposeAsync();
        }
    }
}
