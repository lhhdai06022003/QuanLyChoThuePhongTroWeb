using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public record SentNotice(string Kind, int TargetId, string TieuDe, string NoiDung, string? LinhVuc, string? Link);

    public class FakeThongBaoService : IThongBaoService
    {
        public List<SentNotice> Sent { get; } = new();
        public bool ThrowOnSend { get; set; }
        public bool FailOnSend { get; set; }
        public List<string>? Log { get; set; }

        private Task<ServiceResult> Record(string kind, int targetId, string tieuDe, string noiDung, string? linhVuc, string? link)
        {
            Log?.Add("Notify:" + kind);
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("Gửi thông báo lỗi");
            }

            Sent.Add(new SentNotice(kind, targetId, tieuDe, noiDung, linhVuc, link));
            return Task.FromResult(FailOnSend ? ServiceResult.Fail("Không có người nhận thông báo.") : ServiceResult.Ok());
        }

        public Task<ServiceResult> GuiChoKhachThueAsync(int nguoiThueId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
            => Record("Khach", nguoiThueId, tieuDe, noiDung, linhVuc, linkDieuHuong);

        public Task<ServiceResult> GuiChoNguoiPhuTrachPhongAsync(int phongTroId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
            => Record("Phong", phongTroId, tieuDe, noiDung, linhVuc, linkDieuHuong);

        public Task<ServiceResult> GuiChoNguoiDungAsync(int nguoiDungId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
            => Record("NguoiDung", nguoiDungId, tieuDe, noiDung, linhVuc, linkDieuHuong);

        public Task<ServiceResult> GuiChoQuyenAsync(AppRole role, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
            => Record("Quyen", (int)role, tieuDe, noiDung, linhVuc, linkDieuHuong);

        public Task<List<ThongBaoRes>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> DanhDauDaDocAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> DanhDauTatCaDaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DataTableResponse<ThongBaoRes>> LayDanhSachPhanTrangAsync(DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> XoaThongBaoAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult> XoaTatCaThongBaoAsync(int nguoiDungId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
