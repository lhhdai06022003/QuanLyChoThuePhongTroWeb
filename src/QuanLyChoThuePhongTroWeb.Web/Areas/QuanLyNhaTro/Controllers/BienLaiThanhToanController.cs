using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Models.ThanhToan;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    // Biên lai thanh toán phía quản lý (spec thanh toán trực tuyến §3.4) và gửi lại email biên lai (§7.2).
    // Quyền xem (InvoiceRead) và gửi (InvoiceSend) theo chi nhánh do Application kiểm.
    public class BienLaiThanhToanController : AdminBaseController
    {
        private readonly IInvoiceReceiptService _receipts;

        public BienLaiThanhToanController(IInvoiceReceiptService receipts)
        {
            _receipts = receipts;
        }

        [HttpGet("/QuanLyNhaTro/HoaDon/BienLai/{lichSuThanhToanId:int}")]
        public async Task<IActionResult> Index(int lichSuThanhToanId, CancellationToken ct)
        {
            var result = await _receipts.GetForEmployeeAsync(CurrentActorId, lichSuThanhToanId, ct);
            if (!result.Success)
            {
                // Trang mở thẳng từ trình duyệt nên trả trang lỗi thân thiện, mã HTTP vẫn theo loại lỗi.
                var errorView = View("Loi", BienLaiLoiViewModel.From(result, "/QuanLyNhaTro/QuanLyHoaDon", "Về danh sách hóa đơn"));
                errorView.StatusCode = result.ToHttpStatusCode();
                return errorView;
            }

            var receipt = result.Data!;
            return View(BienLaiThanhToanViewModel.From(receipt, $"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={receipt.HoaDonId}"));
        }

        [HttpPost("/QuanLyNhaTro/HoaDon/BienLai/{lichSuThanhToanId:int}/GuiLai")]
        public async Task<IActionResult> GuiLai(int lichSuThanhToanId, CancellationToken ct)
        {
            var result = await _receipts.ResendForEmployeeAsync(CurrentActorId, lichSuThanhToanId, ct);
            return result.Success
                ? Ok(new { success = true, message = result.Message })
                : this.ToErrorResult(result);
        }
    }
}
