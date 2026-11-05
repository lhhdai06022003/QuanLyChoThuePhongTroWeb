using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeBaoCaoExcelExporter : IBaoCaoExcelExporter
    {
        public List<string> Calls { get; } = new();

        public byte[] XuatDoanhThu(BaoCaoDoanhThuDto data) { Calls.Add(nameof(XuatDoanhThu)); return new byte[] { 1 }; }
        public byte[] XuatCongNo(BaoCaoCongNoDto data) { Calls.Add(nameof(XuatCongNo)); return new byte[] { 2 }; }
        public byte[] XuatCongSuatPhong(BaoCaoCongSuatDto data) { Calls.Add(nameof(XuatCongSuatPhong)); return new byte[] { 3 }; }
        public byte[] XuatHopDong(BaoCaoHopDongDto data) { Calls.Add(nameof(XuatHopDong)); return new byte[] { 4 }; }
        public byte[] XuatDienNuoc(BaoCaoDienNuocDto data) { Calls.Add(nameof(XuatDienNuoc)); return new byte[] { 5 }; }
    }
}
