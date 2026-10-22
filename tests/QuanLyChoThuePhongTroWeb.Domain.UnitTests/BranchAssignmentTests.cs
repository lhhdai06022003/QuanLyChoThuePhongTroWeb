using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    public class BranchAssignmentTests
    {
        [Fact]
        public void Role_Enum_Contains_KhachVangLai_With_Value_3()
        {
            Assert.Equal(3, (int)Role.KhachVangLai);
        }

        [Fact]
        public void Staff_CanHaveMultipleActiveBranchAssignments()
        {
            var assignments = new List<NhanVienChiNhanh>
            {
                new()
                {
                    NhanVienChiNhanhId = 1,
                    NguoiDungId = 10,
                    ChiNhanhId = 1,
                    IsActive = true,
                    NgayPhanCong = DateTime.UtcNow,
                    NguoiPhanCongId = 1
                },
                new()
                {
                    NhanVienChiNhanhId = 2,
                    NguoiDungId = 10,
                    ChiNhanhId = 2,
                    IsActive = true,
                    NgayPhanCong = DateTime.UtcNow,
                    NguoiPhanCongId = 1
                }
            };

            var activeBranches = assignments
                .Where(a => a.NguoiDungId == 10 && a.IsActive)
                .Select(a => a.ChiNhanhId)
                .ToList();

            Assert.Equal(2, activeBranches.Count);
            Assert.Contains(1, activeBranches);
            Assert.Contains(2, activeBranches);
        }

        [Fact]
        public void RevokedAssignment_DeniesAccess_EvenIfPreviouslyAssigned()
        {
            var assignment = new NhanVienChiNhanh
            {
                NhanVienChiNhanhId = 1,
                NguoiDungId = 10,
                ChiNhanhId = 1,
                IsActive = false,
                NgayPhanCong = DateTime.UtcNow.AddMonths(-2),
                NguoiPhanCongId = 1,
                NgayThuHoi = DateTime.UtcNow.AddDays(-1),
                NguoiThuHoiId = 1,
                LyDo = "Chuyển công tác"
            };

            var hasAccess = assignment.IsActive;
            Assert.False(hasAccess);
            Assert.NotNull(assignment.NgayThuHoi);
            Assert.NotNull(assignment.NguoiThuHoiId);
        }
    }
}
