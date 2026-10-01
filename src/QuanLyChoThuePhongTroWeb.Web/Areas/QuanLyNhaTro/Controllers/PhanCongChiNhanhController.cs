using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PhanCongChiNhanhController : AdminBaseController
    {
        private readonly IEmployeeBranchAssignmentService _employeeBranchAssignmentService;

        public PhanCongChiNhanhController(IEmployeeBranchAssignmentService employeeBranchAssignmentService)
        {
            _employeeBranchAssignmentService = employeeBranchAssignmentService;
        }

        [Route("QuanLyNhaTro/PhanCongChiNhanh")]
        public IActionResult Index(int? nguoiDungId)
        {
            ViewBag.OpenNguoiDungId = nguoiDungId > 0 ? nguoiDungId : null;
            return View();
        }

        [HttpGet]
        [Route("QuanLyNhaTro/PhanCongChiNhanh/DanhSach")]
        public async Task<IActionResult> DanhSach(int? chiNhanhId)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _employeeBranchAssignmentService.GetEmployeesAsync(chiNhanhId, actorId);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpGet]
        [Route("QuanLyNhaTro/PhanCongChiNhanh/ChiTiet/{nguoiDungId:int}")]
        public async Task<IActionResult> ChiTiet(int nguoiDungId)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _employeeBranchAssignmentService.GetEmployeeAsync(nguoiDungId, actorId);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpGet]
        [Route("QuanLyNhaTro/PhanCongChiNhanh/ChiNhanhs")]
        public async Task<IActionResult> ChiNhanhs()
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _employeeBranchAssignmentService.GetBranchOptionsAsync(actorId);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("QuanLyNhaTro/PhanCongChiNhanh/PhanCong")]
        public async Task<IActionResult> PhanCong([FromBody] AssignEmployeeBranchRequest req)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _employeeBranchAssignmentService.AssignAsync(req, actorId);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("QuanLyNhaTro/PhanCongChiNhanh/ThuHoi")]
        public async Task<IActionResult> ThuHoi([FromBody] RevokeEmployeeBranchRequest req)
        {
            if (!TryGetActorId(out var actorId))
                return Unauthorized(new { success = false, message = "Người dùng chưa đăng nhập hoặc phiên làm việc đã hết hạn." });

            var result = await _employeeBranchAssignmentService.RevokeAsync(req, actorId);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        private bool TryGetActorId(out int actorId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId) && actorId > 0;
        }
    }
}
