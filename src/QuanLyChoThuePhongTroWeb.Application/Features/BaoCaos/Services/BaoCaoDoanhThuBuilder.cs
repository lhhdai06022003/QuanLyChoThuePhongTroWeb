using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    // Gom số liệu doanh thu từ các row của store. Không đọc DB nên test được trực tiếp.
    public static class BaoCaoDoanhThuBuilder
    {
        private sealed class Acc
        {
            public int SoHoaDon;
            public decimal PhatSinh;
            public decimal ThuTien;
            public decimal CanTruCoc;
            public decimal DaThu;
            public decimal ConNo;
            public decimal TienMat;
            public decimal ChuyenKhoan;
            public decimal SePay;
            public decimal VnPay;
            public decimal TienThua;
            public decimal ThuKhacGiuCho;

            public BaoCaoDoanhThuDongDto ToDto(string nhan, int? thang, int? nam, int? chiNhanhId) => new()
            {
                Nhan = nhan,
                Thang = thang,
                Nam = nam,
                ChiNhanhId = chiNhanhId,
                SoHoaDon = SoHoaDon,
                PhatSinh = PhatSinh,
                ThuTien = ThuTien,
                CanTruCoc = CanTruCoc,
                DaThu = DaThu,
                ConNo = ConNo,
                TyLeThu = BaoCaoCalculator.TyLe(DaThu, PhatSinh),
                TienMat = TienMat,
                ChuyenKhoan = ChuyenKhoan,
                SePay = SePay,
                VnPay = VnPay,
                TienThua = TienThua,
                ThuKhacGiuCho = ThuKhacGiuCho
            };
        }

        public static BaoCaoDoanhThuDto Build(
            BaoCaoPhamViDto phamVi,
            IReadOnlyList<BaoCaoKy> kys,
            bool coBangChiNhanh,
            IReadOnlyList<BaoCaoHoaDonRow> hoaDons,
            IReadOnlyList<BaoCaoThanhToanRow> thanhToans,
            decimal tienCocDangGiu,
            IReadOnlyList<BaoCaoGiuChoRow> giuChos)
        {
            var theoHoaDon = thanhToans.ToLookup(t => t.HoaDonId);
            var tong = new Acc();
            var theoKy = kys.ToDictionary(k => k.Key, _ => new Acc());
            var theoChiNhanh = new Dictionary<int, (string Ten, Acc Acc)>();

            Acc ChiNhanhAcc(int id, string ten)
            {
                if (!theoChiNhanh.TryGetValue(id, out var entry))
                {
                    entry = (ten, new Acc());
                    theoChiNhanh[id] = entry;
                }

                return entry.Acc;
            }

            foreach (var hd in hoaDons)
            {
                var one = new Acc { SoHoaDon = 1, PhatSinh = hd.TongTien };
                foreach (var t in theoHoaDon[hd.HoaDonId])
                {
                    one.DaThu += t.SoTien;
                    switch (PaymentChannels.Of(t.MaGiaoDich, t.PhuongThuc))
                    {
                        case KenhThu.CanTruCoc: one.CanTruCoc += t.SoTien; break;
                        case KenhThu.SePay: one.SePay += t.SoTien; break;
                        case KenhThu.VnPay: one.VnPay += t.SoTien; break;
                        case KenhThu.TienMat: one.TienMat += t.SoTien; break;
                        default: one.ChuyenKhoan += t.SoTien; break;
                    }

                    one.TienThua += PaymentNoteTags.Parse(t.GhiChu).TienThua ?? 0m;
                }

                // Đã thu = tổng sổ, không cắt theo TongTien: sổ không bao giờ vượt tổng tiền, tiền thừa nằm ở thẻ ghi chú.
                one.ThuTien = one.DaThu - one.CanTruCoc;
                one.ConNo = Math.Max(0m, hd.TongTien - one.DaThu);

                Cong(tong, one);
                if (theoKy.TryGetValue(new BaoCaoKy(hd.Thang, hd.Nam).Key, out var kyAcc))
                {
                    Cong(kyAcc, one);
                }

                if (coBangChiNhanh)
                {
                    Cong(ChiNhanhAcc(hd.ChiNhanhId, hd.TenChiNhanh), one);
                }
            }

            // Cọc giữ lại ghi theo ngày kết thúc giữ chỗ, độc lập với quyết định hoàn phần dư hoặc muộn.
            // Với yêu cầu cũ, giữ nguyên cách tính theo quyết định hoàn tiền đã hoàn tất.
            foreach (var g in giuChos)
            {
                // Hoàn xong phần chuyển dư trong khi còn giữ chỗ chưa phải là giữ lại cọc.
                if (g.DangGiuChoTuPhucVu)
                {
                    continue;
                }

                var daGiuLaiCoc = g.TrangThai == TrangThaiYeuCauGiuCho.KhachKhongThue;
                var ngayGhiNhan = daGiuLaiCoc ? g.NgayGiuLaiUtc
                    : g.TrangThaiQuyetDinh == TrangThaiHoanTien.DaHoanTien ? g.NgayQuyetDinhUtc : null;
                if (ngayGhiNhan is null || (!daGiuLaiCoc && !g.CoQuyetDinh))
                {
                    continue;
                }

                var khongHoan = daGiuLaiCoc ? g.CocGiuLai : g.DaNhan - g.DaApDung - g.DaHoan;
                if (khongHoan <= 0m)
                {
                    continue;
                }

                var ky = BaoCaoCalculator.KyCua(ngayGhiNhan.Value);
                if (!theoKy.TryGetValue(ky.Key, out var kyAcc))
                {
                    continue;
                }

                kyAcc.ThuKhacGiuCho += khongHoan;
                tong.ThuKhacGiuCho += khongHoan;
                if (coBangChiNhanh)
                {
                    ChiNhanhAcc(g.ChiNhanhId, g.TenChiNhanh).ThuKhacGiuCho += khongHoan;
                }
            }

            // Đang chờ quyết định: yêu cầu chưa có quyết định hoàn tiền, đã bị từ chối/hết hạn/hủy, còn tiền đã nhận chưa áp dụng.
            var dangCho = 0m;
            foreach (var g in giuChos)
            {
                if (g.CoQuyetDinh)
                {
                    continue;
                }

                if (g.TrangThai is TrangThaiYeuCauGiuCho.TuChoi or TrangThaiYeuCauGiuCho.HetHan or TrangThaiYeuCauGiuCho.DaHuy)
                {
                    var con = g.DaNhan - g.DaApDung;
                    if (con > 0m)
                    {
                        dangCho += con;
                    }
                }
            }

            return new BaoCaoDoanhThuDto
            {
                PhamVi = phamVi,
                Tong = tong.ToDto("Tổng", null, null, null),
                TheoKy = kys.Select(k => theoKy[k.Key].ToDto(k.Text, k.Thang, k.Nam, null)).ToList(),
                TheoChiNhanh = theoChiNhanh
                    .OrderBy(e => e.Value.Ten, StringComparer.CurrentCultureIgnoreCase)
                    .Select(e => e.Value.Acc.ToDto(e.Value.Ten, null, null, e.Key))
                    .ToList(),
                TienCocDangGiu = tienCocDangGiu,
                GiuChoDangChoQuyetDinh = dangCho
            };
        }

        private static void Cong(Acc dich, Acc nguon)
        {
            dich.SoHoaDon += nguon.SoHoaDon;
            dich.PhatSinh += nguon.PhatSinh;
            dich.ThuTien += nguon.ThuTien;
            dich.CanTruCoc += nguon.CanTruCoc;
            dich.DaThu += nguon.DaThu;
            dich.ConNo += nguon.ConNo;
            dich.TienMat += nguon.TienMat;
            dich.ChuyenKhoan += nguon.ChuyenKhoan;
            dich.SePay += nguon.SePay;
            dich.VnPay += nguon.VnPay;
            dich.TienThua += nguon.TienThua;
        }
    }
}
