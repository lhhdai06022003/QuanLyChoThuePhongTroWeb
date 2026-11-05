using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    // Kiểm quyền chi nhánh dùng chung cho thanh lý, gia hạn và tra cứu hợp đồng (fail closed).
    internal static class HopDongAccessGuard
    {
        public const string KhongCoQuyen = "Bạn không có quyền thao tác hợp đồng thuộc chi nhánh này.";
        public const string KhongTimThayHopDong = "Không tìm thấy hợp đồng.";

        // Trả null khi được phép. writeAction = null nghĩa là chỉ cần quyền đọc theo chi nhánh.
        public static async Task<ServiceResult?> CheckAsync(
            IEmployeeAccessService access, int actorId, int chiNhanhId, string? writeAction, CancellationToken ct)
        {
            if (actorId <= 0)
            {
                return ServiceResult.Fail("Người thực hiện không hợp lệ.");
            }

            var scope = await access.GetScopeAsync(actorId, ct);
            if (scope == null || !scope.CanAccessBranch(chiNhanhId))
            {
                return ServiceResult.Forbidden(KhongCoQuyen);
            }

            if (writeAction != null && !await access.CanPerformAsync(actorId, chiNhanhId, writeAction, ct))
            {
                return ServiceResult.Forbidden(KhongCoQuyen);
            }

            return null;
        }

        public static ServiceResult<T> As<T>(ServiceResult error)
        {
            return new ServiceResult<T> { Success = false, Message = error.Message, ErrorKind = error.ErrorKind };
        }
    }
}
