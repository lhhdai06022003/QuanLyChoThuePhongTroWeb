using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Persistence
{
    public interface IThongBaoStore
    {
        Task<List<ThongBao>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<NguoiDung?> GetActiveNguoiDungByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<List<int>> GetActiveUserIdsByRoleAsync(Role role, CancellationToken cancellationToken = default);
        Task<ThongBao?> GetByIdAsync(int thongBaoId, CancellationToken cancellationToken = default);
        Task<ThongBao?> GetByIdAndUserAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default);
        Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<List<ThongBao>> GetUnreadByUserAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<List<ThongBao>> GetAllByUserAsync(int nguoiDungId, CancellationToken cancellationToken = default);
        Task<DataTableResponse<ThongBao>> LayDanhSachPhanTrangAsync(DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default);
        void Add(ThongBao thongBao);
        void AddRange(IEnumerable<ThongBao> thongBaos);
        void Remove(ThongBao thongBao);
        void RemoveRange(IEnumerable<ThongBao> thongBaos);
    }
}
