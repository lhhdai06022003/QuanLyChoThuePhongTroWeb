using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    // Chi tiết người thuê với tab Lịch sử thuê, và API nợ cũ để cảnh báo khi lập hợp đồng. Phạm vi chi nhánh do Application kiểm.
    public class NguoiThueChiTietController : AdminBaseController
    {
        private readonly IHopDongTraCuuService _traCuu;

        public NguoiThueChiTietController(IHopDongTraCuuService traCuu)
        {
            _traCuu = traCuu;
        }

        [HttpGet("/QuanLyNhaTro/NguoiThue/ChiTiet/{id:int}")]
        public async Task<IActionResult> ChiTiet(int id, CancellationToken ct)
        {
            var result = await _traCuu.GetLichSuThueAsync(CurrentActorId, id, ct);
            if (!result.Success)
            {
                var view = View("~/Areas/QuanLyNhaTro/Views/HopDongChiTiet/Loi.cshtml",
                    HopDongLoiViewModel.From(result, "/QuanLyNhaTro/QuanLyNguoiThue", "Về danh sách người thuê"));
                view.StatusCode = result.ToHttpStatusCode();
                return view;
            }

            return View("ChiTiet", result.Data!);
        }

        [HttpGet("/NguoiThue/NoCu/{id:int}")]
        public async Task<IActionResult> NoCu(int id, CancellationToken ct)
        {
            var result = await _traCuu.GetNoCuAsync(CurrentActorId, id, ct);
            return result.Success ? Ok(result.Data) : this.ToErrorResult(result);
        }
    }
}
