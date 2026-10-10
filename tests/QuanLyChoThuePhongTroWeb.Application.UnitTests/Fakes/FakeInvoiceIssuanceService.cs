using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeInvoiceIssuanceService : IInvoiceIssuanceService
    {
        public List<(CreateInvoiceDraftsRequest Request, int ActorId)> CreateCalls { get; } = new();
        public List<(int HoaDonId, int ActorId)> SubmitProvisionalCalls { get; } = new();
        public Func<CreateInvoiceDraftsRequest, int, ServiceResult<InvoiceDraftBatchResult>>? OnCreateDrafts { get; set; }
        public Func<int, int, ServiceResult<InvoiceTransitionResult>>? OnSubmitProvisional { get; set; }
        public DateTime HanPhanHoiUtc { get; set; } = new DateTime(2026, 10, 11, 3, 0, 0, DateTimeKind.Utc);
        public List<string>? Log { get; set; }

        public Task<ServiceResult<InvoiceDraftBatchResult>> CreateDraftsAsync(CreateInvoiceDraftsRequest request, int actorId, CancellationToken ct = default)
        {
            CreateCalls.Add((request, actorId));
            Log?.Add("CreateDrafts");
            if (OnCreateDrafts != null)
            {
                return Task.FromResult(OnCreateDrafts(request, actorId));
            }

            return Task.FromResult(ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
            {
                CreatedCount = 1,
                Items = new[]
                {
                    new InvoiceDraftItemResult
                    {
                        PhongTroId = request.PhongTroIds[0],
                        SoPhong = "101",
                        HopDongId = 10,
                        MaHopDong = "HD-01",
                        Status = InvoiceDraftItemStatus.Created,
                        HoaDonId = 500,
                        MaHoaDon = "HD-500"
                    }
                }
            }));
        }

        public Task<ServiceResult<InvoiceTransitionResult>> SubmitProvisionalAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            SubmitProvisionalCalls.Add((hoaDonId, actorId));
            Log?.Add("SubmitProvisional");
            if (OnSubmitProvisional != null)
            {
                return Task.FromResult(OnSubmitProvisional(hoaDonId, actorId));
            }

            return Task.FromResult(ServiceResult<InvoiceTransitionResult>.Ok(new InvoiceTransitionResult
            {
                HoaDonId = hoaDonId,
                MaHoaDon = "HD-500",
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet,
                HanPhanHoiUtc = HanPhanHoiUtc
            }));
        }

        public Task<ServiceResult<InvoiceTransitionResult>> SubmitAsync(int hoaDonId, int actorId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<InvoiceTransitionResult>> FinalizeAsync(int hoaDonId, int actorId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<InvoiceTransitionResult>> RejectAsync(int hoaDonId, string lyDo, int actorId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
