using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Security
{
    public static class EmployeeActionCodes
    {
        public const string MeterUpload = "Meter.Upload";
        public const string MeterReview = "Meter.Review";
        public const string MeterRetryOcr = "Meter.RetryOcr";
        public const string MeterRead = "Meter.Read";
        public const string InvoiceDraft = "Invoice.Draft";
        public const string InvoiceSubmit = "Invoice.Submit";
        public const string InvoiceFinalize = "Invoice.Finalize";
        public const string InvoiceReject = "Invoice.Reject";
        public const string InvoiceSend = "Invoice.Send";
        public const string InvoiceCancel = "Invoice.Cancel";
        public const string InvoiceResendEmail = "Invoice.ResendEmail";
        public const string InvoiceRead = "Invoice.Read";

        private static readonly FrozenSet<string> ValidActionsSet = new[]
        {
            MeterUpload, MeterReview, MeterRetryOcr, MeterRead,
            InvoiceDraft, InvoiceSubmit, InvoiceFinalize, InvoiceReject,
            InvoiceSend, InvoiceCancel, InvoiceResendEmail, InvoiceRead
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        private static readonly FrozenSet<string> AdminOnlySet = new[]
        {
            InvoiceFinalize, InvoiceReject
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlySet<string> AllValidActions => ValidActionsSet;
        public static IReadOnlySet<string> AdminOnlyActions => AdminOnlySet;

        public static bool IsValid(string? action)
        {
            return !string.IsNullOrWhiteSpace(action) && ValidActionsSet.Contains(action);
        }

        public static bool IsAdminOnly(string? action)
        {
            return !string.IsNullOrWhiteSpace(action) && AdminOnlySet.Contains(action);
        }
    }
}
