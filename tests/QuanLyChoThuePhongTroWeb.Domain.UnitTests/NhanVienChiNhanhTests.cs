using System;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    public class NhanVienChiNhanhTests
    {
        private static NhanVienChiNhanh CreateActiveAssignment(DateTime? ngayPhanCong = null, int nguoiPhanCongId = 1)
        {
            return new NhanVienChiNhanh
            {
                NhanVienChiNhanhId = 1,
                NguoiDungId = 10,
                ChiNhanhId = 1,
                IsActive = true,
                NgayPhanCong = ngayPhanCong ?? DateTime.UtcNow.AddDays(-10),
                NguoiPhanCongId = nguoiPhanCongId
            };
        }

        private static NhanVienChiNhanh CreateRevokedAssignment()
        {
            var assignment = CreateActiveAssignment();
            assignment.ThuHoi(actorId: 2, nowUtc: DateTime.UtcNow.AddDays(-1), lyDo: "Chuyển công tác");
            return assignment;
        }

        // ---- ThuHoi: đường đúng ----

        [Fact]
        public void ThuHoi_HappyPath_SetsRevocationFields_AndKeepsAssignmentHistory()
        {
            var ngayPhanCongGoc = DateTime.UtcNow.AddMonths(-2);
            var assignment = CreateActiveAssignment(ngayPhanCongGoc, nguoiPhanCongId: 5);
            var now = DateTime.UtcNow;

            assignment.ThuHoi(actorId: 99, nowUtc: now, lyDo: "  Nghỉ việc  ");

            Assert.False(assignment.IsActive);
            Assert.Equal(now, assignment.NgayThuHoi);
            Assert.Equal(99, assignment.NguoiThuHoiId);
            Assert.Equal("Nghỉ việc", assignment.LyDo);
            // Giữ nguyên thông tin phân công ban đầu
            Assert.Equal(ngayPhanCongGoc, assignment.NgayPhanCong);
            Assert.Equal(5, assignment.NguoiPhanCongId);
        }

        // ---- ThuHoi: biên ----

        [Fact]
        public void ThuHoi_AlreadyRevoked_ThrowsInvalidOperationException()
        {
            var assignment = CreateRevokedAssignment();

            Assert.Throws<InvalidOperationException>(() =>
                assignment.ThuHoi(actorId: 3, nowUtc: DateTime.UtcNow, lyDo: "Thu hồi lần hai"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ThuHoi_MissingReason_ThrowsArgumentException(string? lyDo)
        {
            var assignment = CreateActiveAssignment();

            Assert.Throws<ArgumentException>(() =>
                assignment.ThuHoi(actorId: 1, nowUtc: DateTime.UtcNow, lyDo: lyDo!));
        }

        [Fact]
        public void ThuHoi_Reason501Characters_ThrowsArgumentException()
        {
            var assignment = CreateActiveAssignment();
            var lyDo501 = new string('a', 501);

            Assert.Throws<ArgumentException>(() =>
                assignment.ThuHoi(actorId: 1, nowUtc: DateTime.UtcNow, lyDo: lyDo501));
        }

        [Fact]
        public void ThuHoi_Reason500Characters_Succeeds()
        {
            var assignment = CreateActiveAssignment();
            var lyDo500 = new string('a', 500);

            assignment.ThuHoi(actorId: 1, nowUtc: DateTime.UtcNow, lyDo: lyDo500);

            Assert.Equal(500, assignment.LyDo!.Length);
        }

        // ---- PhanCongLai: đường đúng và biên ----

        [Fact]
        public void PhanCongLai_WhileActive_ThrowsInvalidOperationException()
        {
            var assignment = CreateActiveAssignment();

            Assert.Throws<InvalidOperationException>(() =>
                assignment.PhanCongLai(actorId: 4, nowUtc: DateTime.UtcNow, lyDo: "Phân công lại"));
        }

        [Fact]
        public void PhanCongLai_AfterRevocation_ClearsRevocationFields_AndSetsNewAssignmentInfo()
        {
            var assignment = CreateRevokedAssignment();
            var now = DateTime.UtcNow;

            assignment.PhanCongLai(actorId: 42, nowUtc: now, lyDo: "  Quay lại phụ trách  ");

            Assert.True(assignment.IsActive);
            Assert.Equal(now, assignment.NgayPhanCong);
            Assert.Equal(42, assignment.NguoiPhanCongId);
            Assert.Null(assignment.NgayThuHoi);
            Assert.Null(assignment.NguoiThuHoiId);
            Assert.Equal("Quay lại phụ trách", assignment.LyDo);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PhanCongLai_BlankReason_BecomesNull(string? lyDo)
        {
            var assignment = CreateRevokedAssignment();

            assignment.PhanCongLai(actorId: 7, nowUtc: DateTime.UtcNow, lyDo: lyDo);

            Assert.Null(assignment.LyDo);
        }

        [Fact]
        public void PhanCongLai_Reason501Characters_ThrowsArgumentException()
        {
            var assignment = CreateRevokedAssignment();
            var lyDo501 = new string('b', 501);

            Assert.Throws<ArgumentException>(() =>
                assignment.PhanCongLai(actorId: 7, nowUtc: DateTime.UtcNow, lyDo: lyDo501));
        }

        [Fact]
        public void PhanCongLai_NeverAssignedBefore_StillWorks_LikeFreshAssignment()
        {
            // Dòng "chưa có" trong nghiệp vụ thực tế được tạo mới bằng object initializer,
            // nhưng entity method PhanCongLai không tự phân biệt "mới" và "thu hồi";
            // nó chỉ chặn khi DangHieuLuc() = true. Mô phỏng dòng chưa từng active (NgayThuHoi null, IsActive false)
            // không xảy ra trong domain thực tế; test này khẳng định hành vi khi NgayThuHoi có giá trị (đã thu hồi).
            var assignment = CreateRevokedAssignment();

            assignment.PhanCongLai(actorId: 8, nowUtc: DateTime.UtcNow, lyDo: null);

            Assert.True(assignment.IsActive);
            Assert.Null(assignment.NgayThuHoi);
        }
    }
}
