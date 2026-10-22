using System;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    // Nhãn tiếng Việt và chuyển snapshot sang DTO; giữ Web không phải tham chiếu Domain.
    public static class PaymentDtoMapper
    {
        public static string YeuCauLabel(TrangThaiYeuCauThanhToan trangThai) => trangThai switch
        {
            TrangThaiYeuCauThanhToan.ChoThanhToan => "Chờ thanh toán",
            TrangThaiYeuCauThanhToan.DaBaoChuyen => "Đã báo chuyển",
            TrangThaiYeuCauThanhToan.DangDoiChieu => "Đang chờ đối chiếu",
            TrangThaiYeuCauThanhToan.DaHoanTat => "Đã hoàn tất",
            TrangThaiYeuCauThanhToan.TuChoi => "Từ chối",
            TrangThaiYeuCauThanhToan.HetHan => "Hết hạn",
            TrangThaiYeuCauThanhToan.DaHuy => "Đã hủy",
            _ => "—"
        };

        public static string MinhChungLabel(TrangThaiMinhChungThanhToan trangThai) => trangThai switch
        {
            TrangThaiMinhChungThanhToan.ChoXacNhan => "Chờ xác nhận",
            TrangThaiMinhChungThanhToan.DaXacNhan => "Đã xác nhận",
            TrangThaiMinhChungThanhToan.TuChoi => "Bị từ chối",
            _ => "—"
        };

        public static PaymentRequestDto ToDto(PaymentRequestSnapshot snapshot, DateTime nowUtc)
        {
            var hieuLuc = YeuCauThanhToanHoaDon.TrangThaiHieuLuc(snapshot.TrangThai, snapshot.HanThanhToan, nowUtc);
            var latest = snapshot.LichSu.LastOrDefault();

            return new PaymentRequestDto
            {
                YeuCauId = snapshot.YeuCauId,
                HoaDonId = snapshot.HoaDonId,
                MaYeuCau = snapshot.MaYeuCau,
                SoTien = snapshot.SoTien,
                NoiDungChuyenKhoan = snapshot.NoiDungChuyenKhoan,
                HanThanhToanUtc = snapshot.HanThanhToan,
                NgayTaoUtc = snapshot.NgayTao,
                TrangThai = (AppTrangThaiYeuCauThanhToan)(int)snapshot.TrangThai,
                TrangThaiHieuLuc = (AppTrangThaiYeuCauThanhToan)(int)hieuLuc,
                TrangThaiText = YeuCauLabel(hieuLuc),
                ChoAdmin = snapshot.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu && PaymentNoteTags.IsChoAdmin(latest?.LyDo),
                MinhChungs = snapshot.MinhChungs
                    .OrderByDescending(m => m.NgayTao)
                    .Select(m => new PaymentProofDto
                    {
                        MinhChungId = m.MinhChungId,
                        NgayNopUtc = m.NgayTao,
                        NgayChuyenKhaiBaoUtc = m.NgayChuyenKhaiBao,
                        SoTienKhaiBao = m.SoTienKhaiBao,
                        MaGiaoDichNganHang = m.MaGiaoDichNganHang,
                        TrangThai = (AppTrangThaiMinhChungThanhToan)(int)m.TrangThai,
                        TrangThaiText = m.GhiNhanBiHuy ? "Đã xác nhận, sau đó bị hủy ghi nhận" : MinhChungLabel(m.TrangThai),
                        LyDoTuChoi = m.LyDoTuChoi,
                        NgayDoiChieuUtc = m.NgayDoiChieu,
                        GhiNhanBiHuy = m.GhiNhanBiHuy
                    })
                    .ToList(),
                LichSu = snapshot.LichSu
                    .Select(l => new PaymentRequestHistoryDto
                    {
                        ThoiGianUtc = l.NgayThucHien,
                        TrangThaiCu = l.TrangThaiCu.HasValue ? YeuCauLabel(l.TrangThaiCu.Value) : "—",
                        TrangThaiMoi = YeuCauLabel(l.TrangThaiMoi),
                        NguoiThucHien = string.IsNullOrEmpty(l.NguoiThucHien) ? "Hệ thống" : l.NguoiThucHien!,
                        LyDo = l.LyDo ?? string.Empty
                    })
                    .ToList()
            };
        }

        // Dùng ngay sau khi ghi, khi entity vừa lưu còn trong transaction (chưa có tên người thực hiện).
        public static PaymentRequestDto ToDto(YeuCauThanhToanHoaDon yeuCau, DateTime nowUtc)
        {
            return ToDto(new PaymentRequestSnapshot
            {
                YeuCauId = yeuCau.YeuCauThanhToanHoaDonId,
                HoaDonId = yeuCau.HoaDonId,
                MaYeuCau = yeuCau.MaYeuCau,
                SoTien = yeuCau.SoTien,
                NoiDungChuyenKhoan = yeuCau.NoiDungChuyenKhoan,
                HanThanhToan = yeuCau.HanThanhToan,
                NgayTao = yeuCau.NgayTao,
                TrangThai = yeuCau.TrangThai,
                MinhChungs = yeuCau.MinhChungThanhToanHoaDons.Select(ToSnapshot).ToList(),
                LichSu = yeuCau.LichSuTrangThaiYeuCauThanhToanHoaDons
                    .OrderBy(l => l.NgayThucHien)
                    .ThenBy(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
                    .Select(l => new PaymentHistorySnapshot
                    {
                        Id = l.LichSuTrangThaiYeuCauThanhToanHoaDonId,
                        TrangThaiCu = l.TrangThaiCu,
                        TrangThaiMoi = l.TrangThaiMoi,
                        NgayThucHien = l.NgayThucHien,
                        LyDo = l.LyDo
                    })
                    .ToList()
            }, nowUtc);
        }

        public static PaymentProofDto ToDto(MinhChungThanhToanHoaDon minhChung, DateTime nowUtc)
        {
            return ToDto(new PaymentRequestSnapshot { MinhChungs = { ToSnapshot(minhChung) } }, nowUtc).MinhChungs[0];
        }

        private static PaymentProofSnapshot ToSnapshot(MinhChungThanhToanHoaDon m) => new()
        {
            MinhChungId = m.MinhChungThanhToanHoaDonId,
            HinhAnhUrl = m.HinhAnhUrl,
            PublicId = m.PublicId,
            NgayTao = m.NgayTao,
            NgayChuyenKhaiBao = m.NgayChuyenKhaiBao,
            SoTienKhaiBao = m.SoTienKhaiBao,
            MaGiaoDichNganHang = m.MaGiaoDichNganHang,
            TrangThai = m.TrangThaiDoiChieu,
            LyDoTuChoi = m.LyDoTuChoi,
            NgayDoiChieu = m.NgayDoiChieu
        };

        // Lượt đang hoạt động theo trạng thái hiệu lực (khớp YeuCauThanhToanHoaDon.DangHoatDong).
        public static bool IsActive(PaymentRequestDto dto)
        {
            return dto.TrangThaiHieuLuc is AppTrangThaiYeuCauThanhToan.ChoThanhToan or AppTrangThaiYeuCauThanhToan.DangDoiChieu;
        }

        public static ReviewQueueRowDto ToDto(ReviewQueueRowSnapshot row) => new()
        {
            MinhChungId = row.MinhChungId,
            YeuCauId = row.YeuCauId,
            MaYeuCau = row.MaYeuCau,
            HoaDonId = row.HoaDonId,
            MaHoaDon = row.MaHoaDon,
            TenPhong = row.TenPhong,
            TenChiNhanh = row.TenChiNhanh,
            TenKhachThue = row.TenKhachThue,
            SoTienLuot = row.SoTienLuot,
            MaGiaoDichKhaiBao = row.MaGiaoDichKhaiBao,
            NgayChuyenKhaiBaoUtc = row.NgayChuyenKhaiBao,
            NgayNopUtc = row.NgayNop,
            TrangThai = (AppTrangThaiMinhChungThanhToan)(int)row.TrangThai,
            TrangThaiText = MinhChungLabel(row.TrangThai),
            ChoAdmin = row.ChoAdmin
        };
    }
}
