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

    // Chan: không lưu được theo luồng thường. Do: đỏ, cần ghi chú. Vang: vàng, cần ghi chú.
    public enum MeterAnomalyLevel
    {
        Chan = 1,
        Do,
        Vang
    }

    public sealed record MeterAnomalyResult(
        MeterAnomalyReason LyDo,
        string MoTa,
        MeterAnomalyLevel MucDo = MeterAnomalyLevel.Vang,
        decimal? ChiSoGoiY = null,
        decimal? SanLuongTheoGoiY = null,
        decimal? TranCongTo = null);

    // Kiểm tra chỉ số điện nước bất thường (thuần, không I/O). So sánh bằng giá trị chính xác, chỉ làm tròn khi in.
    public static class MeterAnomalyPolicy
    {
        private const decimal TranCongToToiThieu = 10_000m;
        private const decimal TyLeQuayVong = 0.9m;
        private const decimal HeSoDoTieuThu = 3m;

        // Lũy thừa của 10 nhỏ nhất lớn hơn chỉ số cũ, tối thiểu 10.000.
        public static decimal TinhTranCongTo(decimal cu)
        {
            var tran = TranCongToToiThieu;
            while (tran <= cu)
            {
                tran *= 10m;
            }

            return tran;
        }

        // null = hợp lệ. Trả lý do đầu tiên theo thứ tự: số giảm, nghi sai chữ số, tiêu thụ.
        public static MeterAnomalyResult? Evaluate(LoaiDongHo loai, decimal cu, decimal moi, MeterConsumptionHistory lichSu, AutoInvoiceOptions o)
        {
            var laDien = loai == LoaiDongHo.Dien;
            var ten = laDien ? "Điện" : "Nước";
            var dv = laDien ? "kWh" : "m³";

            var cacKy = lichSu.CacKyTronThang
                .Take(o.SoKyLichSuToiDa)
                .Select(k => laDien ? k.Dien : k.Nuoc)
                .ToList();
            var duLichSu = cacKy.Count >= o.SoKyToiThieu;
            var m = duLichSu ? TrungVi(cacKy) : 0m;
            var sanTang = laDien ? o.SanTangDienKwh : o.SanTangNuocM3;
            var tranChoPhep = TinhTranChoPhep(laDien, lichSu, duLichSu, m, sanTang, o);

            if (moi < cu)
            {
                var moTaGiam = $"{ten}: chỉ số mới ({ChiSo.DinhDang(moi)}) nhỏ hơn chỉ số cũ ({ChiSo.DinhDang(cu)})";

                var tranCongTo = TinhTranCongTo(cu);
                if (cu >= TyLeQuayVong * tranCongTo)
                {
                    var sanLuongQuayVong = (tranCongTo - cu) + moi;
                    if (sanLuongQuayVong <= tranChoPhep)
                    {
                        return new MeterAnomalyResult(MeterAnomalyReason.ChiSoGiam, moTaGiam, MeterAnomalyLevel.Chan,
                            ChiSoGoiY: null, SanLuongTheoGoiY: sanLuongQuayVong, TranCongTo: tranCongTo);
                    }
                }

                foreach (var k in new[] { 10m, 100m })
                {
                    var ungVien = Math.Round(moi * k, 3, MidpointRounding.AwayFromZero);
                    var sanLuong = ungVien - cu;
                    if (sanLuong > 0 && sanLuong <= tranChoPhep)
                    {
                        return new MeterAnomalyResult(MeterAnomalyReason.ChiSoGiam, moTaGiam, MeterAnomalyLevel.Chan,
                            ChiSoGoiY: ungVien, SanLuongTheoGoiY: sanLuong);
                    }
                }

                return new MeterAnomalyResult(MeterAnomalyReason.ChiSoGiam, moTaGiam, MeterAnomalyLevel.Chan);
            }

            var t = moi - cu;
            var dk1 = cu >= 100m && moi >= cu * 9m;
            var dk2 = t > tranChoPhep;
            if (dk1 || dk2)
            {
                var moTaSaiChuSo = $"{ten}: chỉ số mới ({ChiSo.DinhDang(moi)}) quá lớn so với chỉ số cũ ({ChiSo.DinhDang(cu)}), có thể đọc thừa một chữ số";

                foreach (var k in new[] { 10m, 100m, 1000m })
                {
                    var ungVien = Math.Round(moi / k, 3, MidpointRounding.AwayFromZero);
                    var sanLuong = ungVien - cu;
                    if (sanLuong > 0 && sanLuong <= tranChoPhep)
                    {
                        return new MeterAnomalyResult(MeterAnomalyReason.NghiSaiChuSo, moTaSaiChuSo, MeterAnomalyLevel.Do,
                            ChiSoGoiY: ungVien, SanLuongTheoGoiY: sanLuong);
                    }
                }

                if (dk1)
                {
                    return new MeterAnomalyResult(MeterAnomalyReason.NghiSaiChuSo, moTaSaiChuSo, MeterAnomalyLevel.Do);
                }
            }

            if (duLichSu)
            {
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
                var moTa = $"{ten}: tiêu thụ {ChiSo.DinhDang(t)} {dv}, trung vị {cacKy.Count} kỳ của hợp đồng {ChiSo.DinhDang(m)} {dv} " +
                    $"(cho phép {ChiSo.DinhDang(min)}–{ChiSo.DinhDang(max)} {dv})";

                if (lyDo == MeterAnomalyReason.TieuThuThapBatThuong)
                {
                    if (t == 0m)
                    {
                        moTa += ", kiểm tra đồng hồ kẹt số hoặc ảnh cũ";
                    }

                    return new MeterAnomalyResult(lyDo.Value, moTa, MeterAnomalyLevel.Vang);
                }

                var mucDo = m > 0m && t >= m * HeSoDoTieuThu ? MeterAnomalyLevel.Do : MeterAnomalyLevel.Vang;
                return new MeterAnomalyResult(lyDo.Value, moTa, mucDo);
            }

            // Chưa đủ lịch sử: so với trần chung, co theo số ngày hợp đồng phủ trong tháng.
            if (t > tranChoPhep)
            {
                return new MeterAnomalyResult(MeterAnomalyReason.TieuThuBatThuong,
                    $"{ten}: tiêu thụ {ChiSo.DinhDang(t)} {dv}, hợp đồng mới chưa đủ lịch sử, so với trần chung {ChiSo.DinhDang(Math.Round(tranChoPhep, 1, MidpointRounding.AwayFromZero))} {dv}",
                    MeterAnomalyLevel.Vang);
            }

            return null;
        }

        // Đủ lịch sử: max(M × HeSoTang, M + sàn tăng). Chưa đủ: trần chung co theo số ngày hợp đồng phủ trong tháng.
        private static decimal TinhTranChoPhep(bool laDien, MeterConsumptionHistory lichSu, bool duLichSu, decimal m, decimal sanTang, AutoInvoiceOptions o)
        {
            if (duLichSu)
            {
                return Math.Max(m * o.HeSoTang, m + sanTang);
            }

            return (laDien ? o.TranDienKwh : o.TranNuocM3) * lichSu.SoNgayPhu / lichSu.SoNgayCuaThang;
        }

        private static decimal TrungVi(IReadOnlyList<decimal> giaTri)
        {
            var sapXep = giaTri.OrderBy(x => x).ToList();
            var giua = sapXep.Count / 2;
            return sapXep.Count % 2 == 1 ? sapXep[giua] : (sapXep[giua - 1] + sapXep[giua]) / 2m;
        }
    }
}
