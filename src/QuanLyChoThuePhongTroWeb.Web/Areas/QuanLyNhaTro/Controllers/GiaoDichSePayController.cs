using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    // Màn "Giao dịch SePay" (spec thanh toán trực tuyến §4.3, §7.4): đồng bộ bù giao dịch tiền vào khi webhook bị lỡ.
    // Chỉ Admin; Application kiểm lại (fail closed) và không gọi API SePay nếu không phải Admin.
    [Authorize(Roles = "Admin")]
    public class GiaoDichSePayController : AdminBaseController
    {
        private const string DateFormat = "yyyy-MM-dd";

        private readonly ISePaySyncService _sync;

        public GiaoDichSePayController(ISePaySyncService sync)
        {
            _sync = sync;
        }

        // Ngày trên form là giờ Việt Nam.
        public class DongBoSePayReq
        {
            public string? TuNgay { get; set; }
            public string? DenNgay { get; set; }
        }

        [HttpGet("/QuanLyNhaTro/GiaoDichSePay")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var settings = await _sync.GetSettingsAsync(CurrentActorId, ct);
            if (!settings.Success)
            {
                return this.ToErrorResult(settings);
            }

            var s = settings.Data!;
            var now = DateTime.UtcNow;
            return View(new GiaoDichSePayViewModel(
                s.DaCauHinhApiToken,
                VietnamTime.Format(now.AddDays(-(s.SoNgayMacDinh - 1)), DateFormat),
                VietnamTime.Format(now, DateFormat),
                s.SoNgayToiDa,
                s.ChiDoiChieu));
        }

        [HttpPost("/QuanLyNhaTro/GiaoDichSePay/DongBo")]
        public async Task<IActionResult> DongBo([FromBody] DongBoSePayReq? req, CancellationToken ct)
        {
            if (!VietnamTime.TryParseToUtc(req?.TuNgay, out var tuUtc) ||
                !VietnamTime.TryParseToUtc(req?.DenNgay, out var denDauNgayUtc))
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn ngày hợp lệ." });
            }

            // Hai đầu đều tính: đến hết giây cuối cùng của ngày kết thúc (giờ Việt Nam).
            var denUtc = denDauNgayUtc.AddDays(1).AddSeconds(-1);
            var result = await _sync.SyncAsync(CurrentActorId, new SePaySyncRequest(tuUtc, denUtc), ct);
            if (!result.Success)
            {
                return this.ToErrorResult(result);
            }

            var data = result.Data!;
            // Không trả id của API SePay và không trả cấu hình nào.
            return Ok(new
            {
                success = true,
                data = new
                {
                    hoanTat = data.HoanTat,
                    loiDongBo = data.LoiDongBo,
                    canhBao = data.CanhBao,
                    chiDoiChieu = data.ChiDoiChieu,
                    tongTheoTrangThai = data.TongTheoTrangThai.Select(c => new
                    {
                        trangThai = c.TrangThai.ToString(),
                        nhan = c.Nhan,
                        soLuong = c.SoLuong
                    }),
                    rows = data.Rows.Select(r => new
                    {
                        thoiGian = VietnamTime.Format(r.NgayGiaoDichUtc, "dd/MM/yyyy HH:mm:ss"),
                        soTien = r.SoTien,
                        noiDung = r.NoiDung,
                        maThamChieu = r.MaThamChieu,
                        maChuyenKhoan = r.MaChuyenKhoan,
                        hoaDonId = r.HoaDonId,
                        maHoaDon = r.MaHoaDon,
                        trangThai = r.TrangThai.ToString(),
                        nhanTrangThai = r.NhanTrangThai,
                        ghiChu = r.GhiChu
                    })
                }
            });
        }
    }
}
