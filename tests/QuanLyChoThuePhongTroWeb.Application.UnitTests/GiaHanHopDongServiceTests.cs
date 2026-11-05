using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class GiaHanHopDongServiceTests
    {
        private const int Staff = 7;
        private const int Branch = 1;
        private const int OtherBranch = 2;
        private const string RootCode = "HD-CN-2510-001";
        private static readonly DateTime EndOctVn = new(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc);

        private sealed class Kit
        {
            public FakeHopDongLifecycleStore Store { get; } = new();
            public RecordingUnitOfWork Uow { get; } = new();
            public FakeEmployeeAccessService Access { get; } = new();
            public FakeThongBaoService ThongBao { get; } = new();
            public GiaHanHopDongService Service { get; }
            public HopDong Contract { get; }
            public PhongTro Room { get; }

            public Kit(string code = RootCode, int id = 10, int[]? branches = null, bool grantRenew = true)
            {
                Access.ScopeToReturn = new EmployeeAccessScope(Staff, false, branches ?? new[] { Branch });
                if (grantRenew)
                {
                    Access.Permissions.Add((Staff, Branch, EmployeeActionCodes.ContractRenew));
                }

                var chiNhanh = new ChiNhanh { ChiNhanhId = Branch, TenChiNhanh = "CN Một" };
                Room = new PhongTro { PhongTroId = 5, ChiNhanhId = Branch, ChiNhanh = chiNhanh, SoPhong = "P101", TrangThai = TrangThaiPhong.DaThue };
                var nguoiThue = new NguoiThue { NguoiThueId = 3, HoVaTen = "Nguyễn Văn A" };
                Contract = new HopDong
                {
                    HopDongId = id,
                    MaHopDong = code,
                    PhongTroId = 5,
                    PhongTro = Room,
                    NguoiThueId = 3,
                    NguoiThue = nguoiThue,
                    ThoiDiemBatDau = new DateTime(2025, 10, 31, 17, 0, 0, DateTimeKind.Utc),
                    ThoiDiemKetThuc = EndOctVn,
                    TienThuePhong = 3_000_000m,
                    TienCocPhong = 3_000_000m,
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
                };
                Store.HopDongs.Add(Contract);
                Store.PhongTros.Add(Room);
                Store.Members.Add(new ChiTietThanhVienHopDong { ChiTietThanhVienHopDongId = 1, HopDongId = id, NguoiThueId = 3, NguoiThue = nguoiThue, NgayVao = Contract.ThoiDiemBatDau });
                Store.Terms.Add(new HopDongDieuKhoan { Id = 1, HopDongId = id, TieuDe = "Điều 1", NoiDung = "Nội dung 1", ThuTu = 1 });

                Service = new GiaHanHopDongService(Store, Uow, Access, ThongBao,
                    new FixedTimeProvider(new DateTimeOffset(Store.NowUtc)), NullLogger<GiaHanHopDongService>.Instance);
            }

            public GiaHanHopDongRequest Request(int thang = 11, int nam = 2026, DateOnly? end = null, decimal thue = 3_500_000m, decimal coc = 3_000_000m, List<int>? terms = null) => new()
            {
                HopDongId = Contract.HopDongId,
                ThangBatDau = thang,
                NamBatDau = nam,
                NgayKetThucMoi = end ?? new DateOnly(2027, 10, 31),
                TienThue = thue,
                TienCoc = coc,
                DieuKhoanMauIds = terms
            };
        }

        [Fact]
        public async Task Renew_FromNovember_SplitsAtVietnamMonthBoundary_AndCopiesMembersAndTerms()
        {
            var kit = new Kit();

            var result = await kit.Service.GiaHanAsync(Staff, kit.Request());

            Assert.True(result.Success, result.Message);
            Assert.Equal(RootCode + "-GH1", result.Data!.MaHopDongMoi);
            Assert.Equal(TrangThaiHopDong.DaKetThuc, kit.Contract.TrangThaiHopDong);
            Assert.Equal(new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc), kit.Contract.ThoiDiemKetThuc);
            var moi = kit.Store.HopDongs.Single(h => h.HopDongId == result.Data.HopDongMoiId);
            Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), moi.ThoiDiemBatDau);
            Assert.Equal(new DateTime(2027, 10, 31, 16, 59, 59, DateTimeKind.Utc), moi.ThoiDiemKetThuc);
            Assert.Equal(TrangThaiHopDong.DangHoatDong, moi.TrangThaiHopDong);
            Assert.Equal(3_500_000m, moi.TienThuePhong);
            Assert.Equal(5, moi.PhongTroId);
            Assert.Equal(TrangThaiPhong.DaThue, kit.Room.TrangThai);

            var oldMember = kit.Store.Members.Single(m => m.HopDongId == 10);
            var newMember = kit.Store.Members.Single(m => m.HopDongId == moi.HopDongId);
            Assert.Equal(kit.Contract.ThoiDiemKetThuc, oldMember.NgayChuyenDi);
            Assert.Equal(moi.ThoiDiemBatDau, newMember.NgayVao);
            Assert.Null(newMember.NgayChuyenDi);
            var term = Assert.Single(kit.Store.Terms.Where(t => t.HopDongId == moi.HopDongId));
            Assert.Equal("Điều 1", term.TieuDe);
            Assert.True(kit.Uow.Transactions.Single().Committed);
        }

        // Gia hạn sớm (bắt đầu 01/08/2026 trong khi hợp đồng cũ tới 31/10/2026): thành viên vào ngày 15/09/2026, sau mốc kết thúc mới
        // của hợp đồng cũ (31/07), không được có ngày chuyển đi sớm hơn ngày vào; ở hợp đồng mới giữ đúng ngày vào thật.
        [Fact]
        public async Task EarlyRenewal_MemberJoinedAfterNewStart_KeepsJoinDate_AndNeverMovesOutBeforeJoining()
        {
            var kit = new Kit();
            var vaoSau = new DateTime(2026, 9, 14, 17, 0, 0, DateTimeKind.Utc);
            kit.Store.Members.Add(new ChiTietThanhVienHopDong { ChiTietThanhVienHopDongId = 2, HopDongId = 10, NguoiThueId = 4, NguoiThue = new NguoiThue { NguoiThueId = 4, HoVaTen = "Trần B" }, NgayVao = vaoSau });

            var result = await kit.Service.GiaHanAsync(Staff, kit.Request(thang: 8, nam: 2026, end: new DateOnly(2027, 7, 31)));

            Assert.True(result.Success, result.Message);
            var oldEnd = new DateTime(2026, 7, 31, 16, 59, 59, DateTimeKind.Utc);
            var newStart = new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc);
            var moiId = result.Data!.HopDongMoiId;

            var cuDaiDien = kit.Store.Members.Single(m => m.HopDongId == 10 && m.NguoiThueId == 3);
            var moiDaiDien = kit.Store.Members.Single(m => m.HopDongId == moiId && m.NguoiThueId == 3);
            Assert.Equal(oldEnd, cuDaiDien.NgayChuyenDi);
            Assert.Equal(newStart, moiDaiDien.NgayVao);

            var cuVaoSau = kit.Store.Members.Single(m => m.HopDongId == 10 && m.NguoiThueId == 4);
            var moiVaoSau = kit.Store.Members.Single(m => m.HopDongId == moiId && m.NguoiThueId == 4);
            Assert.True(cuVaoSau.NgayChuyenDi >= cuVaoSau.NgayVao, $"NgayChuyenDi {cuVaoSau.NgayChuyenDi:o} < NgayVao {cuVaoSau.NgayVao:o}");
            Assert.Equal(vaoSau, cuVaoSau.NgayChuyenDi);
            Assert.Equal(vaoSau, moiVaoSau.NgayVao);
            Assert.Null(moiVaoSau.NgayChuyenDi);
        }

        [Fact]
        public async Task SecondRenewal_FromGh1_BecomesGh2_WithoutNestingRoot()
        {
            var kit = new Kit(code: RootCode + "-GH1");
            kit.Store.HopDongs.Add(new HopDong { HopDongId = 9, MaHopDong = RootCode, PhongTroId = 5, NguoiThueId = 3, TrangThaiHopDong = TrangThaiHopDong.DaKetThuc });

            var result = await kit.Service.GiaHanAsync(Staff, kit.Request());

            Assert.True(result.Success, result.Message);
            Assert.Equal(RootCode + "-GH2", result.Data!.MaHopDongMoi);
        }

        [Fact]
        public async Task DeletedGh1Code_IsSkipped()
        {
            var kit = new Kit();
            kit.Store.HopDongs.Add(new HopDong { HopDongId = 8, MaHopDong = RootCode + "-GH1", IsDeleted = true, TrangThaiHopDong = TrangThaiHopDong.DaHuy });

            var result = await kit.Service.GiaHanAsync(Staff, kit.Request());

            Assert.True(result.Success, result.Message);
            Assert.Equal(RootCode + "-GH2", result.Data!.MaHopDongMoi);
        }

        [Fact]
        public async Task InvoiceInNewMonth_Fails_AndNothingChanges()
        {
            var kit = new Kit();
            kit.Store.HoaDons.Add(new HoaDon { HoaDonId = 1, HopDongId = 10, Thang = 11, Nam = 2026, TongTien = 1m });

            var result = await kit.Service.GiaHanAsync(Staff, kit.Request());

            Assert.False(result.Success);
            Assert.Contains("T11/2026", result.Message);
            Assert.Equal(TrangThaiHopDong.DangHoatDong, kit.Contract.TrangThaiHopDong);
            Assert.Single(kit.Store.HopDongs);
            Assert.True(kit.Uow.Transactions.Single().RolledBack);
        }

        [Fact]
        public async Task StartNotAfterOldStart_Fails()
        {
            var kit = new Kit();

            var result = await kit.Service.GiaHanAsync(Staff, kit.Request(thang: 10, nam: 2025, end: new DateOnly(2026, 10, 31)));

            Assert.False(result.Success);
            Assert.Contains("sau ngày bắt đầu", result.Message);
        }

        [Fact]
        public async Task InvalidInput_Fails()
        {
            var kit = new Kit();

            Assert.False((await kit.Service.GiaHanAsync(Staff, kit.Request(thue: 0m))).Success);
            Assert.False((await kit.Service.GiaHanAsync(Staff, kit.Request(coc: -1m))).Success);
            Assert.False((await kit.Service.GiaHanAsync(Staff, kit.Request(end: new DateOnly(2026, 11, 1)))).Success);
            Assert.False((await kit.Service.GiaHanAsync(Staff, kit.Request(thang: 13))).Success);
            Assert.Empty(kit.Uow.Transactions);
        }

        [Fact]
        public async Task FractionalRentOrDeposit_Fails_AndNothingChanges()
        {
            var kit = new Kit();

            var thueLe = await kit.Service.GiaHanAsync(Staff, kit.Request(thue: 3_000_000.5m));
            var cocLe = await kit.Service.GiaHanAsync(Staff, kit.Request(coc: 2_000_000.25m));

            Assert.False(thueLe.Success);
            Assert.Contains("Giá thuê", thueLe.Message!);
            Assert.False(cocLe.Success);
            Assert.Contains("Tiền cọc", cocLe.Message!);
            Assert.Empty(kit.Uow.Transactions);
        }

        [Fact]
        public async Task TermTemplates_AreUsedWhenGiven_AndEmptyListMeansNoTerms()
        {
            var kit = new Kit();
            kit.Store.TermTemplates.Add(new DieuKhoanMau { DieuKhoanMauId = 5, TieuDe = "Mẫu 5", NoiDung = "x" });

            var withTemplate = await kit.Service.GiaHanAsync(Staff, kit.Request(terms: new List<int> { 5 }));
            Assert.True(withTemplate.Success, withTemplate.Message);
            Assert.Equal("Mẫu 5", Assert.Single(kit.Store.Terms.Where(t => t.HopDongId == withTemplate.Data!.HopDongMoiId)).TieuDe);

            var kit2 = new Kit();
            var none = await kit2.Service.GiaHanAsync(Staff, kit2.Request(terms: new List<int>()));
            Assert.True(none.Success, none.Message);
            Assert.DoesNotContain(kit2.Store.Terms, t => t.HopDongId == none.Data!.HopDongMoiId);
        }

        [Fact]
        public async Task Permissions_NullScope_OtherBranch_MissingAction()
        {
            var noScope = new Kit();
            noScope.Access.ScopeToReturn = null;
            Assert.Equal(ServiceErrorKind.Forbidden, (await noScope.Service.GiaHanAsync(Staff, noScope.Request())).ErrorKind);

            var other = new Kit(branches: new[] { OtherBranch });
            Assert.Equal(ServiceErrorKind.Forbidden, (await other.Service.GiaHanAsync(Staff, other.Request())).ErrorKind);
            Assert.Empty(other.Store.LockHopDongCalls);

            var missing = new Kit(grantRenew: false);
            Assert.Equal(ServiceErrorKind.Forbidden, (await missing.Service.GiaHanAsync(Staff, missing.Request())).ErrorKind);
            Assert.Empty(missing.Store.LockHopDongCalls);
        }

        [Fact]
        public async Task Notifications_OneForStaff_OneForTenant_FailureKeepsResult()
        {
            var kit = new Kit();
            Assert.True((await kit.Service.GiaHanAsync(Staff, kit.Request())).Success);
            Assert.Equal(new[] { "Phong", "Khach" }, kit.ThongBao.Sent.Select(s => s.Kind).ToArray());

            var throwing = new Kit();
            throwing.ThongBao.ThrowOnSend = true;
            Assert.True((await throwing.Service.GiaHanAsync(Staff, throwing.Request())).Success);
        }

        [Fact]
        public async Task GetForm_ReturnsDefaults_AndExpectedCode()
        {
            var kit = new Kit();
            var service = new GiaHanHopDongService(kit.Store, kit.Uow, kit.Access, kit.ThongBao,
                new FixedTimeProvider(new DateTimeOffset(2026, 10, 20, 3, 0, 0, TimeSpan.Zero)), NullLogger<GiaHanHopDongService>.Instance);

            var result = await service.GetFormAsync(Staff, 10);

            Assert.True(result.Success, result.Message);
            var form = result.Data!;
            Assert.Equal(RootCode + "-GH1", form.MaHopDongMoiDuKien);
            Assert.Equal(11, form.ThangBatDauMacDinh);
            Assert.Equal(2026, form.NamBatDauMacDinh);
            Assert.Equal(new DateOnly(2027, 10, 31), form.NgayKetThucMacDinh);
            Assert.Equal(new[] { "Điều 1" }, form.DieuKhoanHienTai.ToArray());
            Assert.Equal(new[] { "Nguyễn Văn A" }, form.ThanhVienDangO.ToArray());
        }

        [Fact]
        public async Task GetForm_EndedContract_Fails()
        {
            var kit = new Kit();
            kit.Contract.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;

            var result = await kit.Service.GetFormAsync(Staff, 10);

            Assert.False(result.Success);
        }
    }
}
