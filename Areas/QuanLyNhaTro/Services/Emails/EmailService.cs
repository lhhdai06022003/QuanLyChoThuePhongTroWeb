using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Utils;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Emails
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
        {
            try
            {
                // 1. Đọc cấu hình SMTP
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var portStr = _configuration["EmailSettings:Port"] ?? "587";
                int port = int.TryParse(portStr, out int p) ? p : 587;
                var senderName = _configuration["EmailSettings:SenderName"] ?? "Hệ thống Quản lý Nhà Trọ";
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var password = _configuration["EmailSettings:Password"];

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(password))
                {
                    return (false, "Chưa cấu hình tài khoản gửi thư (SenderEmail/Password) trong appsettings.json.");
                }

                // 2. Đọc cấu hình VietQR để hiển thị trong email
                var bankId = _configuration["VietQRSettings:BankId"] ?? "MB";
                var accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
                var accountName = _configuration["VietQRSettings:AccountName"] ?? "";

                // 3. Tạo thông điệp Email
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(hoaDon.TenNguoiThue, toEmail));
                message.Subject = $"[HÓA ĐƠN TIỀN PHÒNG] - Phòng {hoaDon.TenPhong} - Kỳ tháng {hoaDon.Thang}/{hoaDon.Nam}";

                var bodyBuilder = new BodyBuilder();

                // Tạo định dạng số VND
                string FormatVND(double amount)
                {
                    return string.Format("{0:#,##0}", amount);
                }

                // Sinh link ảnh QR thanh toán
                string memo = $"THANH TOAN {hoaDon.MaHoaDon}";
                string qrUrl = $"https://img.vietqr.io/image/{bankId}-{accountNumber}-compact2.png?amount={hoaDon.TongTien}&addInfo={Uri.EscapeDataString(memo)}&accountName={Uri.EscapeDataString(accountName)}";

                // Xây dựng các dòng chi tiết dịch vụ
                var lineItemsHtml = new StringBuilder();
                int stt = 1;
                foreach (var ct in hoaDon.ChiTietHoaDons)
                {
                    string donViText = string.IsNullOrEmpty(ct.DonVi) ? "-" : ct.DonVi;
                    lineItemsHtml.Append($@"
                        <tr>
                            <td style='text-align: center; border: 1px solid #dcdfe6; padding: 10px;'>{stt++}</td>
                            <td style='border: 1px solid #dcdfe6; padding: 10px;'>{ct.TenDichVu}</td>
                            <td style='text-align: right; border: 1px solid #dcdfe6; padding: 10px;'>{FormatVND(ct.DonGia)}</td>
                            <td style='text-align: center; border: 1px solid #dcdfe6; padding: 10px;'>{ct.SoLuong}</td>
                            <td style='text-align: center; border: 1px solid #dcdfe6; padding: 10px;'>{donViText}</td>
                            <td style='text-align: right; border: 1px solid #dcdfe6; padding: 10px; font-weight: bold;'>{FormatVND(ct.TongTien)}</td>
                        </tr>");
                }

                // Template HTML
                bodyBuilder.HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #333; line-height: 1.6; background-color: #f4f6fa; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; background-color: #ffffff; margin: 0 auto; border-radius: 8px; padding: 30px; box-shadow: 0 4px 10px rgba(0,0,0,0.05); border: 1px solid #e1e4e8; }}
        .header {{ text-align: center; border-bottom: 2px solid #0056b3; padding-bottom: 15px; margin-bottom: 25px; }}
        .header h2 {{ color: #0056b3; margin: 0; font-size: 24px; text-transform: uppercase; }}
        .header p {{ color: #666; margin: 5px 0 0 0; font-size: 14px; }}
        .info-table {{ width: 100%; border-collapse: collapse; margin-bottom: 25px; }}
        .info-table td {{ padding: 8px 0; font-size: 15px; }}
        .info-table td.label {{ font-weight: bold; color: #555; width: 35%; }}
        .info-table td.value {{ color: #111; }}
        .details-table {{ width: 100%; border-collapse: collapse; margin-bottom: 25px; }}
        .details-table th {{ background-color: #f1f3f9; border: 1px solid #dcdfe6; color: #0056b3; font-weight: bold; padding: 10px; text-align: left; font-size: 14px; }}
        .total-row {{ font-weight: bold; font-size: 16px; color: #d9001b; }}
        .qr-section {{ background-color: #f9f9fb; border: 1px dashed #409eff; border-radius: 6px; padding: 20px; text-align: center; margin-top: 25px; }}
        .qr-section h3 {{ margin: 0 0 10px 0; color: #0056b3; font-size: 16px; }}
        .qr-section img {{ max-width: 180px; height: auto; border: 1px solid #ebeef5; border-radius: 4px; }}
        .footer {{ text-align: center; margin-top: 30px; font-size: 12px; color: #999; border-top: 1px solid #eee; padding-top: 15px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Hóa Đơn Thanh Tự Tiền Phòng</h2>
            <p>{hoaDon.TenChiNhanh}</p>
        </div>
        
        <table class='info-table'>
            <tr><td class='label'>Mã hóa đơn:</td><td class='value'><strong>{hoaDon.MaHoaDon}</strong></td></tr>
            <tr><td class='label'>Phòng:</td><td class='value' style='color: #0056b3; font-weight: bold;'>{hoaDon.TenPhong}</td></tr>
            <tr><td class='label'>Người thuê:</td><td class='value'>{hoaDon.TenNguoiThue}</td></tr>
            <tr><td class='label'>Kỳ thanh toán:</td><td class='value'>Tháng {hoaDon.Thang}/{hoaDon.Nam}</td></tr>
            <tr><td class='label'>Trạng thái:</td><td class='value'><span style='color: #e6a23c; font-weight: bold;'>{hoaDon.TrangThaiHoaDon}</span></td></tr>
        </table>
        
        <table class='details-table'>
            <thead>
                <tr>
                    <th style='text-align: center;'>STT</th>
                    <th>Dịch vụ</th>
                    <th style='text-align: right;'>Đơn giá</th>
                    <th style='text-align: center;'>SL</th>
                    <th style='text-align: center;'>Đơn vị</th>
                    <th style='text-align: right;'>Thành tiền</th>
                </tr>
            </thead>
            <tbody>
                {lineItemsHtml}
                <tr class='total-row'>
                    <td colspan='5' style='text-align: right; text-transform: uppercase; border: 1px solid #dcdfe6; padding: 10px;'>Tổng cộng:</td>
                    <td style='text-align: right; color: #d9001b; border: 1px solid #dcdfe6; padding: 10px;'>{FormatVND(hoaDon.TongTien)} đ</td>
                </tr>
            </tbody>
        </table>
        
        <div class='qr-section'>
            <h3>Quét mã QR để chuyển khoản nhanh</h3>
            <img src='{qrUrl}' alt='VietQR' />
            <div style='text-align: left; font-size: 13px; margin-top: 15px; color: #444;'>
                <div>- Ngân hàng: <strong>{bankId}</strong></div>
                <div>- Số tài khoản: <strong>{accountNumber}</strong></div>
                <div>- Tên chủ TK: <strong>{accountName}</strong></div>
                <div>- Số tiền: <strong style='color: #d9001b;'>{FormatVND(hoaDon.TongTien)} đ</strong></div>
                <div>- Nội dung: <code style='color: #0056b3; font-weight: bold; font-size: 14px;'>THANH TOAN {hoaDon.MaHoaDon}</code></div>
            </div>
        </div>
        
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống quản lý nhà trọ.</p>
            <p>Vui lòng không phản hồi trực tiếp email này. Xin cảm ơn!</p>
        </div>
    </div>
</body>
</html>";

                // 4. Đính kèm file PDF hóa đơn
                if (pdfBytes != null && pdfBytes.Length > 0)
                {
                    string safeFileName = $"HoaDon_{hoaDon.MaHoaDon}.pdf";
                    bodyBuilder.Attachments.Add(safeFileName, pdfBytes, new ContentType("application", "pdf"));
                }

                message.Body = bodyBuilder.ToMessageBody();

                // 5. Kết nối SMTP và gửi
                using (var client = new SmtpClient())
                {
                    // Cài đặt xử lý chứng chỉ SSL (cho phép chứng chỉ tự ký nếu cần, nhưng tốt nhất dùng StartTls)
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(senderEmail, password);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
