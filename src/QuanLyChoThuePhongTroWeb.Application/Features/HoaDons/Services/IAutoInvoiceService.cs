using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IAutoInvoiceService
    {
        // actorId là người làm thao tác gốc (nhân viên/Admin, hoặc tài khoản khách khi trigger = KhachTaiAnh).
        // Luôn trả Success = true với kết quả trong Data, không ném exception.
        Task<ServiceResult<AutoInvoiceRunResult>> RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam);
    }
}
