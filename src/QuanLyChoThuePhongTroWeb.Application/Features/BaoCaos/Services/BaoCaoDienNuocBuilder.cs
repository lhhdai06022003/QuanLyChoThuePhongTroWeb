using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    public static class BaoCaoDienNuocBuilder
    {
        private const int SoPhongToiDa = 10;

        private sealed class Acc
        {
            public decimal Kwh;
            public decimal M3;
            public decimal TienDien;
            public decimal TienNuoc;
            public int SoBanGhi;
            public int SoBanGhiBoQua;
        }

        public static BaoCaoDienNuocDto Build(BaoCaoPhamViDto phamVi, IReadOnlyList<BaoCaoKy> kys, IReadOnlyList<BaoCaoDienNuocRow> rows)
        {
            var tong = new Acc();
            var theoKy = kys.ToDictionary(k => k.Key, _ => new Acc());
            var theoPhong = new Dictionary<int, (string SoPhong, string TenChiNhanh, Acc Acc)>();

            foreach (var r in rows)
            {
                if (!theoKy.TryGetValue(new BaoCaoKy(r.Thang, r.Nam).Key, out var ky))
                {
                    continue;
                }

                // Chỉ số mới nhỏ hơn chỉ số cũ (đổi đồng hồ, quay vòng chưa xử lý): bỏ cả bản ghi để không làm sai tổng.
                if (r.DienMoi < r.DienCu || r.NuocMoi < r.NuocCu)
                {
                    ky.SoBanGhiBoQua++;
                    tong.SoBanGhiBoQua++;
                    continue;
                }

                var kwh = r.DienMoi - r.DienCu;
                var m3 = r.NuocMoi - r.NuocCu;
                var tienDien = kwh * r.DonGiaDien;
                var tienNuoc = m3 * r.DonGiaNuoc;

                if (!theoPhong.TryGetValue(r.PhongTroId, out var phong))
                {
                    phong = (r.SoPhong, r.TenChiNhanh, new Acc());
                    theoPhong[r.PhongTroId] = phong;
                }

                foreach (var acc in new[] { ky, tong, phong.Acc })
                {
                    acc.Kwh += kwh;
                    acc.M3 += m3;
                    acc.TienDien += tienDien;
                    acc.TienNuoc += tienNuoc;
                    acc.SoBanGhi++;
                }
            }

            return new BaoCaoDienNuocDto
            {
                PhamVi = phamVi,
                TongKwh = tong.Kwh,
                TongM3 = tong.M3,
                TienDien = tong.TienDien,
                TienNuoc = tong.TienNuoc,
                TrungBinhKwhPhongThang = tong.SoBanGhi == 0 ? null : Math.Round(tong.Kwh / tong.SoBanGhi, 1),
                SoBanGhi = tong.SoBanGhi,
                SoBanGhiBoQua = tong.SoBanGhiBoQua,
                TheoKy = kys.Select(k =>
                {
                    var a = theoKy[k.Key];
                    return new BaoCaoDienNuocDongDto
                    {
                        Nhan = k.Text,
                        Thang = k.Thang,
                        Nam = k.Nam,
                        Kwh = a.Kwh,
                        M3 = a.M3,
                        TienDien = a.TienDien,
                        TienNuoc = a.TienNuoc,
                        SoBanGhi = a.SoBanGhi,
                        SoBanGhiBoQua = a.SoBanGhiBoQua
                    };
                }).ToList(),
                TopPhong = theoPhong
                    .OrderByDescending(e => e.Value.Acc.Kwh)
                    .ThenBy(e => e.Value.SoPhong, StringComparer.CurrentCultureIgnoreCase)
                    .Take(SoPhongToiDa)
                    .Select(e => new BaoCaoPhongDienNuocDto
                    {
                        PhongTroId = e.Key,
                        SoPhong = e.Value.SoPhong,
                        TenChiNhanh = e.Value.TenChiNhanh,
                        Kwh = e.Value.Acc.Kwh,
                        M3 = e.Value.Acc.M3,
                        TienDien = e.Value.Acc.TienDien,
                        TienNuoc = e.Value.Acc.TienNuoc,
                        SoKy = e.Value.Acc.SoBanGhi
                    })
                    .ToList()
            };
        }
    }
}
