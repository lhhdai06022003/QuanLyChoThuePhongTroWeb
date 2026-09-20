using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.Persistence
{
    public interface IThanhVienHopDongStore
    {
        Task<HopDong?> GetHopDongWithPhongTroAsync(int hopDongId, CancellationToken cancellationToken = default);
        Task<List<ThanhVienHopDongRes>> GetThanhVienByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default);
        Task<int> CountActiveMembersAsync(int hopDongId, CancellationToken cancellationToken = default);
        Task<bool> ExistsNguoiThueAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<NguoiThue?> GetNguoiThueByCccdAsync(string cccd, CancellationToken cancellationToken = default);
        Task<bool> IsNguoiThueOverlappingAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<ChiTietThanhVienHopDong?> GetChiTietWithHopDongAsync(int chiTietId, CancellationToken cancellationToken = default);
        void AddNguoiThue(NguoiThue nguoiThue);
        void AddChiTiet(ChiTietThanhVienHopDong chiTiet);
    }
}
