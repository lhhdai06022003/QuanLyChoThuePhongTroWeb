using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters
{
    public class BaoCaoExcelExporter : IBaoCaoExcelExporter
    {
        private const string FormatTien = "#,##0";
        private const string FormatChiSo = "#,##0.###";
        private const string FormatTyLe = "0.0";
        private const string KhongCo = "—";

        // Kiểu cột: T = chữ, I = số nguyên, M = tiền, C = chỉ số, P = tỷ lệ %.
        private sealed record Cot(string TieuDe, char Kieu);

        public byte[] XuatDoanhThu(BaoCaoDoanhThuDto data)
        {
            using var workbook = new XLWorkbook();
            TongHop(workbook, data.PhamVi, false, new (string, object?, char)[]
            {
                ("Số hóa đơn", data.Tong.SoHoaDon, 'I'),
                ("Phát sinh", data.Tong.PhatSinh, 'M'),
                ("Thu tiền", data.Tong.ThuTien, 'M'),
                ("Cấn trừ cọc", data.Tong.CanTruCoc, 'M'),
                ("Đã thu", data.Tong.DaThu, 'M'),
                ("Còn nợ", data.Tong.ConNo, 'M'),
                ("Tỷ lệ thu (%)", data.Tong.TyLeThu, 'P'),
                ("Tiền thừa", data.Tong.TienThua, 'M'),
                ("Tiền cọc đang giữ", data.TienCocDangGiu, 'M'),
                ("Thu khác: tiền giữ chỗ không hoàn", data.Tong.ThuKhacGiuCho, 'M'),
                ("Tiền giữ chỗ đang chờ quyết định", data.GiuChoDangChoQuyetDinh, 'M')
            });

            var cots = new[]
            {
                new Cot("Kỳ", 'T'), new Cot("Số hóa đơn", 'I'), new Cot("Phát sinh", 'M'), new Cot("Thu tiền", 'M'), new Cot("Cấn trừ cọc", 'M'),
                new Cot("Đã thu", 'M'), new Cot("Còn nợ", 'M'), new Cot("Tỷ lệ thu (%)", 'P'), new Cot("Tiền mặt", 'M'),
                new Cot("Chuyển khoản", 'M'), new Cot("SePay", 'M'), new Cot("VNPay", 'M'), new Cot("Tiền thừa", 'M'), new Cot("Thu khác", 'M')
            };
            object?[] Dong(BaoCaoDoanhThuDongDto d) => new object?[]
            {
                d.Nhan, d.SoHoaDon, d.PhatSinh, d.ThuTien, d.CanTruCoc, d.DaThu, d.ConNo, d.TyLeThu,
                d.TienMat, d.ChuyenKhoan, d.SePay, d.VnPay, d.TienThua, d.ThuKhacGiuCho
            };

            Bang(workbook, "Theo kỳ", cots, data.TheoKy.Select(Dong).Append(Dong(data.Tong)));
            if (data.TheoChiNhanh.Count > 0)
            {
                var cotsCn = cots.ToArray();
                cotsCn[0] = new Cot("Chi nhánh", 'T');
                Bang(workbook, "Theo chi nhánh", cotsCn, data.TheoChiNhanh.Select(Dong));
            }

            return Luu(workbook);
        }

        public byte[] XuatCongNo(BaoCaoCongNoDto data)
        {
            using var workbook = new XLWorkbook();
            TongHop(workbook, data.PhamVi, true, new (string, object?, char)[]
            {
                ("Tổng còn nợ", data.TongConNo, 'M'),
                ("Quá hạn", data.QuaHan, 'M'),
                ("Số khách nợ", data.SoKhachNo, 'I'),
                ("Số hóa đơn nợ", data.SoHoaDonNo, 'I'),
                ("Số hóa đơn quá hạn", data.SoHoaDonQuaHan, 'I'),
                ("Nợ của khách đã trả phòng", data.NoKhachDaTraPhong, 'M')
            });

            Bang(workbook, "Tuổi nợ",
                new[] { new Cot("Nhóm", 'T'), new Cot("Số hóa đơn", 'I'), new Cot("Số tiền", 'M') },
                data.NhomTuoiNo.Select(n => new object?[] { n.Nhan, n.SoHoaDon, n.SoTien }));
            Bang(workbook, "Theo khách",
                new[]
                {
                    new Cot("Khách thuê", 'T'), new Cot("Phòng", 'T'), new Cot("Chi nhánh", 'T'), new Cot("Số hóa đơn", 'I'), new Cot("Tổng nợ", 'M'),
                    new Cot("Kỳ nợ cũ nhất", 'T'), new Cot("Quá hạn lâu nhất (ngày)", 'I'), new Cot("Đã trả phòng", 'T')
                },
                data.TheoKhach.Select(k => new object?[]
                {
                    k.HoVaTen, k.SoPhong, k.TenChiNhanh, k.SoHoaDon, k.TongNo, k.KyNoCuNhat, k.SoNgayQuaHanLonNhat, k.DaTraPhong ? "Có" : "Không"
                }));

            return Luu(workbook);
        }

        public byte[] XuatCongSuatPhong(BaoCaoCongSuatDto data)
        {
            using var workbook = new XLWorkbook();
            TongHop(workbook, data.PhamVi, false, new (string, object?, char)[]
            {
                ("Tổng phòng", data.TongPhong, 'I'),
                ("Đang thuê", data.DangThue, 'I'),
                ("Trống", data.Trong, 'I'),
                ("Bảo trì", data.BaoTri, 'I'),
                ("Ngày-phòng có thể cho thuê", data.Tong.NgayPhongCoTheThue, 'I'),
                ("Ngày-phòng đã thuê", data.Tong.NgayPhongDaThue, 'I'),
                ("Tỷ lệ lấp đầy (%)", data.Tong.TyLeLapDay, 'P')
            });

            var cots = new[] { new Cot("Kỳ", 'T'), new Cot("Ngày-phòng có thể cho thuê", 'I'), new Cot("Ngày-phòng đã thuê", 'I'), new Cot("Tỷ lệ lấp đầy (%)", 'P') };
            object?[] Dong(BaoCaoCongSuatDongDto d) => new object?[] { d.Nhan, d.NgayPhongCoTheThue, d.NgayPhongDaThue, d.TyLeLapDay };
            Bang(workbook, "Theo kỳ", cots, data.TheoKy.Select(Dong).Append(Dong(data.Tong)));
            if (data.TheoChiNhanh.Count > 0)
            {
                Bang(workbook, "Theo chi nhánh", new[] { new Cot("Chi nhánh", 'T'), cots[1], cots[2], cots[3] }, data.TheoChiNhanh.Select(Dong));
            }

            Bang(workbook, "Phòng trống lâu",
                new[] { new Cot("Phòng", 'T'), new Cot("Chi nhánh", 'T'), new Cot("Số ngày trống", 'I'), new Cot("Từ ngày", 'T') },
                data.PhongTrongLauNhat.Select(p => new object?[] { p.SoPhong, p.TenChiNhanh, p.SoNgayTrong, p.TuNgayText }));

            return Luu(workbook);
        }

        public byte[] XuatHopDong(BaoCaoHopDongDto data)
        {
            using var workbook = new XLWorkbook();
            TongHop(workbook, data.PhamVi, false, new (string, object?, char)[]
            {
                ("Đang hoạt động", data.DangHoatDong, 'I'),
                ("Quá hạn chưa xử lý", data.QuaHanChuaXuLy, 'I'),
                ("Sắp hết hạn (30 ngày)", data.SapHetHan, 'I'),
                ("Hợp đồng mới", data.Tong.HopDongMoi, 'I'),
                ("Gia hạn", data.Tong.GiaHan, 'I'),
                ("Trả phòng", data.Tong.TraPhong, 'I'),
                ("Tỷ lệ gia hạn (%)", data.Tong.TyLeGiaHan, 'P')
            });

            object?[] Dong(BaoCaoHopDongDongDto d) => new object?[]
            {
                d.Nhan, d.HopDongMoi, d.GiaHan, d.TraPhong, d.HetHanTrongKy, d.HetHanDaKetThuc, d.HetHanCoGiaHan, d.TyLeGiaHan
            };
            Bang(workbook, "Theo kỳ",
                new[]
                {
                    new Cot("Kỳ", 'T'), new Cot("Hợp đồng mới", 'I'), new Cot("Gia hạn", 'I'), new Cot("Trả phòng", 'I'), new Cot("Hết hạn trong kỳ", 'I'),
                    new Cot("Hết hạn đã kết thúc", 'I'), new Cot("Trong đó có gia hạn", 'I'), new Cot("Tỷ lệ gia hạn (%)", 'P')
                },
                data.TheoKy.Select(Dong).Append(Dong(data.Tong)));

            return Luu(workbook);
        }

        public byte[] XuatDienNuoc(BaoCaoDienNuocDto data)
        {
            using var workbook = new XLWorkbook();
            TongHop(workbook, data.PhamVi, false, new (string, object?, char)[]
            {
                ("Tổng kWh", data.TongKwh, 'C'),
                ("Tổng m³", data.TongM3, 'C'),
                ("Tiền điện", data.TienDien, 'M'),
                ("Tiền nước", data.TienNuoc, 'M'),
                ("TB kWh/phòng/tháng", data.TrungBinhKwhPhongThang, 'C'),
                ("Số bản ghi", data.SoBanGhi, 'I'),
                ("Bản ghi bỏ qua (chỉ số mới nhỏ hơn chỉ số cũ)", data.SoBanGhiBoQua, 'I')
            });

            Bang(workbook, "Theo kỳ",
                new[]
                {
                    new Cot("Kỳ", 'T'), new Cot("kWh", 'C'), new Cot("m³", 'C'), new Cot("Tiền điện", 'M'), new Cot("Tiền nước", 'M'),
                    new Cot("Số bản ghi", 'I'), new Cot("Bản ghi bỏ qua", 'I')
                },
                data.TheoKy.Select(d => new object?[] { d.Nhan, d.Kwh, d.M3, d.TienDien, d.TienNuoc, d.SoBanGhi, d.SoBanGhiBoQua }));
            Bang(workbook, "Phòng dùng nhiều",
                new[]
                {
                    new Cot("Phòng", 'T'), new Cot("Chi nhánh", 'T'), new Cot("kWh", 'C'), new Cot("m³", 'C'), new Cot("Tiền điện", 'M'),
                    new Cot("Tiền nước", 'M'), new Cot("Số kỳ", 'I')
                },
                data.TopPhong.Select(p => new object?[] { p.SoPhong, p.TenChiNhanh, p.Kwh, p.M3, p.TienDien, p.TienNuoc, p.SoKy }));

            return Luu(workbook);
        }

        // Sheet đầu tiên "Tổng hợp": cột A là nhãn, cột B là giá trị. Tab Công nợ ghi "Tính tới" thay cho khoảng kỳ.
        private static void TongHop(XLWorkbook workbook, BaoCaoPhamViDto phamVi, bool tinhToi, IEnumerable<(string Nhan, object? GiaTri, char Kieu)> cacO)
        {
            var ws = workbook.Worksheets.Add("Tổng hợp");
            var dong = 1;
            void Ghi(string nhan, object? giaTri, char kieu)
            {
                ws.Cell(dong, 1).Value = nhan;
                GhiO(ws.Cell(dong, 2), giaTri, kieu);
                dong++;
            }

            Ghi("Chi nhánh", phamVi.TenChiNhanh, 'T');
            if (tinhToi)
            {
                Ghi("Tính tới", phamVi.ThoiDiemTinhText, 'T');
            }
            else
            {
                Ghi("Từ kỳ", phamVi.TuKyText, 'T');
                Ghi("Đến kỳ", phamVi.DenKyText, 'T');
            }

            Ghi("Ngày xuất", phamVi.ThoiDiemTinhText, 'T');
            dong++;
            foreach (var o in cacO)
            {
                Ghi(o.Nhan, o.GiaTri, o.Kieu);
            }

            ws.Column(1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();
        }

        private static void Bang(XLWorkbook workbook, string ten, Cot[] cots, IEnumerable<object?[]> dongs)
        {
            var ws = workbook.Worksheets.Add(ten);
            for (var c = 0; c < cots.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = cots[c].TieuDe;
                cell.Style.Font.Bold = true;
            }

            var r = 2;
            foreach (var dong in dongs)
            {
                for (var c = 0; c < cots.Length; c++)
                {
                    GhiO(ws.Cell(r, c + 1), dong[c], cots[c].Kieu);
                }

                r++;
            }

            ws.Columns().AdjustToContents();
        }

        // Ô số ghi kiểu số (không ghi chuỗi); giá trị null (không có mẫu số) ghi dấu gạch.
        private static void GhiO(IXLCell cell, object? giaTri, char kieu)
        {
            if (giaTri == null)
            {
                cell.Value = KhongCo;
                return;
            }

            switch (giaTri)
            {
                case decimal d:
                    cell.Value = d;
                    cell.Style.NumberFormat.Format = kieu switch { 'M' => FormatTien, 'C' => FormatChiSo, 'P' => FormatTyLe, _ => FormatTien };
                    break;
                case int i:
                    cell.Value = i;
                    cell.Style.NumberFormat.Format = "#,##0";
                    break;
                default:
                    cell.Value = Convert.ToString(giaTri, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    break;
            }
        }

        private static byte[] Luu(XLWorkbook workbook)
        {
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
