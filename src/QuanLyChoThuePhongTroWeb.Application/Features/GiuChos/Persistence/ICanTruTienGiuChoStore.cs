using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

public interface ICanTruTienGiuChoStore
{
    Task PrepareInvoicesAsync(IReadOnlyList<HoaDon> invoices);
    Task<bool> HasAllocationAsync(int invoiceId);
    Task<decimal> GetAvailableAsync(int contractId, int month, int year);
}
