using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices
{
    public sealed record AutoInvoiceMeterDecision(
        LoaiDongHo LoaiDongHo,
        AutoInvoiceReason? LyDo,
        string? MoTa,
        decimal? ChiSoCu,
        decimal? ChiSoMoi,
        int? AnhTuXacNhanId,
        double? DoTinCay);

    public sealed record AutoInvoiceRoomDecision(
        bool DuDieuKien,
        IReadOnlyList<AutoInvoiceReason> LyDos,
        IReadOnlyList<string> MoTaLyDos,
        AutoInvoiceMeterDecision? Dien,
        AutoInvoiceMeterDecision? Nuoc,
        AutoInvoiceContractRef? HopDong);

    // Quyết định thuần (không I/O) có tự tạo hóa đơn tạm tính hay không.
    public static class AutoInvoiceReadingPolicy
    {
        public static bool IsSilent(AutoInvoiceReason reason) =>
            reason is AutoInvoiceReason.ThieuChiSo or AutoInvoiceReason.DaCoHoaDon or AutoInvoiceReason.KhongCoHopDong;

        // Lý do gửi cho khách thuê (người dùng quyết định ngày 10/10/2026: gồm cả số giảm).
        public static bool CanBaoKhach(AutoInvoiceReason reason) =>
            reason is AutoInvoiceReason.DoTinCayThap or AutoInvoiceReason.KhongDocDuoc or AutoInvoiceReason.ChiSoGiam;

        public static string GhiChuTuXacNhan(double doTinCay) =>
            $"Tự xác nhận, độ tin cậy {DoTinCay(doTinCay)}";

        public static AutoInvoiceMeterDecision EvaluateMeter(LoaiDongHo loai, AutoInvoiceRoomContext ctx, AutoInvoiceOptions o)
        {
            var ten = loai == LoaiDongHo.Dien ? "Điện" : "Nước";
            var donVi = loai == LoaiDongHo.Dien ? "kWh" : "m³";
            var cu = loai == LoaiDongHo.Dien ? ctx.ChiSoDienCu : ctx.ChiSoNuocCu;
            var tran = loai == LoaiDongHo.Dien ? o.TranDienKwh : o.TranNuocM3;

            decimal moi;
            int? anhTuXacNhanId = null;
            double? doTinCay = null;

            if (ctx.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet)
            {
                moi = loai == LoaiDongHo.Dien ? ctx.ChiSoDienMoi : ctx.ChiSoNuocMoi;
            }
            else
            {
                var cungLoai = ctx.Images.Where(i => i.LoaiDongHo == loai).ToList();
                var chinhThuc = cungLoai.FirstOrDefault(i => i.DuocChonLamChiSoChinhThuc && i.GiaTriXacNhan.HasValue);
                if (chinhThuc != null)
                {
                    moi = chinhThuc.GiaTriXacNhan!.Value;
                }
                else
                {
                    var ungVien = cungLoai
                        .Where(i => i.TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DaThayThe && !i.DuocChonLamChiSoChinhThuc)
                        .OrderByDescending(i => i.NgayGui)
                        .ThenByDescending(i => i.AnhChiSoDongHoId)
                        .FirstOrDefault();

                    if (ungVien == null)
                    {
                        return new AutoInvoiceMeterDecision(loai, AutoInvoiceReason.ThieuChiSo, $"{ten}: chưa có ảnh chỉ số", cu, null, null, null);
                    }

                    switch (ungVien.TrangThaiXuLy)
                    {
                        case TrangThaiXuLyAnhChiSo.KhongDocDuoc:
                        case TrangThaiXuLyAnhChiSo.Loi:
                        case TrangThaiXuLyAnhChiSo.CanChupLai:
                            return new AutoInvoiceMeterDecision(loai, AutoInvoiceReason.KhongDocDuoc, $"{ten}: ảnh không đọc được, cần chụp lại", cu, null, null, ungVien.DoTinCay);
                        case TrangThaiXuLyAnhChiSo.DocDuoc:
                            break;
                        default:
                            return new AutoInvoiceMeterDecision(loai, AutoInvoiceReason.ThieuChiSo, $"{ten}: ảnh chưa xử lý xong", cu, null, null, ungVien.DoTinCay);
                    }

                    if (ungVien.DoTinCay == null || ungVien.DoTinCay.Value < o.NguongDoTinCay)
                    {
                        var thuc = ungVien.DoTinCay.HasValue ? DoTinCay(ungVien.DoTinCay.Value) : "không có";
                        return new AutoInvoiceMeterDecision(loai, AutoInvoiceReason.DoTinCayThap,
                            $"{ten}: độ tin cậy nhận diện thấp ({thuc} < {DoTinCay(o.NguongDoTinCay)})", cu, null, null, ungVien.DoTinCay);
                    }

                    if (ungVien.GiaTriAIGoiY == null)
                    {
                        return new AutoInvoiceMeterDecision(loai, AutoInvoiceReason.KhongDocDuoc, $"{ten}: ảnh không đọc được, cần chụp lại", cu, null, null, ungVien.DoTinCay);
                    }

                    moi = ungVien.GiaTriAIGoiY.Value;
                    anhTuXacNhanId = ungVien.AnhChiSoDongHoId;
                    doTinCay = ungVien.DoTinCay;
                }
            }

            if (moi < cu)
            {
                return new AutoInvoiceMeterDecision(loai, AutoInvoiceReason.ChiSoGiam,
                    $"{ten}: chỉ số mới ({ChiSo.DinhDang(moi)}) nhỏ hơn chỉ số cũ ({ChiSo.DinhDang(cu)})", cu, moi, anhTuXacNhanId, doTinCay);
            }

            var tieuThu = moi - cu;
            var cacKy = ctx.TieuThuCacKyTruoc
                .Take(o.SoKyTrungBinh)
                .Select(k => loai == LoaiDongHo.Dien ? k.Dien : k.Nuoc)
                .ToList();
            var trungBinh = cacKy.Count == 0 ? 0m : cacKy.Average();

            decimal gioiHan;
            if (cacKy.Count == 0 || trungBinh == 0m)
            {
                gioiHan = tran;
            }
            else
            {
                gioiHan = o.HeSoBatThuong * trungBinh;
            }

            if (tieuThu > gioiHan)
            {
                return new AutoInvoiceMeterDecision(loai, AutoInvoiceReason.TieuThuBatThuong,
                    $"{ten}: tiêu thụ bất thường ({ChiSo.DinhDang(tieuThu)} {donVi}, giới hạn {ChiSo.DinhDang(gioiHan)} {donVi})", cu, moi, anhTuXacNhanId, doTinCay);
            }

            return new AutoInvoiceMeterDecision(loai, null, null, cu, moi, anhTuXacNhanId, doTinCay);
        }

        public static AutoInvoiceRoomDecision EvaluateRoom(AutoInvoiceRoomContext ctx, AutoInvoiceOptions o)
        {
            if (ctx.DichVuDienNuocCuaPhongId == null)
            {
                return Stop(AutoInvoiceReason.ThieuChiSo, "Chưa có kỳ chỉ số điện nước");
            }

            if (ctx.CoHoaDonHoatDong)
            {
                return Stop(AutoInvoiceReason.DaCoHoaDon, "Đã có hóa đơn trong kỳ");
            }

            if (ctx.HopDongHieuLuc.Count == 0)
            {
                return Stop(AutoInvoiceReason.KhongCoHopDong, "Không có hợp đồng hiệu lực trong kỳ");
            }

            if (ctx.HopDongHieuLuc.Count > 1)
            {
                return Stop(AutoInvoiceReason.NhieuHopDongTrongKy, "Phòng có nhiều hợp đồng trong kỳ, lập hóa đơn theo luồng tay");
            }

            var dien = EvaluateMeter(LoaiDongHo.Dien, ctx, o);
            var nuoc = EvaluateMeter(LoaiDongHo.Nuoc, ctx, o);
            var meters = new[] { dien, nuoc };

            var canBao = meters.Where(m => m.LyDo.HasValue && m.LyDo.Value != AutoInvoiceReason.ThieuChiSo).ToList();
            if (canBao.Count > 0)
            {
                return new AutoInvoiceRoomDecision(false,
                    canBao.Select(m => m.LyDo!.Value).ToList(),
                    canBao.Select(m => m.MoTa ?? string.Empty).ToList(),
                    dien, nuoc, null);
            }

            if (meters.Any(m => m.LyDo == AutoInvoiceReason.ThieuChiSo))
            {
                return new AutoInvoiceRoomDecision(false, new[] { AutoInvoiceReason.ThieuChiSo },
                    meters.Where(m => m.LyDo == AutoInvoiceReason.ThieuChiSo).Select(m => m.MoTa ?? string.Empty).ToList(),
                    dien, nuoc, null);
            }

            var thieuDien = dien.ChiSoMoi.GetValueOrDefault() - dien.ChiSoCu.GetValueOrDefault() > 0m && ctx.DonGiaDien <= 0m;
            var thieuNuoc = nuoc.ChiSoMoi.GetValueOrDefault() - nuoc.ChiSoCu.GetValueOrDefault() > 0m && ctx.DonGiaNuoc <= 0m;
            if (thieuDien || thieuNuoc)
            {
                var loai = thieuDien && thieuNuoc ? "điện và nước" : thieuDien ? "điện" : "nước";
                return new AutoInvoiceRoomDecision(false, new[] { AutoInvoiceReason.ThieuDonGia },
                    new[] { $"Chi nhánh {ctx.TenChiNhanh} chưa có đơn giá {loai} cho kỳ {ctx.Thang:00}/{ctx.Nam}" },
                    dien, nuoc, null);
            }

            if (ctx.KyTruocChuaChot)
            {
                var (thangTruoc, namTruoc) = ctx.Thang <= 1 ? (12, ctx.Nam - 1) : (ctx.Thang - 1, ctx.Nam);
                return new AutoInvoiceRoomDecision(false, new[] { AutoInvoiceReason.KyTruocChuaChot },
                    new[] { $"Kỳ {thangTruoc:00}/{namTruoc} trước chưa được chốt" }, dien, nuoc, null);
            }

            if (ctx.NguoiDungKhachId == null && ctx.EmailKhach == null)
            {
                return new AutoInvoiceRoomDecision(false, new[] { AutoInvoiceReason.KhachKhongCoKenhNhan },
                    new[] { "Khách thuê đại diện không có tài khoản cổng và không có email" }, dien, nuoc, null);
            }

            return new AutoInvoiceRoomDecision(true, Array.Empty<AutoInvoiceReason>(), Array.Empty<string>(), dien, nuoc, ctx.HopDongHieuLuc[0]);
        }

        // Độ tin cậy OCR kiểu Việt Nam ("0,93"), không phụ thuộc culture như ChiSo.DinhDang.
        private static string DoTinCay(double giaTri) => giaTri.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

        private static AutoInvoiceRoomDecision Stop(AutoInvoiceReason reason, string moTa) =>
            new(false, new[] { reason }, new[] { moTa }, null, null, null);
    }
}
