using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    // Trang chi tiết, thanh lý, gia hạn hợp đồng và biên bản thanh lý. Quyền theo chi nhánh do Application kiểm.
    public class HopDongChiTietController : AdminBaseController
    {
        private const string DuLieuKhongHopLe = "Dữ liệu gửi lên không hợp lệ.";

        private readonly IThanhLyHopDongService _thanhLy;
        private readonly IGiaHanHopDongService _giaHan;
        private readonly IHopDongTraCuuService _traCuu;
        private readonly IDieuKhoanMauService _dieuKhoanMau;

        public HopDongChiTietController(
            IThanhLyHopDongService thanhLy,
            IGiaHanHopDongService giaHan,
            IHopDongTraCuuService traCuu,
            IDieuKhoanMauService dieuKhoanMau)
        {
            _thanhLy = thanhLy;
            _giaHan = giaHan;
            _traCuu = traCuu;
            _dieuKhoanMau = dieuKhoanMau;
        }

        [HttpGet("/QuanLyNhaTro/HopDong/ChiTiet/{id:int}")]
        public async Task<IActionResult> ChiTiet(int id, CancellationToken ct)
        {
            var result = await _traCuu.GetChiTietAsync(CurrentActorId, id, ct);
            return result.Success ? View("ChiTiet", result.Data!) : LoiView(result);
        }

        [HttpGet("/QuanLyNhaTro/HopDong/ThanhLy/{id:int}")]
        public async Task<IActionResult> ThanhLy(int id, CancellationToken ct)
        {
            var result = await _thanhLy.GetPreviewAsync(CurrentActorId, id, ct);
            return result.Success ? View("ThanhLy", result.Data!) : LoiView(result);
        }

        [HttpPost("/HopDong/ThanhLy/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanThanhLy(int id, [FromBody] ThanhLyHopDongBody? body, CancellationToken ct)
        {
            if (body == null)
            {
                return BadRequest(new { success = false, message = DuLieuKhongHopLe });
            }

            var result = await _thanhLy.ThanhLyAsync(CurrentActorId, new ThanhLyHopDongRequest
            {
                HopDongId = id,
                TongNoLucMo = body.TongNoLucMo,
                XacNhanConNo = body.XacNhanConNo
            }, ct);

            return result.Success
                ? Ok(new { success = true, message = result.Message, data = result.Data })
                : this.ToErrorResult(result);
        }

        [HttpGet("/QuanLyNhaTro/HopDong/GiaHan/{id:int}")]
        public async Task<IActionResult> GiaHan(int id, CancellationToken ct)
        {
            var result = await _giaHan.GetFormAsync(CurrentActorId, id, ct);
            if (!result.Success)
            {
                return LoiView(result);
            }

            return View("GiaHan", new GiaHanHopDongViewModel(result.Data!, await _dieuKhoanMau.GetAllAsync()));
        }

        [HttpPost("/HopDong/GiaHan/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanGiaHan(int id, [FromBody] GiaHanHopDongBody? body, CancellationToken ct)
        {
            if (body == null)
            {
                return BadRequest(new { success = false, message = DuLieuKhongHopLe });
            }

            if (!DateOnly.TryParseExact(body.NgayKetThucMoi, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayKetThuc))
            {
                return BadRequest(new { success = false, message = "Ngày kết thúc mới không hợp lệ." });
            }

            var result = await _giaHan.GiaHanAsync(CurrentActorId, new GiaHanHopDongRequest
            {
                HopDongId = id,
                ThangBatDau = body.ThangBatDau,
                NamBatDau = body.NamBatDau,
                NgayKetThucMoi = ngayKetThuc,
                TienThue = body.TienThue,
                TienCoc = body.TienCoc,
                DieuKhoanMauIds = body.ChepDieuKhoanCu ? null : (body.DieuKhoanMauIds ?? new List<int>())
            }, ct);

            return result.Success
                ? Ok(new { success = true, message = result.Message, data = new { hopDongMoiId = result.Data!.HopDongMoiId, maHopDongMoi = result.Data.MaHopDongMoi } })
                : this.ToErrorResult(result);
        }

        [HttpPost("/HopDong/BienBanThanhLy/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BienBanLanDau(int id, [FromForm] string? ghiChu, CancellationToken ct)
        {
            var result = await _thanhLy.XuatBienBanAsync(CurrentActorId, id, ghiChu, ct);
            return result.Success
                ? File(result.Data!, "application/pdf", $"BienBanThanhLy_{id}.pdf")
                : this.ToErrorResult(result);
        }

        [HttpGet("/HopDong/BienBanThanhLy/{id:int}")]
        public async Task<IActionResult> BienBanInLai(int id, CancellationToken ct)
        {
            var result = await _thanhLy.XuatBienBanAsync(CurrentActorId, id, null, ct);
            return result.Success
                ? File(result.Data!, "application/pdf", $"BienBanThanhLy_{id}.pdf")
                : this.ToErrorResult(result);
        }

        private IActionResult LoiView(ServiceResult result)
        {
            // Trang mở thẳng từ trình duyệt nên trả trang lỗi thân thiện, mã HTTP vẫn theo loại lỗi.
            var view = View("Loi", HopDongLoiViewModel.From(result));
            view.StatusCode = result.ToHttpStatusCode();
            return view;
        }

        public class ThanhLyHopDongBody
        {
            public decimal TongNoLucMo { get; set; }
            public bool XacNhanConNo { get; set; }
        }

        public class GiaHanHopDongBody
        {
            public int ThangBatDau { get; set; }
            public int NamBatDau { get; set; }
            // "yyyy-MM-dd"
            public string NgayKetThucMoi { get; set; } = string.Empty;
            public decimal TienThue { get; set; }
            public decimal TienCoc { get; set; }
            public bool ChepDieuKhoanCu { get; set; }
            public List<int>? DieuKhoanMauIds { get; set; }
        }
    }
}
