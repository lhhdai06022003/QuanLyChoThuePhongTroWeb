namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public class AutoInvoiceOptions
    {
        public const string SectionName = "AutoInvoiceOptions";

        public double NguongDoTinCay { get; set; } = 0.90;
        public decimal HeSoBatThuong { get; set; } = 3m;
        public int SoKyTrungBinh { get; set; } = 3;
        public decimal TranDienKwh { get; set; } = 500m;
        public decimal TranNuocM3 { get; set; } = 30m;
        // Chu kỳ job tự chốt (đợt B); đợt A chỉ khai báo và kiểm hợp lệ.
        public int ChuKyJobPhut { get; set; } = 5;

        public void Validate()
        {
            if (double.IsNaN(NguongDoTinCay) || NguongDoTinCay <= 0 || NguongDoTinCay > 1)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:NguongDoTinCay phải lớn hơn 0 và không vượt quá 1.");
            }

            if (HeSoBatThuong <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:HeSoBatThuong phải lớn hơn 0.");
            }

            if (SoKyTrungBinh < 1)
            {
                throw new System.InvalidOperationException("Cấu hình AutoInvoiceOptions:SoKyTrungBinh phải lớn hơn hoặc bằng 1.");
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
