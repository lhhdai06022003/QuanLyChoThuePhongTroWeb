using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeAutoInvoiceService : IAutoInvoiceService
    {
        public List<(int ActorId, AutoInvoiceTrigger Trigger, int PhongTroId, int Thang, int Nam)> Calls { get; } = new();

        // Giá trị hoanGuiEmail của từng lần gọi, cùng thứ tự với Calls.
        public List<bool> HoanGuiEmailCalls { get; } = new();
        // Email chờ gửi trả về theo phòng khi hoanGuiEmail = true.
        public Dictionary<int, PendingProvisionalEmail> EmailChoGuiTheoPhong { get; } = new();

        public Task<ServiceResult<AutoInvoiceRunResult>> RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam, bool hoanGuiEmail = false)
        {
            Calls.Add((actorId, trigger, phongTroId, thang, nam));
            HoanGuiEmailCalls.Add(hoanGuiEmail);
            PendingProvisionalEmail? pending = null;
            if (hoanGuiEmail)
            {
                EmailChoGuiTheoPhong.TryGetValue(phongTroId, out pending);
            }

            return Task.FromResult(ServiceResult<AutoInvoiceRunResult>.Ok(new AutoInvoiceRunResult
            {
                Outcome = AutoInvoiceOutcome.DungImLang,
                EmailChoGui = pending
            }));
        }
    }
}
