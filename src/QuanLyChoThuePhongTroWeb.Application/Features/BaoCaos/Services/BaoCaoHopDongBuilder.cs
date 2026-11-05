using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    public static class BaoCaoHopDongBuilder
    {
        private const int CuaSoSapHetHanNgay = 31;

        private sealed class Acc
        {
            public int Moi;
            public int GiaHan;
            public int TraPhong;
            public int HetHan;
            public int HetHanDaKetThuc;
            public int HetHanCoGiaHan;

            public BaoCaoHopDongDongDto ToDto(string nhan, int? thang, int? nam) => new()
            {
                Nhan = nhan,
                Thang = thang,
                Nam = nam,
                HopDongMoi = Moi,
                GiaHan = GiaHan,
                TraPhong = TraPhong,
                HetHanTrongKy = HetHan,
                HetHanDaKetThuc = HetHanDaKetThuc,
                HetHanCoGiaHan = HetHanCoGiaHan,
                TyLeGiaHan = BaoCaoCalculator.TyLe(HetHanCoGiaHan, HetHanDaKetThuc)
            };
        }

        public static BaoCaoHopDongDto Build(
            BaoCaoPhamViDto phamVi,
            IReadOnlyList<BaoCaoKy> kys,
            IReadOnlyList<BaoCaoHopDongRow> hopDongs,
            DateTime nowUtc)
        {
            // Mã kế tiếp trong chuỗi gia hạn của cùng phòng (chép cách tìm của màn danh sách hợp đồng).
            var maTheoChuoi = hopDongs
                .GroupBy(h => (h.PhongTroId, Goc: HopDongCodeChain.RootOf(h.MaHopDong)))
                .ToDictionary(g => g.Key, g => g.Select(h => h.MaHopDong).ToList());

            var tong = new Acc();
            var theoKy = kys.ToDictionary(k => k.Key, _ => new Acc());

            foreach (var h in hopDongs)
            {
                var kyBatDau = BaoCaoCalculator.KyCua(h.BatDauUtc);
                if (theoKy.TryGetValue(kyBatDau.Key, out var accBatDau))
                {
                    var laGiaHan = HopDongCodeChain.IsRenewal(h.MaHopDong);
                    foreach (var acc in new[] { accBatDau, tong })
                    {
                        if (laGiaHan) acc.GiaHan++; else acc.Moi++;
                    }
                }

                if (h.KetThucUtc == null)
                {
                    continue;
                }

                var kyKetThuc = BaoCaoCalculator.KyCua(h.KetThucUtc.Value);
                if (!theoKy.TryGetValue(kyKetThuc.Key, out var accKetThuc))
                {
                    continue;
                }

                var maKeTiep = HopDongCodeChain.TimMaKeTiep(
                    h.MaHopDong,
                    maTheoChuoi[(h.PhongTroId, HopDongCodeChain.RootOf(h.MaHopDong))]);
                var daKetThuc = h.TrangThai == TrangThaiHopDong.DaKetThuc;

                foreach (var acc in new[] { accKetThuc, tong })
                {
                    acc.HetHan++;
                    if (daKetThuc)
                    {
                        acc.HetHanDaKetThuc++;
                        if (maKeTiep != null) acc.HetHanCoGiaHan++; else acc.TraPhong++;
                    }
                }
            }

            // Thẻ hiện tại: mốc là đầu ngày VN (khớp HopDongService/HopDongStore).
            var moc = DateTime.SpecifyKind(nowUtc.AddHours(7).Date, DateTimeKind.Utc).AddHours(-7);
            var dangHoatDong = hopDongs.Where(h => h.TrangThai == TrangThaiHopDong.DangHoatDong).ToList();

            return new BaoCaoHopDongDto
            {
                PhamVi = phamVi,
                DangHoatDong = dangHoatDong.Count,
                QuaHanChuaXuLy = dangHoatDong.Count(h => h.KetThucUtc != null && h.KetThucUtc < moc),
                SapHetHan = dangHoatDong.Count(h => h.KetThucUtc != null && h.KetThucUtc >= moc && h.KetThucUtc < moc.AddDays(CuaSoSapHetHanNgay)),
                Tong = tong.ToDto("Tổng", null, null),
                TheoKy = kys.Select(k => theoKy[k.Key].ToDto(k.Text, k.Thang, k.Nam)).ToList()
            };
        }
    }
}
