using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IProvisionalInvoiceEmailDispatcher
    {
        // Trả về ngay. Gửi ở nền trong DI scope riêng; không dùng DbContext/scoped service của request. Không ném.
        void Enqueue(IReadOnlyList<PendingProvisionalEmail> emails);
    }
}
