using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class HoaDonCalculatorService : IHoaDonCalculatorService
    {
        public (double SoTien, string DienGiai, int SoNgayO) TinhTienPhong(double giaThue, DateTime batDau, DateTime? ketThuc, int thang, int nam)
        {
            var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            
            int daysInMonth = DateTime.DaysInMonth(nam, thang);

            DateTime activeStartVn = batDau > startOfMonthUtc ? batDau.AddHours(7).Date : startOfMonthVn.Date;
            DateTime activeEndVn = (ketThuc != null && ketThuc.Value < endOfMonthUtc) ? ketThuc.Value.AddHours(7).Date : endOfMonthVn.Date;
            
            int activeDays = (activeEndVn - activeStartVn).Days + 1;

            if (activeDays <= 0) return (0, "Không có ngày ở thực tế", 0);

            double tienPhong = Math.Round((giaThue / daysInMonth) * activeDays);

            string dienGiai = "Tiền thuê phòng";
            if (activeDays < daysInMonth)
            {
                dienGiai = $"Tiền thuê phòng (thực tế ở {activeDays}/{daysInMonth} ngày)";
            }

            return (tienPhong, dienGiai, activeDays);
        }

        public (double SoLuong, double SoTien, string DienGiai) TinhTienDienNuoc(double chiSoMoi, double chiSoCu, double donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang)
        {
            if (chiSoMoi < chiSoCu)
            {
                throw new InvalidOperationException($"Chỉ số {tenDichVu} mới ({chiSoMoi}) nhỏ hơn chỉ số cũ ({chiSoCu}).");
            }

            double tongTieuThu = chiSoMoi - chiSoCu;
            double tyLe = (double)soNgayO / tongNgayTrongThang;
            double soLuongTyLe = tongTieuThu * tyLe;
            double soTien = Math.Round(soLuongTyLe * donGia);
            
            string dienGiai = tenDichVu;
            if (soNgayO < tongNgayTrongThang)
            {
                dienGiai = $"{tenDichVu} (tỷ lệ {soNgayO}/{tongNgayTrongThang} ngày, {chiSoCu} → {chiSoMoi})";
            }
            else
            {
                dienGiai = $"{tenDichVu} ({chiSoCu} → {chiSoMoi})";
            }

            return (soLuongTyLe, soTien, dienGiai);
        }

        public (double SoTien, string DienGiai) TinhTienDichVuCoDinh(double giaDv, int soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null)
        {
            var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            
            int daysInMonth = DateTime.DaysInMonth(nam, thang);

            DateTime activeStartVn = batDau > startOfMonthUtc ? batDau.AddHours(7).Date : startOfMonthVn.Date;
            DateTime activeEndVn = (ketThuc != null && ketThuc.Value < endOfMonthUtc) ? ketThuc.Value.AddHours(7).Date : endOfMonthVn.Date;

            // Clamp theo phạm vi hợp đồng (nếu có)
            if (contractStart.HasValue)
            {
                var contractStartVn = contractStart.Value > startOfMonthUtc ? contractStart.Value.AddHours(7).Date : startOfMonthVn.Date;
                if (contractStartVn > activeStartVn)
                    activeStartVn = contractStartVn;
            }
            if (contractEnd.HasValue)
            {
                var contractEndVn = contractEnd.Value < endOfMonthUtc ? contractEnd.Value.AddHours(7).Date : endOfMonthVn.Date;
                if (contractEndVn < activeEndVn)
                    activeEndVn = contractEndVn;
            }
            
            int activeDays = (activeEndVn - activeStartVn).Days + 1;

            if (activeDays <= 0) return (0, $"Dịch vụ {tenDichVu} không phát sinh");

            double tienDichVu = Math.Round((giaDv * soLuong / daysInMonth) * activeDays);
            
            string dienGiai = tenDichVu;
            if (activeDays < daysInMonth)
            {
                dienGiai = $"{tenDichVu} (sử dụng {activeDays}/{daysInMonth} ngày)";
            }

            return (tienDichVu, dienGiai);
        }
    }
}
