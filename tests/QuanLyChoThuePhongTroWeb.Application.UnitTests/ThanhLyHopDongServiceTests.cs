using System;
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
    public class ThanhLyHopDongServiceTests
    {
        private const int Staff = 7;
        private const int Branch = 1;
        private const int OtherBranch = 2;
        private const int RootId = 10;
        private const string RootCode = "HD-CN-2510-001";
        private static readonly DateTime EndOctVn = new(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc);

        private sealed class Kit
        {
            public FakeHopDongLifecycleStore Store { get; } = new();
            public RecordingUnitOfWork Uow { get; } = new();
            public FakeEmployeeAccessService Access { get; } = new();
            public FakeThongBaoService ThongBao { get; } = new();
            public FakeBienBanThanhLyExporter Exporter { get; } = new();
            public ThanhLyHopDongService Service { get; }
            public PhongTro Room { get; }
            public HopDong Contract { get; }

            public Kit(decimal coc = 3_000_000m, DateTime? end = null, int[]? branches = null, bool grantSettle = true)
            {
                Access.ScopeToReturn = new EmployeeAccessScope(Staff, false, branches ?? new[] { Branch });
                if (grantSettle)
                {
                    Access.Permissions.Add((Staff, Branch, EmployeeActionCodes.ContractSettle));
                }

                var chiNhanh = new ChiNhanh { ChiNhanhId = Branch, TenChiNhanh = "CN Một", DiaChi = "1 Đường A", SoDienThoai = "0900000000" };
                Room = new PhongTro { PhongTroId = 5, ChiNhanhId = Branch, ChiNhanh = chiNhanh, SoPhong = "P101", TrangThai = TrangThaiPhong.DaThue };
                Store.PhongTros.Add(Room);
                var nguoiThue = new NguoiThue { NguoiThueId = 3, HoVaTen = "Nguyễn Văn A", CCCD = "0123", SoDienThoai = "0911" };
                Store.NguoiThues.Add(nguoiThue);
                Contract = NewContract(RootId, RootCode, TrangThaiHopDong.DangHoatDong, coc, end ?? EndOctVn);
                Store.HopDongs.Add(Contract);
                Store.Members.Add(new ChiTietThanhVienHopDong { ChiTietThanhVienHopDongId = 1, HopDongId = RootId, NguoiThueId = 3, NguoiThue = nguoiThue, NgayVao = Contract.ThoiDiemBatDau });
                Store.Services.Add(new DangKyDichVu { PhongTroId = 5, NgayBatDau = Contract.ThoiDiemBatDau });

                Service = new ThanhLyHopDongService(Store, Uow, Access, ThongBao, Exporter,
                    new FixedTimeProvider(new DateTimeOffset(Store.NowUtc)), NullLogger<ThanhLyHopDongService>.Instance);
            }

            public HopDong NewContract(int id, string ma, TrangThaiHopDong status, decimal coc, DateTime? end) => new()
            {
                HopDongId = id,
                MaHopDong = ma,
                PhongTroId = 5,
                PhongTro = Room,
                NguoiThueId = 3,
                NguoiThue = Store.NguoiThues[0],
                ThoiDiemBatDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = end,
                TienCocPhong = coc,
                TienThuePhong = 3_000_000m,
                TrangThaiHopDong = status
            };

            public HoaDon AddInvoice(int id, int hopDongId, int thang, int nam, decimal tong, TrangThaiPhatHanhHoaDon phatHanh = TrangThaiPhatHanhHoaDon.DaGui, decimal daThu = 0)
            {
                var hd = new HoaDon
                {
                    HoaDonId = id,
                    MaHoaDon = $"HDN-{id}",
                    HopDongId = hopDongId,
                    Thang = thang,
                    Nam = nam,
                    TongTien = tong,
                    TrangThaiPhatHanh = phatHanh,
                    TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan
                };
                Store.HoaDons.Add(hd);
                if (daThu > 0)
                {
                    Store.Ledgers.Add(new LichSuThanhToan { HoaDonId = id, SoTienThanhToan = daThu, MaGiaoDich = "TM-" + id, PhuongThucThanhToan = PhuongThucThanhToan.TienMat });
                }

                return hd;
            }

            public ThanhLyHopDongRequest Request(decimal tongNo, bool xacNhan = false) => new() { HopDongId = RootId, TongNoLucMo = tongNo, XacNhanConNo = xacNhan };
        }

        [Fact]
        public async Task OffsetsOldestPeriodFirst_AndPartiallyPaysTheNewest()
        {
            var kit = new Kit(coc: 3_000_000m);
            var t10 = kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);
            var t9 = kit.AddInvoice(91, RootId, 9, 2026, 1_500_000m);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(3_500_000m, xacNhan: true));

            Assert.True(result.Success, result.Message);
            Assert.Equal(3_000_000m, result.Data!.CanTru);
            Assert.Equal(0m, result.Data.CocHoanTrucTiep);
            Assert.Equal(500_000m, result.Data.ConNoSauCanTru);
            var offsets = kit.Store.Ledgers.Where(l => l.PhuongThucThanhToan == PhuongThucThanhToan.CanTruTienCoc).OrderBy(l => l.HoaDonId).ToList();
            Assert.Equal(2, offsets.Count);
            Assert.Equal(("CANTRU-HD10-91", 1_500_000m), (offsets[0].MaGiaoDich, offsets[0].SoTienThanhToan));
            Assert.Equal(("CANTRU-HD10-92", 1_500_000m), (offsets[1].MaGiaoDich, offsets[1].SoTienThanhToan));
            Assert.All(offsets, o =>
            {
                Assert.Equal(Staff, o.NguoiXacNhanId);
                Assert.Equal("Cấn trừ tiền cọc hợp đồng " + RootCode, o.GhiChu);
            });
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, t9.TrangThaiHoaDon);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, t10.TrangThaiHoaDon);
            Assert.Equal(TrangThaiHopDong.DaKetThuc, kit.Contract.TrangThaiHopDong);
        }

        [Fact]
        public async Task DepositLargerThanDebt_NeedsNoConfirmation_AndRefundsRest()
        {
            var kit = new Kit(coc: 5_000_000m);
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);
            kit.AddInvoice(91, RootId, 9, 2026, 1_500_000m);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(3_500_000m));

            Assert.True(result.Success, result.Message);
            Assert.Equal(3_500_000m, result.Data!.CanTru);
            Assert.Equal(1_500_000m, result.Data.CocHoanTrucTiep);
            Assert.Equal(0m, result.Data.ConNoSauCanTru);
            Assert.All(kit.Store.HoaDons, h => Assert.Equal(TrangThaiHoaDon.DaThanhToan, h.TrangThaiHoaDon));
        }

        [Fact]
        public async Task NoDebt_WritesNoLedger_ButStillEndsContract()
        {
            var kit = new Kit();
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m, daThu: 2_000_000m);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(0m));

            Assert.True(result.Success, result.Message);
            Assert.DoesNotContain(kit.Store.Ledgers, l => l.PhuongThucThanhToan == PhuongThucThanhToan.CanTruTienCoc);
            Assert.Equal(3_000_000m, result.Data!.CocHoanTrucTiep);
            Assert.Equal(TrangThaiHopDong.DaKetThuc, kit.Contract.TrangThaiHopDong);
        }

        [Fact]
        public async Task ZeroDeposit_LeavesAllDebt()
        {
            var kit = new Kit(coc: 0m);
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);

            var withoutConfirm = await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m));
            var withConfirm = await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m, xacNhan: true));

            Assert.False(withoutConfirm.Success);
            Assert.True(withConfirm.Success, withConfirm.Message);
            Assert.Equal(2_000_000m, withConfirm.Data!.ConNoSauCanTru);
            Assert.Empty(kit.Store.Ledgers);
        }

        [Fact]
        public async Task DebtRemaining_WithoutConfirmation_FailsAndChangesNothing()
        {
            var kit = new Kit(coc: 1_000_000m);
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m, xacNhan: false));

            Assert.False(result.Success);
            Assert.Contains("xác nhận", result.Message);
            Assert.Empty(kit.Store.Ledgers);
            Assert.Equal(TrangThaiHopDong.DangHoatDong, kit.Contract.TrangThaiHopDong);
            Assert.True(kit.Uow.Transactions.Single().RolledBack);
            Assert.False(kit.Uow.Transactions.Single().Committed);
            Assert.Equal(0, kit.Uow.SaveChangesCalls);
        }

        [Fact]
        public async Task DebtChangedSinceOpen_Fails()
        {
            var kit = new Kit();
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(1_000_000m));

            Assert.False(result.Success);
            Assert.Contains("đã thay đổi", result.Message);
            Assert.Equal(TrangThaiHopDong.DangHoatDong, kit.Contract.TrangThaiHopDong);
        }

        [Fact]
        public async Task MissingPublishedInvoiceForReturnMonth_Fails()
        {
            var kit = new Kit();
            kit.AddInvoice(91, RootId, 9, 2026, 1_000_000m);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(1_000_000m, xacNhan: true));

            Assert.False(result.Success);
            Assert.Contains("T10/2026", result.Message);
        }

        [Fact]
        public async Task PendingApprovalInvoice_Fails()
        {
            var kit = new Kit();
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);
            kit.AddInvoice(93, RootId, 11, 2026, 2_000_000m, TrangThaiPhatHanhHoaDon.ChoDuyet);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m, xacNhan: true));

            Assert.False(result.Success);
            Assert.Contains("Chờ duyệt", result.Message);
        }

        [Fact]
        public async Task SecondClick_Fails_AndAddsNoLedger()
        {
            var kit = new Kit(coc: 5_000_000m);
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);

            Assert.True((await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m))).Success);
            var ledgerCount = kit.Store.Ledgers.Count;
            var second = await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m));

            Assert.False(second.Success);
            Assert.Contains("đã được thanh lý", second.Message);
            Assert.Equal(ledgerCount, kit.Store.Ledgers.Count);
        }

        [Fact]
        public async Task RoomStaysRented_WhenAnotherActiveContractExists_OtherwiseBecomesEmpty()
        {
            var kit = new Kit();
            kit.AddInvoice(92, RootId, 10, 2026, 1_000_000m, daThu: 1_000_000m);
            kit.Store.HopDongs.Add(kit.NewContract(77, "HD-CN-2611-009", TrangThaiHopDong.DangHoatDong, 0, null));
            await kit.Service.ThanhLyAsync(Staff, kit.Request(0m));
            Assert.Equal(TrangThaiPhong.DaThue, kit.Room.TrangThai);

            var kit2 = new Kit();
            kit2.AddInvoice(92, RootId, 10, 2026, 1_000_000m, daThu: 1_000_000m);
            await kit2.Service.ThanhLyAsync(Staff, kit2.Request(0m));
            Assert.Equal(TrangThaiPhong.Trong, kit2.Room.TrangThai);
        }

        [Fact]
        public async Task MembersAndServices_AreClosedAtReturnDate()
        {
            var kit = new Kit();
            kit.AddInvoice(92, RootId, 10, 2026, 1_000_000m, daThu: 1_000_000m);

            await kit.Service.ThanhLyAsync(Staff, kit.Request(0m));

            Assert.Equal(EndOctVn, kit.Store.Members.Single().NgayChuyenDi);
            Assert.Equal(EndOctVn, kit.Store.Services.Single().NgayKetThuc);
        }

        [Fact]
        public async Task WaitingPaymentRequest_IsCancelled_ButUnderReviewBlocks()
        {
            var kit = new Kit(coc: 5_000_000m);
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);
            var waiting = YeuCauThanhToanHoaDon.Tao(92, "TTPTHD92", 2_000_000m, kit.Store.NowUtc.AddHours(2), Staff, kit.Store.NowUtc);
            kit.Store.Requests.Add(waiting);

            var ok = await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m));
            Assert.True(ok.Success, ok.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, waiting.TrangThai);

            var kit2 = new Kit(coc: 5_000_000m);
            kit2.AddInvoice(92, RootId, 10, 2026, 2_000_000m);
            var reviewing = YeuCauThanhToanHoaDon.Tao(92, "TTPTHD92", 2_000_000m, kit2.Store.NowUtc.AddHours(2), Staff, kit2.Store.NowUtc);
            reviewing.TrangThai = TrangThaiYeuCauThanhToan.DangDoiChieu;
            kit2.Store.Requests.Add(reviewing);

            var blocked = await kit2.Service.ThanhLyAsync(Staff, kit2.Request(2_000_000m));
            Assert.False(blocked.Success);
            Assert.Contains("đối chiếu", blocked.Message);
        }

        [Fact]
        public async Task OddAmounts_Block()
        {
            var kit = new Kit();
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000.5m);
            var odd = await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000.5m, xacNhan: true));
            Assert.False(odd.Success);
            Assert.Contains("số tiền lẻ", odd.Message);

            var kit2 = new Kit(coc: 1_000_000.5m);
            kit2.AddInvoice(92, RootId, 10, 2026, 2_000_000m, daThu: 2_000_000m);
            var oddCoc = await kit2.Service.ThanhLyAsync(Staff, kit2.Request(0m));
            Assert.False(oddCoc.Success);
            Assert.Contains("Tiền cọc có số lẻ", oddCoc.Message);
        }

        [Fact]
        public async Task Permissions_NullScope_OtherBranch_MissingAction()
        {
            var noScope = new Kit();
            noScope.Access.ScopeToReturn = null;
            var r1 = await noScope.Service.ThanhLyAsync(Staff, noScope.Request(0m));
            Assert.Equal(ServiceErrorKind.Forbidden, r1.ErrorKind);

            var otherBranch = new Kit(branches: new[] { OtherBranch });
            var r2 = await otherBranch.Service.ThanhLyAsync(Staff, otherBranch.Request(0m));
            Assert.Equal(ServiceErrorKind.Forbidden, r2.ErrorKind);
            Assert.Empty(otherBranch.Store.LockHopDongCalls);

            var missing = new Kit(grantSettle: false);
            var r3 = await missing.Service.ThanhLyAsync(Staff, missing.Request(0m));
            Assert.Equal(ServiceErrorKind.Forbidden, r3.ErrorKind);
            Assert.Empty(missing.Store.LockHopDongCalls);
        }

        [Fact]
        public async Task Notification_AfterCommit_FailureDoesNotChangeResult_AndNoneOnSaveError()
        {
            var kit = new Kit();
            kit.AddInvoice(92, RootId, 10, 2026, 1_000_000m, daThu: 1_000_000m);
            var ok = await kit.Service.ThanhLyAsync(Staff, kit.Request(0m));
            Assert.True(ok.Success);
            var sent = Assert.Single(kit.ThongBao.Sent);
            Assert.Equal("Phong", sent.Kind);
            Assert.Equal($"/QuanLyNhaTro/HopDong/ChiTiet/{RootId}", sent.Link);

            var throwing = new Kit();
            throwing.AddInvoice(92, RootId, 10, 2026, 1_000_000m, daThu: 1_000_000m);
            throwing.ThongBao.ThrowOnSend = true;
            Assert.True((await throwing.Service.ThanhLyAsync(Staff, throwing.Request(0m))).Success);

            var failing = new Kit();
            failing.AddInvoice(92, RootId, 10, 2026, 1_000_000m, daThu: 1_000_000m);
            failing.Uow.FailOnSave = _ => new InvalidOperationException("db");
            var bad = await failing.Service.ThanhLyAsync(Staff, failing.Request(0m));
            Assert.False(bad.Success);
            Assert.Empty(failing.ThongBao.Sent);
            Assert.True(failing.Uow.Transactions.Single().RolledBack);
        }

        [Fact]
        public async Task Chain_OffsetsOldContractDebtFirst_AndLeavesUnrelatedContractAlone()
        {
            var kit = new Kit(coc: 1_500_000m);
            kit.Contract.MaHopDong = RootCode + "-GH1";
            var old = kit.NewContract(9, RootCode, TrangThaiHopDong.DaKetThuc, 0, new DateTime(2026, 6, 30, 16, 59, 59, DateTimeKind.Utc));
            kit.Store.HopDongs.Add(old);
            var other = kit.NewContract(20, "HD-OTHER-001", TrangThaiHopDong.DaKetThuc, 0, new DateTime(2026, 3, 31, 16, 59, 59, DateTimeKind.Utc));
            kit.Store.HopDongs.Add(other);
            var oldInvoice = kit.AddInvoice(71, 9, 6, 2026, 1_000_000m);
            var currentInvoice = kit.AddInvoice(101, RootId, 10, 2026, 2_000_000m);
            var unrelated = kit.AddInvoice(201, 20, 3, 2026, 500_000m);

            var preview = await kit.Service.GetPreviewAsync(Staff, RootId);
            Assert.True(preview.Success, preview.Message);
            Assert.Equal(new[] { 71, 101 }, preview.Data!.HoaDonNo.Select(h => h.HoaDonId).ToArray());
            Assert.Equal(3_000_000m, preview.Data.TongNo);

            var result = await kit.Service.ThanhLyAsync(Staff, kit.Request(3_000_000m, xacNhan: true));

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, oldInvoice.TrangThaiHoaDon);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, currentInvoice.TrangThaiHoaDon);
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, unrelated.TrangThaiHoaDon);
            Assert.DoesNotContain(kit.Store.Ledgers, l => l.HoaDonId == 201);
            Assert.Contains(kit.Store.Ledgers, l => l.MaGiaoDich == "CANTRU-HD10-71" && l.SoTienThanhToan == 1_000_000m);
            Assert.Contains(kit.Store.Ledgers, l => l.MaGiaoDich == "CANTRU-HD10-101" && l.SoTienThanhToan == 500_000m);
        }

        [Fact]
        public async Task Preview_ReportsBlockReasons_ForStaffWithoutSettlePermission()
        {
            var kit = new Kit(grantSettle: false);

            var result = await kit.Service.GetPreviewAsync(Staff, RootId);

            Assert.True(result.Success);
            Assert.False(result.Data!.CoTheThanhLy);
            Assert.Contains(result.Data.LyDoChan, r => r.Contains("T10/2026"));
        }

        [Fact]
        public async Task BienBan_NotSettled_Fails_ThenPrintsCurrentFigures()
        {
            var kit = new Kit(coc: 1_500_000m);
            kit.AddInvoice(92, RootId, 10, 2026, 2_000_000m);

            var notYet = await kit.Service.XuatBienBanAsync(Staff, RootId, "ghi chú");
            Assert.False(notYet.Success);

            Assert.True((await kit.Service.ThanhLyAsync(Staff, kit.Request(2_000_000m, xacNhan: true))).Success);
            var printed = await kit.Service.XuatBienBanAsync(Staff, RootId, "  Hẹn trả trước 30/11  ");

            Assert.True(printed.Success, printed.Message);
            Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, printed.Data);
            var data = kit.Exporter.LastData!;
            Assert.Equal(1_500_000m, data.TongCanTru);
            Assert.Equal(0m, data.CocHoanTrucTiep);
            Assert.Equal(500_000m, data.NoConLai);
            Assert.Equal("Hẹn trả trước 30/11", data.GhiChu);
            var row = Assert.Single(data.HoaDons);
            Assert.Equal(1_500_000m, row.CanTru);
            Assert.Equal(0m, row.DaThuKhac);

            await kit.Service.XuatBienBanAsync(Staff, RootId, null);
            Assert.Null(kit.Exporter.LastData!.GhiChu);
        }

        [Fact]
        public async Task CapNhatNgayTraPhong_ValidDate_UpdatesTo165959Utc()
        {
            var kit = new Kit();
            var ngayMoi = new DateOnly(2026, 11, 15);

            var res = await kit.Service.CapNhatNgayTraPhongAsync(Staff, RootId, ngayMoi);

            Assert.True(res.Success, res.Message);
            Assert.Equal(new DateTime(2026, 11, 15, 16, 59, 59, DateTimeKind.Utc), kit.Contract.ThoiDiemKetThuc);
            Assert.True(kit.Uow.SaveChangesCalls > 0);
            Assert.True(kit.Uow.Transactions.Single().Committed);
        }

        [Fact]
        public async Task CapNhatNgayTraPhong_BeforeStartDate_Fails()
        {
            var kit = new Kit();
            var ngayTruoc = new DateOnly(2025, 12, 31);

            var res = await kit.Service.CapNhatNgayTraPhongAsync(Staff, RootId, ngayTruoc);

            Assert.False(res.Success);
            Assert.Contains("từ ngày bắt đầu", res.Message);
            Assert.Equal(0, kit.Uow.SaveChangesCalls);
        }

        [Fact]
        public async Task CapNhatNgayTraPhong_NotActiveContract_Fails()
        {
            var kit = new Kit();
            kit.Contract.TrangThaiHopDong = TrangThaiHopDong.DaKetThuc;

            var res = await kit.Service.CapNhatNgayTraPhongAsync(Staff, RootId, new DateOnly(2026, 11, 15));

            Assert.False(res.Success);
            Assert.Contains("đang hoạt động", res.Message);
            Assert.Equal(0, kit.Uow.SaveChangesCalls);
        }

        [Fact]
        public async Task CapNhatNgayTraPhong_ExistingInvoiceInFromPeriod_Fails_ExceptDeleted()
        {
            var kit = new Kit();
            var hd = kit.AddInvoice(101, RootId, 11, 2026, 1_000_000m);

            var res = await kit.Service.CapNhatNgayTraPhongAsync(Staff, RootId, new DateOnly(2026, 11, 15));
            Assert.False(res.Success);
            Assert.Contains("Đã có hóa đơn từ T10/2026", res.Message);

            hd.IsDeleted = true;
            var res2 = await kit.Service.CapNhatNgayTraPhongAsync(Staff, RootId, new DateOnly(2026, 11, 15));
            Assert.True(res2.Success, res2.Message);
        }

        [Fact]
        public async Task CapNhatNgayTraPhong_UnauthorizedStaffOrOtherBranch_Forbidden()
        {
            var kitNoPerm = new Kit(grantSettle: false);
            var res1 = await kitNoPerm.Service.CapNhatNgayTraPhongAsync(Staff, RootId, new DateOnly(2026, 11, 15));
            Assert.Equal(ServiceErrorKind.Forbidden, res1.ErrorKind);

            var kitOtherBranch = new Kit(branches: new[] { OtherBranch });
            var res2 = await kitOtherBranch.Service.CapNhatNgayTraPhongAsync(Staff, RootId, new DateOnly(2026, 11, 15));
            Assert.Equal(ServiceErrorKind.Forbidden, res2.ErrorKind);
        }

        [Fact]
        public async Task CapNhatNgayTraPhong_SameDate_ReturnsOkWithoutSave()
        {
            var kit = new Kit();
            var res = await kit.Service.CapNhatNgayTraPhongAsync(Staff, RootId, new DateOnly(2026, 10, 31));

            Assert.True(res.Success);
            Assert.Equal(0, kit.Uow.SaveChangesCalls);
        }

        [Fact]
        public async Task Preview_FillsNewFieldsCorrectly()
        {
            var kit = new Kit();
            kit.AddInvoice(1, RootId, 10, 2026, 1_000_000m, phatHanh: TrangThaiPhatHanhHoaDon.Nhap);
            kit.AddInvoice(2, RootId, 9, 2026, 1_000_000m, phatHanh: TrangThaiPhatHanhHoaDon.ChoDuyet);

            var preview = await kit.Service.GetPreviewAsync(Staff, RootId);

            Assert.True(preview.Success);
            var data = preview.Data!;
            Assert.True(data.DangHoatDong);
            Assert.Equal(Branch, data.ChiNhanhId);
            Assert.Equal(5, data.PhongTroId);
            Assert.Equal(10, data.ThangTraPhong);
            Assert.Equal(2026, data.NamTraPhong);
            Assert.True(data.ThieuHoaDonThangTraPhong);
            Assert.Equal(2, data.HoaDonChuaCongBo.Count);
            Assert.Equal(9, data.HoaDonChuaCongBo[0].Thang);
            Assert.Equal("Chờ duyệt", data.HoaDonChuaCongBo[0].TrangThaiText);
            Assert.Equal(10, data.HoaDonChuaCongBo[1].Thang);
            Assert.Equal("Nháp", data.HoaDonChuaCongBo[1].TrangThaiText);
        }
    }
}
