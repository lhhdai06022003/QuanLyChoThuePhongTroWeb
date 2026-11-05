using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    public static class BaoCaoCongNoBuilder
    {
        public static string NhanNhom(NhomTuoiNo nhom) => nhom switch
        {
            NhomTuoiNo.ChuaDenHan => "Chưa đến hạn",
            NhomTuoiNo.Tu1Den30 => "1–30 ngày",
            NhomTuoiNo.Tu31Den60 => "31–60 ngày",
            NhomTuoiNo.Tu61Den90 => "61–90 ngày",
            NhomTuoiNo.Tren90 => "Trên 90 ngày",
            _ => "Không có hạn"
        };

        private sealed record Dong(BaoCaoHoaDonNoRow Row, decimal ConNo, NhomTuoiNo Nhom);

        public static BaoCaoCongNoDto Build(BaoCaoPhamViDto phamVi, IReadOnlyList<BaoCaoHoaDonNoRow> rows, DateTime nowUtc)
        {
            var dongs = rows
                .Select(r => new Dong(r, r.TongTien - r.DaThu, BaoCaoCalculator.PhanNhomTuoiNo(r.HanThanhToanUtc, nowUtc)))
                .Where(d => d.ConNo > 0m)
                .ToList();

            bool QuaHan(NhomTuoiNo n) => n is NhomTuoiNo.Tu1Den30 or NhomTuoiNo.Tu31Den60 or NhomTuoiNo.Tu61Den90 or NhomTuoiNo.Tren90;

            var nhomTuoi = Enum.GetValues<NhomTuoiNo>()
                .Select(n => new BaoCaoTuoiNoDto
                {
                    Ma = n.ToString(),
                    Nhan = NhanNhom(n),
                    SoHoaDon = dongs.Count(d => d.Nhom == n),
                    SoTien = dongs.Where(d => d.Nhom == n).Sum(d => d.ConNo)
                })
                .ToList();

            var theoKhach = dongs
                .GroupBy(d => d.Row.NguoiThueId)
                .Select(g =>
                {
                    var moiNhat = g.OrderByDescending(d => d.Row.Nam).ThenByDescending(d => d.Row.Thang).First().Row;
                    var cuNhat = g.OrderBy(d => d.Row.Nam).ThenBy(d => d.Row.Thang).First().Row;
                    var quaHan = g.Where(d => QuaHan(d.Nhom)).ToList();
                    return new BaoCaoKhachNoDto
                    {
                        NguoiThueId = g.Key,
                        HoVaTen = moiNhat.HoVaTen,
                        SoPhong = moiNhat.SoPhong,
                        ChiNhanhId = moiNhat.ChiNhanhId,
                        TenChiNhanh = moiNhat.TenChiNhanh,
                        SoHoaDon = g.Count(),
                        TongNo = g.Sum(d => d.ConNo),
                        KyNoCuNhat = new BaoCaoKy(cuNhat.Thang, cuNhat.Nam).Text,
                        SoNgayQuaHanLonNhat = quaHan.Count == 0
                            ? null
                            : quaHan.Max(d => BaoCaoCalculator.SoNgayQuaHan(d.Row.HanThanhToanUtc!.Value, nowUtc)),
                        DaTraPhong = g.All(d => d.Row.DaTraPhong)
                    };
                })
                .OrderByDescending(k => k.TongNo)
                .ThenBy(k => k.HoVaTen, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new BaoCaoCongNoDto
            {
                PhamVi = phamVi,
                TongConNo = dongs.Sum(d => d.ConNo),
                QuaHan = dongs.Where(d => QuaHan(d.Nhom)).Sum(d => d.ConNo),
                NoKhachDaTraPhong = dongs.Where(d => d.Row.DaTraPhong).Sum(d => d.ConNo),
                SoHoaDonNo = dongs.Count,
                SoHoaDonQuaHan = dongs.Count(d => QuaHan(d.Nhom)),
                SoKhachNo = theoKhach.Count,
                NhomTuoiNo = nhomTuoi,
                TheoKhach = theoKhach
            };
        }
    }
}
