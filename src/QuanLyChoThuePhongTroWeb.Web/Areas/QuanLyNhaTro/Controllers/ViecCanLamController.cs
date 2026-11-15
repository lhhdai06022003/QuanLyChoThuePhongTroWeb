using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    public class ViecCanLamController : AdminBaseController
    {
        private readonly IViecCanLamService _service;
        private readonly IPhongTroService _phongTroService;

        public ViecCanLamController(IViecCanLamService service, IPhongTroService phongTroService)
        {
            _service = service;
            _phongTroService = phongTroService;
        }

        [HttpGet("/QuanLyNhaTro/ViecCanLam")]
        public async Task<IActionResult> Index(int? chiNhanhId, CancellationToken cancellationToken)
        {
            if (chiNhanhId <= 0) chiNhanhId = null;
            var tongHop = await _service.GetTongHopAsync(CurrentActorId, chiNhanhId, cancellationToken);
            // Tổng số hóa đơn quá hạn đã có trong mục TT2 của cùng lần tổng hợp: bảng quá hạn không đếm lại.
            var tongQuaHan = tongHop.Items.Where(i => i.MaViec == "TT2").Sum(i => i.SoLuong);
            var quaHan = await _service.GetHoaDonQuaHanAsync(CurrentActorId, chiNhanhId, tongQuaHan, cancellationToken);
            var chiNhanhs = await _phongTroService.GetDanhSachChiNhanhDropdownAsync(CurrentActorId);
            return View(ViecCanLamPageViewModel.From(tongHop, quaHan, chiNhanhs, chiNhanhId));
        }

        [HttpGet("/QuanLyNhaTro/ViecCanLam/TomTat")]
        public async Task<IActionResult> TomTat(CancellationToken cancellationToken)
        {
            var t = await _service.GetTongHopAsync(CurrentActorId, null, cancellationToken);
            return Json(new { tongSo = t.TongSo, soKhan = t.SoKhan, soCanLam = t.SoCanLam, soTheoDoi = t.SoTheoDoi });
        }
    }
}
