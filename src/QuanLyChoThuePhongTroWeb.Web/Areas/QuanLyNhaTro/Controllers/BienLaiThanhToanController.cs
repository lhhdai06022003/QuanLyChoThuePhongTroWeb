using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Models.ThanhToan;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    // Biên lai thanh toán phía quản lý (spec thanh toán trực tuyến §3.4). Quyền xem hóa đơn theo chi nhánh do Application kiểm.
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
                return this.ToErrorResult(result);
            }

            var receipt = result.Data!;
            return View(BienLaiThanhToanViewModel.From(receipt, $"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={receipt.HoaDonId}"));
        }
    }
}
