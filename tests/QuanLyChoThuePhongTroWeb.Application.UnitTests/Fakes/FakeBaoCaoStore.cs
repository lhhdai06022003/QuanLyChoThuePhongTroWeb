using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeBaoCaoStore : IBaoCaoStore
    {
        public Dictionary<int, string> TenChiNhanh { get; } = new();
        public int CallCount { get; private set; }
        public IReadOnlyCollection<int>? LastAllowedBranchIds { get; private set; }
        public int? LastChiNhanhId { get; private set; }
        public int LastTuKyKey { get; private set; }
        public int LastDenKyKey { get; private set; }

        public List<BaoCaoHoaDonRow> HoaDons { get; } = new();
        public List<BaoCaoThanhToanRow> ThanhToans { get; } = new();
        public decimal TienCoc { get; set; }
        public List<BaoCaoGiuChoRow> GiuChos { get; } = new();
        public List<BaoCaoHoaDonNoRow> HoaDonNos { get; } = new();
        public List<BaoCaoPhongRow> Phongs { get; } = new();
        public List<BaoCaoHopDongRow> HopDongs { get; } = new();
        public List<BaoCaoDienNuocRow> DienNuocs { get; } = new();

        private void Ghi(int? chiNhanhId, IReadOnlyCollection<int>? allowed, int tu = 0, int den = 0)
        {
            CallCount++;
            LastChiNhanhId = chiNhanhId;
            LastAllowedBranchIds = allowed;
            if (tu != 0)
            {
                LastTuKyKey = tu;
                LastDenKyKey = den;
            }
        }

        public Task<string?> GetTenChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TenChiNhanh.TryGetValue(chiNhanhId, out var ten) ? ten : null);
        }

        public Task<IReadOnlyList<BaoCaoHoaDonRow>> GetHoaDonDaGuiAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds, tuKyKey, denKyKey);
            return Task.FromResult<IReadOnlyList<BaoCaoHoaDonRow>>(HoaDons);
        }

        public Task<IReadOnlyList<BaoCaoThanhToanRow>> GetThanhToanCuaHoaDonDaGuiAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds, tuKyKey, denKyKey);
            return Task.FromResult<IReadOnlyList<BaoCaoThanhToanRow>>(ThanhToans);
        }

        public Task<decimal> GetTienCocDangGiuAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds);
            return Task.FromResult(TienCoc);
        }

        public Task<IReadOnlyList<BaoCaoGiuChoRow>> GetGiuChoDaNhanTienAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<BaoCaoGiuChoRow>>(GiuChos);
        }

        public Task<IReadOnlyList<BaoCaoHoaDonNoRow>> GetHoaDonConNoAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<BaoCaoHoaDonNoRow>>(HoaDonNos);
        }

        public Task<IReadOnlyList<BaoCaoPhongRow>> GetPhongAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<BaoCaoPhongRow>>(Phongs);
        }

        public Task<IReadOnlyList<BaoCaoHopDongRow>> GetHopDongAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<BaoCaoHopDongRow>>(HopDongs);
        }

        public Task<IReadOnlyList<BaoCaoDienNuocRow>> GetDienNuocDaDuyetAsync(int? chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds, int tuKyKey, int denKyKey, CancellationToken cancellationToken = default)
        {
            Ghi(chiNhanhId, allowedBranchIds, tuKyKey, denKyKey);
            return Task.FromResult<IReadOnlyList<BaoCaoDienNuocRow>>(DienNuocs);
        }
    }
}
