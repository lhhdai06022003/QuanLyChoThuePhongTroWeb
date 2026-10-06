using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools
{
    public static class AiToolCatalog
    {
        private const string ThangMacDinh = "Tháng (1-12). Bỏ trống thì dùng tháng hiện tại.";
        private const string NamMacDinh = "Năm (2000-2100). Bỏ trống thì dùng năm hiện tại.";
        private const string ThangKy = "Tháng của kỳ hóa đơn (1-12). Bỏ trống cả tháng và năm thì lấy mọi kỳ.";
        private const string NamKy = "Năm của kỳ hóa đơn (2000-2100). Bỏ trống thì dùng năm hiện tại nếu có tháng; có năm mà không có tháng thì lấy cả năm.";

        private static readonly IReadOnlyList<AiToolDefinition> ManagerTools = new List<AiToolDefinition>
        {
            new(AiToolNames.PhongTroChuaChotDienNuoc, "Liệt kê các phòng đang cho thuê nhưng chưa chốt chỉ số điện nước của một tháng.",
                new[] { Int("thang", ThangMacDinh), Int("nam", NamMacDinh) }),
            new(AiToolNames.HoaDonChuaThanhToan, "Liệt kê hóa đơn đã gửi cho khách nhưng chưa thanh toán đủ, kèm số đã thu, số còn lại và tổng còn nợ.",
                new[] { Int("thang", ThangKy), Int("nam", NamKy) }),
            new(AiToolNames.DoanhThuThucThu, "Tính doanh thu thực thu (tổng tiền khách đã thanh toán) trong khoảng ngày theo giờ Việt Nam.",
                new[]
                {
                    Str("tuNgay", "Từ ngày, dạng YYYY-MM-DD (giờ Việt Nam). Bỏ trống thì dùng ngày 1 của tháng hiện tại."),
                    Str("denNgay", "Đến ngày (gồm cả ngày này), dạng YYYY-MM-DD (giờ Việt Nam). Bỏ trống thì dùng hôm nay."),
                }),
            new(AiToolNames.PhongTrong, "Liệt kê các phòng đang trống, có thể lọc theo mức giá thuê tối đa.",
                new[] { Num("mucGiaToiDa", "Mức giá thuê tối đa (VND). Bỏ trống thì không lọc giá.") }),
            new(AiToolNames.HopDongSapHetHan, "Liệt kê các hợp đồng đang hoạt động sắp hết hạn trong N ngày tới.",
                new[] { Int("soNgay", "Số ngày tới (1-365). Bỏ trống thì dùng 30 ngày.") }),
            new(AiToolNames.ThongTinKhachThue, "Tra cứu khách thuê theo tên hoặc số phòng.",
                new[] { Str("tuKhoa", "Tên khách thuê hoặc số phòng cần tra cứu.", true) }),
            new(AiToolNames.CongNoPhong, "Tính công nợ (hóa đơn đã gửi chưa thanh toán đủ) của một phòng theo số phòng. Nếu nhiều chi nhánh có phòng cùng số thì cần hỏi người dùng chọn chi nhánh.",
                new[] { Str("soPhong", "Số phòng.", true), Str("tenChiNhanh", "Tên chi nhánh (hoặc một phần tên) nếu người dùng đã nói.") }),
            new(AiToolNames.DoanhThuChiNhanh, "Tính doanh thu theo từng chi nhánh của các hóa đơn thuộc kỳ tháng/năm.",
                new[] { Int("thang", ThangMacDinh), Int("nam", NamMacDinh) }),
            new(AiToolNames.ChiSoDienNuoc, "Xem chỉ số điện nước của một phòng trong một tháng. Nếu nhiều chi nhánh có phòng cùng số thì cần hỏi người dùng chọn chi nhánh.",
                new[]
                {
                    Str("soPhong", "Số phòng.", true),
                    Str("tenChiNhanh", "Tên chi nhánh (hoặc một phần tên) nếu người dùng đã nói."),
                    Int("thang", ThangMacDinh),
                    Int("nam", NamMacDinh),
                }),
        };

        private static readonly IReadOnlyList<AiToolDefinition> TenantTools = new List<AiToolDefinition>
        {
            new(AiToolNames.MyHopDongInfo, "Xem thông tin hợp đồng đang hoạt động của chính khách thuê (phòng, thời hạn, giá thuê, tiền cọc).",
                Array.Empty<AiToolParameter>()),
            new(AiToolNames.MyChiSoDienNuoc, "Xem chỉ số điện nước của phòng mà khách thuê đang thuê trong một tháng.",
                new[] { Int("thang", ThangMacDinh), Int("nam", NamMacDinh) }),
            new(AiToolNames.MyHoaDonChuaThanhToan, "Liệt kê hóa đơn chưa thanh toán đủ của chính khách thuê, kèm số đã thanh toán, số còn lại và tổng còn nợ.",
                new[] { Int("thang", ThangKy), Int("nam", NamKy) }),
        };

        public static IReadOnlyList<AiToolDefinition> ForRole(AiCallerRole role)
            => role == AiCallerRole.KhachThue ? TenantTools : ManagerTools;

        public static bool IsAllowed(AiCallerRole role, string? toolName)
            => !string.IsNullOrEmpty(toolName) && ForRole(role).Any(t => t.Name == toolName);

        private static AiToolParameter Int(string name, string description, bool required = false)
            => new(name, AiToolParameterType.Integer, description, required);

        private static AiToolParameter Num(string name, string description, bool required = false)
            => new(name, AiToolParameterType.Number, description, required);

        private static AiToolParameter Str(string name, string description, bool required = false)
            => new(name, AiToolParameterType.String, description, required);
    }
}
