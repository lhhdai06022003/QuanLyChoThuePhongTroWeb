using System;
using System.Collections.Frozen;
using System.Collections.Generic;

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
