using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases
{
    public class ContractAutoCloseUseCase : IContractAutoCloseUseCase
    {
        private readonly IHopDongStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ContractAutoCloseUseCase> _logger;

        public ContractAutoCloseUseCase(
            IHopDongStore store,
            IUnitOfWork unitOfWork,
            ILogger<ContractAutoCloseUseCase> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var expiredContracts = await _store.GetExpiredActiveContractsAsync(DateTime.UtcNow, cancellationToken);

            if (expiredContracts.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("Tìm thấy {Count} hợp đồng đã quá hạn cần đóng tự động.", expiredContracts.Count);

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var phongTroIds = expiredContracts.Select(hd => hd.PhongTroId).ToList();
                var activeServices = await _store.GetActiveServicesByRoomIdsAsync(phongTroIds, cancellationToken);

                foreach (var contract in expiredContracts)
                {
                    contract.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;
                    contract.NgayCapNhat = DateTime.UtcNow;
                    _store.Update(contract);

                    if (contract.PhongTro != null)
                    {
                        contract.PhongTro.TrangThai = TrangThaiPhong.Trong;
                        contract.PhongTro.NgayCapNhat = DateTime.UtcNow;
                        _store.UpdatePhongTro(contract.PhongTro);
                    }

                    if (contract.ChiTietThanhVienHopDongs != null)
                    {
                        var activeMembers = contract.ChiTietThanhVienHopDongs
                            .Where(x => x.NgayChuyenDi == null && !x.IsDeleted)
                            .ToList();

                        foreach (var member in activeMembers)
                        {
                            member.NgayChuyenDi = DateTime.UtcNow;
                        }
                    }

                    var servicesToClose = activeServices.Where(x => x.PhongTroId == contract.PhongTroId).ToList();
                    foreach (var s in servicesToClose)
                    {
                        s.NgayKetThuc = DateTime.UtcNow;
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Đóng tự động {Count} hợp đồng hết hạn thành công.", expiredContracts.Count);
                return expiredContracts.Count;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình thực hiện đóng hợp đồng trong Transaction.");
                throw;
            }
        }
    }
}
