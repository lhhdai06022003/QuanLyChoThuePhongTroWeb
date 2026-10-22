namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public class InvoiceIssuanceOptions
    {
        public const string SectionName = "InvoiceIssuanceOptions";

        public int SoNgayHanThanhToan { get; set; } = 7;

        public void Validate()
        {
            if (SoNgayHanThanhToan <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình InvoiceIssuanceOptions:SoNgayHanThanhToan phải lớn hơn 0.");
            }
        }
    }
}
