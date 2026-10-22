using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public static class InvoiceIncidentLines
    {
        public static List<ChiTietHoaDon> BuildIncidentLines(IEnumerable<YeuCauSuCo> incidents)
        {
            var lines = new List<ChiTietHoaDon>();
            if (incidents == null) return lines;

            foreach (var sc in incidents.Where(x => !x.IsDeleted && x.CongVaoHoaDon && x.ChiPhiSuaChua > 0 && x.TrangThai == TrangThaiSuCo.DaHoanThanh))
            {
                lines.Add(new ChiTietHoaDon
                {
                    TenDichVu = InvoiceLineNames.SuCoPrefix + sc.TieuDe,
                    DonGia = sc.ChiPhiSuaChua,
                    SoLuong = 1,
                    TongTien = sc.ChiPhiSuaChua,
                    DichVuId = null
                });
            }

            return lines;
        }

        public static void ReplaceIncidentLines(HoaDon hoaDon, IReadOnlyList<ChiTietHoaDon> newIncidentLines)
        {
            if (hoaDon == null) return;

            foreach (var line in hoaDon.ChiTietHoaDonDichVus)
            {
                if (line.DichVuId == null && line.TenDichVu != null && line.TenDichVu.StartsWith(InvoiceLineNames.SuCoPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    line.IsDeleted = true;
                }
            }

            if (newIncidentLines != null)
            {
                foreach (var newline in newIncidentLines)
                {
                    newline.HoaDonId = hoaDon.HoaDonId;
                    newline.HoaDon = hoaDon;
                    hoaDon.ChiTietHoaDonDichVus.Add(newline);
                }
            }
        }
    }
}
