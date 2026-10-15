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
        public List<(CreateInvoiceDraftsRequest Request, int ActorId, string? LyDoKhoiTao)> CreateCalls { get; } = new();
        public Func<CreateInvoiceDraftsRequest, int, ServiceResult<InvoiceDraftBatchResult>>? OnCreateDrafts { get; set; }
        public List<string>? Log { get; set; }

        public Task<ServiceResult<InvoiceDraftBatchResult>> CreateDraftsAsync(CreateInvoiceDraftsRequest request, int actorId, string? lyDoKhoiTao = null, CancellationToken ct = default)
        {
            CreateCalls.Add((request, actorId, lyDoKhoiTao));
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

        public List<(CreateInvoiceDraftsRequest Request, int ActorId, string? LyDoKhoiTao)> CreateInCurrentTransactionCalls { get; } = new();
        public Func<CreateInvoiceDraftsRequest, int, ServiceResult<InvoiceDraftBatchResult>>? OnCreateDraftsInCurrentTransaction { get; set; }

        public Task<ServiceResult<InvoiceDraftBatchResult>> CreateDraftsInCurrentTransactionAsync(CreateInvoiceDraftsRequest request, int actorId, string? lyDoKhoiTao = null, CancellationToken ct = default)
        {
            CreateInCurrentTransactionCalls.Add((request, actorId, lyDoKhoiTao));
            Log?.Add("CreateDraftsInCurrentTransaction");
            if (OnCreateDraftsInCurrentTransaction != null)
            {
                return Task.FromResult(OnCreateDraftsInCurrentTransaction(request, actorId));
            }

            return CreateDraftsAsync(request, actorId, lyDoKhoiTao, ct);
        }

        public Task<ServiceResult<InvoiceTransitionResult>> SubmitAsync(int hoaDonId, int actorId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<InvoiceTransitionResult>> FinalizeAsync(int hoaDonId, int actorId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ServiceResult<InvoiceTransitionResult>> RejectAsync(int hoaDonId, string lyDo, int actorId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
