using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeAutoInvoiceService : IAutoInvoiceService
    {
        public List<(int ActorId, AutoInvoiceTrigger Trigger, int PhongTroId, int Thang, int Nam)> Calls { get; } = new();

        public Task<ServiceResult<AutoInvoiceRunResult>> RunForRoomAsync(int actorId, AutoInvoiceTrigger trigger, int phongTroId, int thang, int nam)
        {
            Calls.Add((actorId, trigger, phongTroId, thang, nam));
            return Task.FromResult(ServiceResult<AutoInvoiceRunResult>.Ok(new AutoInvoiceRunResult
            {
                Outcome = AutoInvoiceOutcome.DungImLang
            }));
        }

        public async Task<IReadOnlyList<ServiceResult<AutoInvoiceRunResult>>> RunForRoomsAsync(int actorId, AutoInvoiceTrigger trigger, IReadOnlyList<int> phongTroIds, int thang, int nam)
        {
            var results = new List<ServiceResult<AutoInvoiceRunResult>>();
            foreach (var id in phongTroIds)
            {
                results.Add(await RunForRoomAsync(actorId, trigger, id, thang, nam));
            }

            return results;
        }
    }
}
