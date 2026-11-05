using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    public static class BaoCaoCongSuatBuilder
    {
        private const int SoPhongTrongToiDa = 5;

        private sealed class Acc
        {
            public int CoTheThue;
            public int DaThue;

            public BaoCaoCongSuatDongDto ToDto(string nhan, int? thang, int? nam, int? chiNhanhId) => new()
            {
                Nhan = nhan,
                Thang = thang,
                Nam = nam,
                ChiNhanhId = chiNhanhId,
                NgayPhongCoTheThue = CoTheThue,
                NgayPhongDaThue = DaThue,
                TyLeLapDay = BaoCaoCalculator.TyLe(DaThue, CoTheThue)
            };
        }

        public static BaoCaoCongSuatDto Build(
            BaoCaoPhamViDto phamVi,
            IReadOnlyList<BaoCaoKy> kys,
            bool coBangChiNhanh,
            IReadOnlyList<BaoCaoPhongRow> phongs,
            IReadOnlyList<BaoCaoHopDongRow> hopDongs,
            DateTime nowUtc)
        {
            var homNay = BaoCaoCalculator.NgayVn(nowUtc);

            // Khoảng thuê của mỗi phòng: trọn ngày đầu và ngày cuối theo giờ VN, cắt ở hôm nay, hợp nhất chồng/liền nhau.
            var khoangTheoPhong = hopDongs
                .GroupBy(h => h.PhongTroId)
                .ToDictionary(
                    g => g.Key,
                    g => KhoangNgayThue.HopNhat(g.Select(h =>
                    {
                        var tu = BaoCaoCalculator.NgayVn(h.BatDauUtc);
                        var den = h.KetThucUtc.HasValue ? BaoCaoCalculator.NgayVn(h.KetThucUtc.Value) : homNay;
                        return (Tu: tu, Den: den < homNay ? den : homNay);
                    })));
            var rong = new List<(DateOnly Tu, DateOnly Den)>();

            var tong = new Acc();
            var theoKy = kys.ToDictionary(k => k.Key, _ => new Acc());
            var theoChiNhanh = new Dictionary<int, (string Ten, Acc Acc)>();

            foreach (var phong in phongs)
            {
                var khoang = khoangTheoPhong.TryGetValue(phong.PhongTroId, out var kh) ? kh : rong;
                var ngayTao = BaoCaoCalculator.NgayVn(phong.NgayTaoUtc);
                foreach (var ky in kys)
                {
                    var cuaSo = BaoCaoCalculator.CuaSoChoThue(ngayTao, ky, homNay);
                    if (cuaSo == null)
                    {
                        continue;
                    }

                    var coTheThue = cuaSo.Value.Den.DayNumber - cuaSo.Value.Tu.DayNumber + 1;
                    var daThue = BaoCaoCalculator.DemNgayGiao(khoang, cuaSo.Value.Tu, cuaSo.Value.Den);

                    theoKy[ky.Key].CoTheThue += coTheThue;
                    theoKy[ky.Key].DaThue += daThue;
                    tong.CoTheThue += coTheThue;
                    tong.DaThue += daThue;

                    if (coBangChiNhanh)
                    {
                        if (!theoChiNhanh.TryGetValue(phong.ChiNhanhId, out var entry))
                        {
                            entry = (phong.TenChiNhanh, new Acc());
                            theoChiNhanh[phong.ChiNhanhId] = entry;
                        }

                        entry.Acc.CoTheThue += coTheThue;
                        entry.Acc.DaThue += daThue;
                    }
                }
            }

            // Mốc bắt đầu trống: ngày kết thúc hợp đồng gần nhất đã qua, không có thì ngày tạo phòng.
            var ketThucGanNhat = hopDongs
                .Where(h => h.KetThucUtc.HasValue && h.KetThucUtc.Value <= nowUtc)
                .GroupBy(h => h.PhongTroId)
                .ToDictionary(g => g.Key, g => g.Max(h => BaoCaoCalculator.NgayVn(h.KetThucUtc!.Value)));

            var phongTrong = phongs
                .Where(p => p.TrangThai == TrangThaiPhong.Trong)
                .Select(p =>
                {
                    var moc = ketThucGanNhat.TryGetValue(p.PhongTroId, out var kt) ? kt : BaoCaoCalculator.NgayVn(p.NgayTaoUtc);
                    return new BaoCaoPhongTrongDto
                    {
                        PhongTroId = p.PhongTroId,
                        SoPhong = p.SoPhong,
                        TenChiNhanh = p.TenChiNhanh,
                        SoNgayTrong = Math.Max(0, homNay.DayNumber - moc.DayNumber),
                        TuNgayText = moc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    };
                })
                .OrderByDescending(p => p.SoNgayTrong)
                .ThenBy(p => p.SoPhong, StringComparer.CurrentCultureIgnoreCase)
                .Take(SoPhongTrongToiDa)
                .ToList();

            var dangThue = phongs.Count(p => p.TrangThai == TrangThaiPhong.DaThue);
            var trong = phongs.Count(p => p.TrangThai == TrangThaiPhong.Trong);
            var baoTri = phongs.Count(p => p.TrangThai == TrangThaiPhong.BaoTri);

            return new BaoCaoCongSuatDto
            {
                PhamVi = phamVi,
                TongPhong = dangThue + trong + baoTri,
                DangThue = dangThue,
                Trong = trong,
                BaoTri = baoTri,
                Tong = tong.ToDto("Tổng", null, null, null),
                TheoKy = kys.Select(k => theoKy[k.Key].ToDto(k.Text, k.Thang, k.Nam, null)).ToList(),
                TheoChiNhanh = theoChiNhanh
                    .OrderBy(e => e.Value.Ten, StringComparer.CurrentCultureIgnoreCase)
                    .Select(e => e.Value.Acc.ToDto(e.Value.Ten, null, null, e.Key))
                    .ToList(),
                PhongTrongLauNhat = phongTrong
            };
        }
    }
}
