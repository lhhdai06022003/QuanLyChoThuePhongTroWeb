using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class HoaDonCalculatorService : IHoaDonCalculatorService
    {
        public (decimal SoTien, string DienGiai, int SoNgayO) TinhTienPhong(decimal giaThue, DateTime batDau, DateTime? ketThuc, int thang, int nam)
        {
            var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            
            int daysInMonth = DateTime.DaysInMonth(nam, thang);

            DateTime activeStartVn = batDau > startOfMonthUtc ? batDau.AddHours(7).Date : startOfMonthVn.Date;
            DateTime activeEndVn = (ketThuc != null && ketThuc.Value < endOfMonthUtc) ? ketThuc.Value.AddHours(7).Date : endOfMonthVn.Date;
            
            int activeDays = (activeEndVn - activeStartVn).Days + 1;

            if (activeDays <= 0) return (0m, "Không có ngày ở thực tế", 0);

            decimal tienPhong = decimal.Round((giaThue / daysInMonth) * activeDays, 2, MidpointRounding.AwayFromZero);

            string dienGiai = "Tiền thuê phòng";
            if (activeDays < daysInMonth)
            {
                dienGiai = $"Tiền thuê phòng (thực tế ở {activeDays}/{daysInMonth} ngày)";
            }

            return (tienPhong, dienGiai, activeDays);
        }

        public (decimal SoLuong, decimal SoTien, string DienGiai) TinhTienDienNuoc(decimal chiSoMoi, decimal chiSoCu, decimal donGia, string tenDichVu, int soNgayO, int tongNgayTrongThang)
        {
            if (chiSoMoi < chiSoCu)
            {
                throw new InvalidOperationException($"Chỉ số {tenDichVu} mới ({chiSoMoi}) nhỏ hơn chỉ số cũ ({chiSoCu}).");
            }

            decimal tongTieuThu = chiSoMoi - chiSoCu;
            decimal tyLe = (decimal)soNgayO / tongNgayTrongThang;
            decimal soLuongTyLe = decimal.Round(tongTieuThu * tyLe, 3, MidpointRounding.AwayFromZero);
            decimal soTien = decimal.Round(soLuongTyLe * donGia, 2, MidpointRounding.AwayFromZero);
            
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

        public (decimal SoTien, string DienGiai) TinhTienDichVuCoDinh(decimal giaDv, decimal soLuong, string tenDichVu, DateTime batDau, DateTime? ketThuc, int thang, int nam, DateTime? contractStart = null, DateTime? contractEnd = null)
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

            if (activeDays <= 0) return (0m, $"Dịch vụ {tenDichVu} không phát sinh");

            decimal tienDichVu = decimal.Round((giaDv * soLuong / daysInMonth) * activeDays, 2, MidpointRounding.AwayFromZero);
            
            string dienGiai = tenDichVu;
            if (activeDays < daysInMonth)
            {
                dienGiai = $"{tenDichVu} (sử dụng {activeDays}/{daysInMonth} ngày)";
            }

            return (tienDichVu, dienGiai);
        }
    }
}
