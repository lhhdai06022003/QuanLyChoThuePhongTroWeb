using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence
{
    // Kiểm người thuê có nhìn thấy được với các chi nhánh của nhân viên hay không (cùng quy tắc với
    // INguoiThueStore.IsVisibleAsync). Hợp đồng và thành viên hợp đồng dùng để không gắn được người thuê
    // của chi nhánh khác vào hợp đồng mình quản lý.
    public interface ITenantVisibilityStore
    {
        // true khi mọi id đều là người thuê còn tồn tại và nhìn thấy được; id không tồn tại tính là không nhìn thấy.
        Task<bool> AreAllVisibleAsync(IReadOnlyCollection<int> nguoiThueIds, IReadOnlyCollection<int> allowedBranchIds, CancellationToken cancellationToken = default);
    }
}
