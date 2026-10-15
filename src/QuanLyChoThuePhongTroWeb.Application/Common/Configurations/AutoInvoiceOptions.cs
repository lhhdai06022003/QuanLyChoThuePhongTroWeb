namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public class AutoInvoiceOptions
    {
        public const string SectionName = "AutoInvoiceOptions";

        public double NguongDoTinCay { get; set; } = 0.90;
        // Số kỳ lịch sử tối đa lấy trung vị và số kỳ tối thiểu để so với trung vị (ít hơn thì so với trần chung).
        public int SoKyLichSuToiDa { get; set; } = 6;
        public int SoKyToiThieu { get; set; } = 2;
        // Tăng bất thường: tiêu thụ > trung vị × HeSoTang và vượt trung vị quá sàn.
        public decimal HeSoTang { get; set; } = 2m;
        public decimal SanTangDienKwh { get; set; } = 50m;
        public decimal SanTangNuocM3 { get; set; } = 5m;
        // Thấp bất thường: trung vị > ngưỡng xét và tiêu thụ < trung vị × HeSoThap.
        public decimal HeSoThap { get; set; } = 0.3m;
        public decimal NguongXetThapDienKwh { get; set; } = 20m;
        public decimal NguongXetThapNuocM3 { get; set; } = 2m;
        // Trần tiêu thụ cả tháng khi hợp đồng chưa đủ lịch sử.
        public decimal TranDienKwh { get; set; } = 500m;
        public decimal TranNuocM3 { get; set; } = 30m;
        // Chu kỳ chạy job tự chốt hóa đơn tạm tính (phút).
        public int ChuKyJobPhut { get; set; } = 5;

        public void Validate()
        {
            if (double.IsNaN(NguongDoTinCay) || NguongDoTinCay <= 0 || NguongDoTinCay > 1)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:NguongDoTinCay phải lớn hơn 0 và không vượt quá 1.");
            }

            if (SoKyLichSuToiDa < 1)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:SoKyLichSuToiDa phải lớn hơn hoặc bằng 1.");
            }

            if (SoKyToiThieu < 1 || SoKyToiThieu > SoKyLichSuToiDa)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:SoKyToiThieu phải lớn hơn hoặc bằng 1 và không vượt quá SoKyLichSuToiDa.");
            }

            if (HeSoTang <= 1)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:HeSoTang phải lớn hơn 1.");
            }

            if (SanTangDienKwh < 0)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:SanTangDienKwh phải lớn hơn hoặc bằng 0.");
            }

            if (SanTangNuocM3 < 0)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:SanTangNuocM3 phải lớn hơn hoặc bằng 0.");
            }

            if (HeSoThap <= 0 || HeSoThap >= 1)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:HeSoThap phải lớn hơn 0 và nhỏ hơn 1.");
            }

            if (NguongXetThapDienKwh < 0)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:NguongXetThapDienKwh phải lớn hơn hoặc bằng 0.");
            }

            if (NguongXetThapNuocM3 < 0)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:NguongXetThapNuocM3 phải lớn hơn hoặc bằng 0.");
            }

            if (TranDienKwh <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:TranDienKwh phải lớn hơn 0.");
            }

            if (TranNuocM3 <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:TranNuocM3 phải lớn hơn 0.");
            }

            if (ChuKyJobPhut < 1)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:ChuKyJobPhut phải lớn hơn hoặc bằng 1.");
            }
        }
    }
}
