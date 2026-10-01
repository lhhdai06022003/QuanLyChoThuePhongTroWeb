using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public sealed class PublishInvoicesRequest
    {
        public IReadOnlyList<int> HoaDonIds { get; init; } = Array.Empty<int>();
    }

    public enum InvoicePublishOutcome
    {
        Published = 1,
        AlreadyPublished,
        NotFound,
        Forbidden,
        Cancelled,
        InvalidState,
        Error
    }

    public enum InvoicePortalNotificationOutcome
    {
        NotApplicable = 0,
        Created,
        NoPortalAccount
    }

    public enum InvoiceEmailOutcome
    {
        NotAttempted = 0,
        Sent,
        Failed,
        NoEmail
    }

    public sealed class InvoicePublishItemResult
    {
        public int HoaDonId { get; init; }
        public string? MaHoaDon { get; init; }
        public InvoicePublishOutcome Publish { get; init; }
        public string PublishMessage { get; init; } = string.Empty;
        public InvoicePortalNotificationOutcome PortalNotification { get; init; }
        public InvoiceEmailOutcome Email { get; init; }
        public string? EmailMessage { get; init; }
        public string? RecipientEmail { get; init; }
        public DateTime? HanThanhToanUtc { get; init; }
    }

    public sealed class InvoicePublishBatchResult
    {
        public IReadOnlyList<InvoicePublishItemResult> Items { get; init; } = Array.Empty<InvoicePublishItemResult>();
        public int PublishedCount { get; init; }
    }

    public sealed class InvoiceEmailResendResult
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public InvoiceEmailOutcome Email { get; init; }
        public string EmailMessage { get; init; } = string.Empty;
        public string? RecipientEmail { get; init; }
    }
}
