using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public static class InvoiceDueDateCalculator
    {
        public static DateTime Compute(DateTime ngayGuiUtc, int soNgay)
        {
            if (soNgay <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(soNgay), "Số ngày hạn thanh toán phải lớn hơn 0.");
            }

            if (ngayGuiUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException(nameof(ngayGuiUtc), "Thời điểm gửi hóa đơn phải là UTC.");
            }

            var ngayVn = ngayGuiUtc.AddHours(7).Date;
            var hanUtc = DateTime.SpecifyKind(ngayVn.AddDays(soNgay).AddHours(23).AddMinutes(59).AddSeconds(59).AddHours(-7), DateTimeKind.Utc);
            return hanUtc;
        }
    }
}
