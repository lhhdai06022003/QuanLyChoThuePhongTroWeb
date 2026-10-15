using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public enum MeterAnomalyReason
    {
        ChiSoGiam = 1,
        NghiSaiChuSo,
        TieuThuBatThuong,
        TieuThuThapBatThuong
    }

    public sealed record MeterAnomalyResult(MeterAnomalyReason LyDo, string MoTa);

    // Kiểm tra chỉ số điện nước bất thường (thuần, không I/O). So sánh bằng giá trị chính xác, chỉ làm tròn khi in.
    public static class MeterAnomalyPolicy
    {
        // null = hợp lệ. Trả lý do đầu tiên theo thứ tự: số giảm, nghi sai chữ số, tiêu thụ.
        public static MeterAnomalyResult? Evaluate(LoaiDongHo loai, decimal cu, decimal moi, MeterConsumptionHistory lichSu, AutoInvoiceOptions o)
        {
            var laDien = loai == LoaiDongHo.Dien;
            var ten = laDien ? "Điện" : "Nước";
            var dv = laDien ? "kWh" : "m³";

            if (moi < cu)
            {
                return new MeterAnomalyResult(MeterAnomalyReason.ChiSoGiam,
                    $"{ten}: chỉ số mới ({ChiSo.DinhDang(moi)}) nhỏ hơn chỉ số cũ ({ChiSo.DinhDang(cu)})");
            }

            if (cu >= 100m && moi >= cu * 9m)
            {
                return new MeterAnomalyResult(MeterAnomalyReason.NghiSaiChuSo,
                    $"{ten}: chỉ số mới ({ChiSo.DinhDang(moi)}) quá lớn so với chỉ số cũ ({ChiSo.DinhDang(cu)}), có thể đọc thừa một chữ số");
            }

            var t = moi - cu;
            var cacKy = lichSu.CacKyTronThang
                .Take(o.SoKyLichSuToiDa)
                .Select(k => laDien ? k.Dien : k.Nuoc)
                .ToList();

            if (cacKy.Count >= o.SoKyToiThieu)
            {
                var m = TrungVi(cacKy);
                var sanTang = laDien ? o.SanTangDienKwh : o.SanTangNuocM3;
                var nguongXetThap = laDien ? o.NguongXetThapDienKwh : o.NguongXetThapNuocM3;

                MeterAnomalyReason? lyDo = null;
                if (t > m * o.HeSoTang && t - m > sanTang)
                {
                    lyDo = MeterAnomalyReason.TieuThuBatThuong;
                }
                else if (m > nguongXetThap && t < m * o.HeSoThap)
                {
                    lyDo = MeterAnomalyReason.TieuThuThapBatThuong;
                }

                if (lyDo == null)
                {
                    return null;
                }

                var max = Math.Max(m * o.HeSoTang, m + sanTang);
                var min = m > nguongXetThap ? m * o.HeSoThap : 0m;
                return new MeterAnomalyResult(lyDo.Value,
                    $"{ten}: tiêu thụ {ChiSo.DinhDang(t)} {dv}, trung vị {cacKy.Count} kỳ của hợp đồng {ChiSo.DinhDang(m)} {dv} " +
                    $"(cho phép {ChiSo.DinhDang(min)}–{ChiSo.DinhDang(max)} {dv})");
            }

            // Chưa đủ lịch sử: so với trần chung, co theo số ngày hợp đồng phủ trong tháng.
            var tran = (laDien ? o.TranDienKwh : o.TranNuocM3) * lichSu.SoNgayPhu / lichSu.SoNgayCuaThang;
            if (t > tran)
            {
                return new MeterAnomalyResult(MeterAnomalyReason.TieuThuBatThuong,
                    $"{ten}: tiêu thụ {ChiSo.DinhDang(t)} {dv}, hợp đồng mới chưa đủ lịch sử, so với trần chung {ChiSo.DinhDang(Math.Round(tran, 1, MidpointRounding.AwayFromZero))} {dv}");
            }

            return null;
        }

        private static decimal TrungVi(IReadOnlyList<decimal> giaTri)
        {
            var sapXep = giaTri.OrderBy(x => x).ToList();
            var giua = sapXep.Count / 2;
            return sapXep.Count % 2 == 1 ? sapXep[giua] : (sapXep[giua - 1] + sapXep[giua]) / 2m;
        }
    }
}
