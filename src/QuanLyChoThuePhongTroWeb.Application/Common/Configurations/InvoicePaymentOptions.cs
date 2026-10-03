namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public class InvoicePaymentOptions
    {
        public const string SectionName = "InvoicePayment";

        // Hạn của một lượt thanh toán tính từ lúc tạo hoặc lúc minh chứng bị từ chối.
        public int YeuCauHetHanSauGio { get; set; } = 24;

        public long MinhChungToiDaBytes { get; set; } = 5 * 1024 * 1024;

        // Ngày khách khai chuyển hoặc ngày giao dịch không được sau giờ server quá số phút này.
        public int DungSaiNgayTuongLaiPhut { get; set; } = 5;

        public void Validate()
        {
            if (YeuCauHetHanSauGio <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình InvoicePayment:YeuCauHetHanSauGio phải lớn hơn 0.");
            }

            if (MinhChungToiDaBytes <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình InvoicePayment:MinhChungToiDaBytes phải lớn hơn 0.");
            }

            if (DungSaiNgayTuongLaiPhut < 0)
            {
                throw new System.InvalidOperationException("Cấu hình InvoicePayment:DungSaiNgayTuongLaiPhut không được âm.");
            }
        }
    }
}
