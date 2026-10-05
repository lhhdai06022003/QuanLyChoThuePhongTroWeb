using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Security
{
    public sealed class EmployeeAccessScope
    {
        public int ActorId { get; }
        public bool IsAdmin { get; }
        public IReadOnlySet<int> ActiveBranchIds { get; }

        public EmployeeAccessScope(int actorId, bool isAdmin, IEnumerable<int>? activeBranchIds = null)
        {
            ActorId = actorId;
            IsAdmin = isAdmin;
            ActiveBranchIds = activeBranchIds?.ToFrozenSet() ?? FrozenSet<int>.Empty;
        }

        // Danh sách chi nhánh để lọc truy vấn: null nghĩa là không giới hạn (Admin);
        // nhân viên chưa được phân công nhận danh sách rỗng nên không thấy dữ liệu nào.
        public IReadOnlyCollection<int>? AllowedBranchIds => IsAdmin ? null : ActiveBranchIds.ToList();

        public bool CanAccessBranch(int branchId)
        {
            if (IsAdmin)
            {
                return true;
            }

            return branchId > 0 && ActiveBranchIds.Contains(branchId);
        }
    }
}
