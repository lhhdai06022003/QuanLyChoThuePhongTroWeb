using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeThongBaoStore : IThongBaoStore
    {
        public List<ThongBao> ThongBaos { get; } = new();
        public List<int> RoomResponsibleUserIds { get; } = new();

        public Task<List<ThongBao>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default)
            => Task.FromResult(ThongBaos.Where(t => t.NguoiDungId == nguoiDungId).ToList());

        public Task<NguoiDung?> GetActiveNguoiDungByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
            => Task.FromResult<NguoiDung?>(null);

        public Task<List<int>> GetActiveUserIdsByRoleAsync(Role role, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<int>());

        public Task<List<int>> GetRoomResponsibleUserIdsAsync(int phongTroId, CancellationToken cancellationToken = default)
            => Task.FromResult(RoomResponsibleUserIds.ToList());

        public Task<ThongBao?> GetByIdAndUserAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default)
            => Task.FromResult(ThongBaos.FirstOrDefault(t => t.Id == thongBaoId && t.NguoiDungId == nguoiDungId));

        public Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default)
            => Task.FromResult(ThongBaos.Count(t => t.NguoiDungId == nguoiDungId && !t.IsRead));

        public Task<List<ThongBao>> GetUnreadByUserAsync(int nguoiDungId, CancellationToken cancellationToken = default)
            => Task.FromResult(ThongBaos.Where(t => t.NguoiDungId == nguoiDungId && !t.IsRead).ToList());

        public Task<List<ThongBao>> GetAllByUserAsync(int nguoiDungId, CancellationToken cancellationToken = default)
            => Task.FromResult(ThongBaos.Where(t => t.NguoiDungId == nguoiDungId).ToList());

        public Task<DataTableResponse<ThongBao>> LayDanhSachPhanTrangAsync(DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default)
            => Task.FromResult(new DataTableResponse<ThongBao>());

        public void Add(ThongBao thongBao) => ThongBaos.Add(thongBao);

        public void AddRange(IEnumerable<ThongBao> thongBaos) => ThongBaos.AddRange(thongBaos);

        public void Remove(ThongBao thongBao) => ThongBaos.Remove(thongBao);

        public void RemoveRange(IEnumerable<ThongBao> thongBaos)
        {
            foreach (var t in thongBaos.ToList()) ThongBaos.Remove(t);
        }
    }
}
