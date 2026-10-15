using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public sealed class CreateInvoiceDraftsRequest
    {
        public int ChiNhanhId { get; init; }
        public int Thang { get; init; }
        public int Nam { get; init; }
        public IReadOnlyList<int> PhongTroIds { get; init; } = Array.Empty<int>();
    }

    public enum InvoiceDraftItemStatus
    {
        Created = 1,
        SkippedHasActiveInvoice,
        SkippedMeterNotApproved,
        SkippedNoMeterReading,
        SkippedNoOccupancy,
        Invalid
    }

    public sealed class InvoiceDraftItemResult
    {
        public int PhongTroId { get; init; }
        public string SoPhong { get; init; } = string.Empty;
        public int HopDongId { get; init; }
        public string MaHopDong { get; init; } = string.Empty;
        public InvoiceDraftItemStatus Status { get; init; }
        public int? HoaDonId { get; init; }
        public string? MaHoaDon { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public sealed class InvoiceDraftBatchResult
    {
        public IReadOnlyList<InvoiceDraftItemResult> Items { get; init; } = Array.Empty<InvoiceDraftItemResult>();
        public int CreatedCount { get; init; }
    }

    public sealed class InvoiceTransitionResult
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public TrangThaiPhatHanhHoaDon TrangThaiPhatHanh { get; init; }
    }
}
