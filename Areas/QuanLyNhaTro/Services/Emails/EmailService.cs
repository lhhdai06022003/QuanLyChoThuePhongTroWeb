using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Utils;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Emails
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
        {
            try
            {
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

                var bankId = _configuration["VietQRSettings:BankId"] ?? "MB";
                var accountNumber = _configuration["VietQRSettings:AccountNumber"] ?? "";
                var accountName = _configuration["VietQRSettings:AccountName"] ?? "";

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(hoaDon.TenNguoiThue, toEmail));
                message.Subject = $"[HÓA ĐƠN TIỀN PHÒNG] - Phòng {hoaDon.TenPhong} - Kỳ tháng {hoaDon.Thang}/{hoaDon.Nam}";

                var bodyBuilder = new BodyBuilder();

                string FormatVND(double amount)
                {
                    return string.Format("{0:#,##0}", amount);
                }

                string memo = $"THANH TOAN {hoaDon.MaHoaDon}";
                string qrUrl = $"https://img.vietqr.io/image/{bankId}-{accountNumber}-compact2.png?amount={hoaDon.TongTien}&addInfo={Uri.EscapeDataString(memo)}&accountName={Uri.EscapeDataString(accountName)}";

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

                bodyBuilder.HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #333333; line-height: 1.6; background-color: #f4f6fa; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; background-color: #ffffff; margin: 0 auto; border-radius: 8px; padding: 30px; box-shadow: 0 4px 10px rgba(0,0,0,0.05); border: 1px solid #e1e4e8; }}
        .header {{ text-align: center; border-bottom: 2px solid #0056b3; padding-bottom: 15px; margin-bottom: 25px; }}
        .header h2 {{ color: #0056b3; margin: 0; font-size: 24px; text-transform: uppercase; }}
        .header p {{ color: #666666; margin: 5px 0 0 0; font-size: 14px; }}
        .info-table {{ width: 100%; border-collapse: collapse; margin-bottom: 25px; }}
        .info-table td {{ padding: 8px 0; font-size: 15px; }}
        .info-table td.label {{ font-weight: bold; color: #555555; width: 35%; }}
        .info-table td.value {{ color: #111111; }}
        .details-table {{ width: 100%; border-collapse: collapse; margin-bottom: 25px; }}
        .details-table th {{ background-color: #f1f3f9; border: 1px solid #dcdfe6; color: #0056b3; font-weight: bold; padding: 10px; text-align: left; font-size: 14px; }}
        .details-table td {{ border: 1px solid #dcdfe6; padding: 10px; font-size: 14px; color: #333333; }}
        .total-row {{ font-weight: bold; font-size: 16px; color: #d9001b; }}
        .qr-section {{ background-color: #f9f9fb; border: 1px dashed #409eff; border-radius: 6px; padding: 20px; text-align: center; margin-top: 25px; }}
        .qr-section h3 {{ margin: 0 0 10px 0; color: #0056b3; font-size: 16px; }}
        .qr-section img {{ max-width: 180px; height: auto; border: 1px solid #ebeef5; border-radius: 4px; background-color: #ffffff; padding: 8px; display: inline-block; }}
        .signature-block {{ text-align: left; margin-top: 30px; margin-bottom: 20px; font-size: 14px; color: #444444; border-top: 1px solid #e1e4e8; padding-top: 20px; }}
        .signature-title {{ font-size: 13px; color: #666666; font-style: italic; margin-bottom: 5px; }}
        .signature-name {{ font-weight: bold; color: #0056b3; font-size: 16px; margin: 0 0 10px 0; }}
        .signature-info {{ font-size: 13px; color: #555555; line-height: 1.5; }}
        .signature-info div {{ margin-bottom: 4px; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #999999; border-top: 1px dashed #eeeeee; padding-top: 15px; }}

        @media (prefers-color-scheme: dark) {{
            body {{ background-color: #1a1f2c !important; color: #f4f6fa !important; }}
            .container {{ background-color: #232936 !important; border-color: #384252 !important; box-shadow: 0 4px 10px rgba(0,0,0,0.2) !important; }}
            .header {{ border-bottom-color: #3080e6 !important; }}
            .header h2 {{ color: #3080e6 !important; }}
            .header p {{ color: #a0aebf !important; }}
            .info-table td.label {{ color: #a0aebf !important; }}
            .info-table td.value {{ color: #ffffff !important; }}
            .details-table th {{ background-color: #2d3545 !important; border-color: #384252 !important; color: #3080e6 !important; }}
            .details-table td {{ border-color: #384252 !important; color: #e1e8f2 !important; }}
            .details-table tr:nth-child(even) {{ background-color: #272e3d !important; }}
            .total-row td {{ color: #ff4d61 !important; }}
            .qr-section {{ background-color: #1a1f2c !important; border-color: #3080e6 !important; }}
            .qr-section h3 {{ color: #3080e6 !important; }}
            .qr-section td, .qr-section div {{ color: #a0aebf !important; }}
            .qr-section strong {{ color: #ffffff !important; }}
            .signature-block {{ border-top-color: #384252 !important; color: #e1e8f2 !important; }}
            .signature-title {{ color: #a0aebf !important; }}
            .signature-name {{ color: #3080e6 !important; }}
            .signature-info {{ color: #a0aebf !important; }}
            .signature-info a {{ color: #3080e6 !important; }}
            .footer {{ color: #707d90 !important; border-top-color: #384252 !important; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Hóa Đơn Thanh Toán Tiền Phòng</h2>
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
                    <th style='text-align: right;'>Don giá</th>
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
        
        <div class='signature-block'>
            <div class='signature-title'>Trân trọng,</div>
            <div class='signature-name'>Ban Quản Lý - {hoaDon.TenChiNhanh}</div>
            <div class='signature-info'>
                <div>📍 <strong>Địa chỉ:</strong> {hoaDon.DiaChiChiNhanh}</div>
                <div>📞 <strong>Hotline hỗ trợ:</strong> <a href='tel:{hoaDon.SoDienThoaiChiNhanh}' style='color: #0056b3; text-decoration: none; font-weight: bold;'>{hoaDon.SoDienThoaiChiNhanh}</a></div>
            </div>
        </div>
        
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống quản lý nhà trọ.</p>
            <p>Vui lòng không phản hồi trực tiếp email này. Xin cảm ơn!</p>
        </div>
    </div>
</body>
</html>";

                if (pdfBytes != null && pdfBytes.Length > 0)
                {
                    string safeFileName = $"HoaDon_{hoaDon.MaHoaDon}.pdf";
                    bodyBuilder.Attachments.Add(safeFileName, pdfBytes, new ContentType("application", "pdf"));
                }

                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(senderEmail, password);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi gửi email hóa đơn {MaHoaDon} đến {Email}", hoaDon.MaHoaDon, toEmail);
                return (false, "Lỗi hệ thống khi gửi email.");
            }
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
        {
            try
            {
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

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(tenNguoiNhan, toEmail));
                message.Subject = $"[CẢNH BÁO HẾT HẠN HỢP ĐỒNG] - Phòng {alertData.SoPhong} còn {alertData.SoNgayConLai} ngày";

                var bodyBuilder = new BodyBuilder();

                string FormatVND(double amount)
                {
                    return string.Format("{0:#,##0}", amount);
                }

                bodyBuilder.HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #333333; line-height: 1.6; background-color: #f4f6fa; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; background-color: #ffffff; margin: 0 auto; border-radius: 8px; padding: 30px; box-shadow: 0 4px 10px rgba(0,0,0,0.05); border: 1px solid #e1e4e8; }}
        .header {{ text-align: center; border-bottom: 2px solid #d9001b; padding-bottom: 15px; margin-bottom: 25px; }}
        .header h2 {{ color: #d9001b; margin: 0; font-size: 24px; text-transform: uppercase; }}
        .header p {{ color: #666666; margin: 5px 0 0 0; font-size: 14px; }}
        .info-table {{ width: 100%; border-collapse: collapse; margin-bottom: 25px; }}
        .info-table td {{ padding: 8px 0; font-size: 15px; }}
        .info-table td.label {{ font-weight: bold; color: #555555; width: 35%; }}
        .info-table td.value {{ color: #111111; }}
        .alert-box {{ background-color: #fff9db; border-left: 4px solid #f59f00; color: #664d03; padding: 15px; border-radius: 4px; font-size: 15px; margin-bottom: 25px; }}
        .signature-block {{ text-align: left; margin-top: 30px; margin-bottom: 20px; font-size: 14px; color: #444444; border-top: 1px solid #e1e4e8; padding-top: 20px; }}
        .signature-title {{ font-size: 13px; color: #666666; font-style: italic; margin-bottom: 5px; }}
        .signature-name {{ font-weight: bold; color: #d9001b; font-size: 16px; margin: 0 0 10px 0; }}
        .signature-info {{ font-size: 13px; color: #555555; line-height: 1.5; }}
        .signature-info div {{ margin-bottom: 4px; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #999999; border-top: 1px dashed #eeeeee; padding-top: 15px; }}

        @media (prefers-color-scheme: dark) {{
            body {{ background-color: #1a1f2c !important; color: #f4f6fa !important; }}
            .container {{ background-color: #232936 !important; border-color: #384252 !important; box-shadow: 0 4px 10px rgba(0,0,0,0.2) !important; }}
            .header {{ border-bottom-color: #ff4d61 !important; }}
            .header h2 {{ color: #ff4d61 !important; }}
            .header p {{ color: #a0aebf !important; }}
            .info-table td.label {{ color: #a0aebf !important; }}
            .info-table td.value {{ color: #ffffff !important; }}
            .alert-box {{ background-color: #2b2515 !important; border-left-color: #f59f00 !important; color: #ffe066 !important; }}
            .signature-block {{ border-top-color: #384252 !important; color: #e1e8f2 !important; }}
            .signature-title {{ color: #a0aebf !important; }}
            .signature-name {{ color: #ff4d61 !important; }}
            .signature-info {{ color: #a0aebf !important; }}
            .signature-info a {{ color: #ff4d61 !important; }}
            .footer {{ color: #707d90 !important; border-top-color: #384252 !important; }}
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Cảnh Báo Hết Hạn Hợp Đồng</h2>
            <p>{alertData.TenChiNhanh}</p>
        </div>

        <div class='alert-box'>
            Hợp đồng thuê phòng của <strong>{alertData.TenNguoiThue}</strong> tại phòng <strong>{alertData.SoPhong}</strong> sẽ hết hạn trong vòng <strong>{alertData.SoNgayConLai}</strong> ngày tới.
        </div>
        
        <table class='info-table'>
            <tr><td class='label'>Mã hợp đồng:</td><td class='value'><strong>{alertData.MaHopDong}</strong></td></tr>
            <tr><td class='label'>Phòng:</td><td class='value' style='color: #d9001b; font-weight: bold;'>{alertData.SoPhong}</td></tr>
            <tr><td class='label'>Người thuê:</td><td class='value'>{alertData.TenNguoiThue}</td></tr>
            <tr><td class='label'>Ngày hết hạn:</td><td class='value'>{alertData.ThoiDiemKetThuc.AddHours(7).ToString("dd/MM/yyyy")}</td></tr>
            <tr><td class='label'>Số ngày còn lại:</td><td class='value' style='color: #f59f00; font-weight: bold;'>{alertData.SoNgayConLai} ngày</td></tr>
            <tr><td class='label'>Giá thuê phòng:</td><td class='value'>{FormatVND(alertData.TienThuePhong)} đ/tháng</td></tr>
        </table>

        <div style='font-size: 15px; margin-top: 15px;'>
            Quý khách/Quý ban quản lý vui lòng thực hiện liên hệ để gia hạn hợp đồng hoặc chuẩn bị các thủ tục bàn giao phòng theo quy định.
        </div>
        
        <div class='signature-block'>
            <div class='signature-title'>Trân trọng,</div>
            <div class='signature-name'>Ban Quản Lý - {alertData.TenChiNhanh}</div>
            <div class='signature-info'>
                <div>📍 <strong>Địa chỉ:</strong> {alertData.DiaChiChiNhanh}</div>
                <div>📞 <strong>Hotline hỗ trợ:</strong> <a href='tel:{alertData.SoDienThoaiChiNhanh}' style='color: #d9001b; text-decoration: none; font-weight: bold;'>{alertData.SoDienThoaiChiNhanh}</a></div>
            </div>
        </div>
        
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống quản lý nhà trọ.</p>
            <p>Vui lòng không phản hồi trực tiếp email này. Xin cảm ơn!</p>
        </div>
    </div>
</body>
</html>";

                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(senderEmail, password);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi hệ thống khi gửi email cảnh báo hết hạn hợp đồng {MaHopDong} đến {Email}", alertData.MaHopDong, toEmail);
                return (false, "Lỗi hệ thống khi gửi email cảnh báo.");
            }
        }
    }
}
