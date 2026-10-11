using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IAutoInvoiceService
    {
        // actorId là người làm thao tác gốc (nhân viên/Admin, hoặc tài khoản khách khi trigger = KhachTaiAnh).
        // hoanGuiEmail = true: không gửi email tạm tính, trả email chờ gửi trong AutoInvoiceRunResult.EmailChoGui để caller gửi nền.
        // Luôn trả Success = true với kết quả trong Data, không ném exception.
        Task<ServiceResult<AutoInvoiceRunResult>> RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam, bool hoanGuiEmail = false);
    }
}
