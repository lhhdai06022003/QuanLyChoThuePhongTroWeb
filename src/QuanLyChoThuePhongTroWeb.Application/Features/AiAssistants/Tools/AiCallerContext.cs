using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools
{
    public enum AiCallerRole { Admin, NhanVien, KhachThue }

    // AllowedBranchIds: null = không giới hạn (chỉ Admin); NguoiThueId chỉ có với khách thuê.
    public sealed record AiCallerContext(int NguoiDungId, AiCallerRole Role, IReadOnlyCollection<int>? AllowedBranchIds, int? NguoiThueId);
}
