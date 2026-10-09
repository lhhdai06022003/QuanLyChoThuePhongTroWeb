using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Trung tâm việc cần làm: phân quyền fail closed, ưu tiên, mô tả, link, kỳ chốt điện nước, giờ VN.
    public class ViecCanLamServiceTests
    {
        private const int Admin = 1;
        private const int Staff = 5;
        private const int BranchA = 1;
        private const int BranchB = 2;

        // 2027-01-10 12:00 giờ VN
        private static readonly DateTime Now = new(2027, 1, 10, 5, 0, 0, DateTimeKind.Utc);

        private static EmployeeAccessScope AdminScope() => new(Admin, true);
        private static EmployeeAccessScope StaffOfA() => new(Staff, false, new[] { BranchA });

        private static (ViecCanLamService Service, FakeViecCanLamStore Store) Create(
            EmployeeAccessScope? scope, DateTime? nowUtc = null, DashboardSettings? dashboard = null, ViecCanLamSettings? settings = null)
        {
            var store = new FakeViecCanLamStore();
            var service = new ViecCanLamService(
                store,
                new FakeEmployeeAccessService { ScopeToReturn = scope },
                dashboard ?? new DashboardSettings(),
                settings ?? new ViecCanLamSettings(),
                new FixedTimeProvider(new DateTimeOffset(DateTime.SpecifyKind(nowUtc ?? Now, DateTimeKind.Utc))));
            return (service, store);
        }

        private static DemHoaDonRow HoaDon(int branchId, string ten, int soLuong, int nam = 2026, decimal conNo = 0)
            => new() { ChiNhanhId = branchId, TenChiNhanh = ten, SoLuong = soLuong, NamCuNhat = nam, TongConNo = conNo };

        private static DemChiNhanhRow Dem(int branchId, string ten, int soLuong)
            => new() { ChiNhanhId = branchId, TenChiNhanh = ten, SoLuong = soLuong };

        private static ViecCanLamDto Item(ViecCanLamTongHopDto dto, string ma) => dto.Items.Single(i => i.MaViec == ma);

        // 1-3. Fail closed

        [Fact]
        public async Task ScopeNull_ReturnsEmpty_AndDoesNotCallStore()
        {
            var (service, store) = Create(null);

            var tongHop = await service.GetTongHopAsync(Staff, null);
            var quaHan = await service.GetHoaDonQuaHanAsync(Staff, null);

            Assert.Equal(0, tongHop.TongSo);
            Assert.Empty(tongHop.Items);
            Assert.Empty(quaHan.Items);
            Assert.Equal(0, quaHan.TongSo);
            Assert.Empty(store.Calls);
        }

        [Fact]
        public async Task StaffWithoutAssignment_ReturnsEmpty_AndDoesNotCallStore()
        {
            var (service, store) = Create(new EmployeeAccessScope(Staff, false));

            var tongHop = await service.GetTongHopAsync(Staff, null);
            var quaHan = await service.GetHoaDonQuaHanAsync(Staff, null);

            Assert.Equal(0, tongHop.TongSo);
            Assert.Empty(tongHop.Items);
            Assert.Empty(quaHan.Items);
            Assert.Empty(store.Calls);
        }

        [Fact]
        public async Task StaffChoosingBranchOutsideScope_ReturnsEmpty_AndDoesNotCallStore()
        {
            var (service, store) = Create(StaffOfA());
            store.HoaDonNhap.Add(HoaDon(BranchB, "Chi nhanh B", 4));

            var tongHop = await service.GetTongHopAsync(Staff, BranchB);
            var quaHan = await service.GetHoaDonQuaHanAsync(Staff, BranchB);

            Assert.Equal(0, tongHop.TongSo);
            Assert.Empty(tongHop.Items);
            Assert.Empty(quaHan.Items);
            Assert.Empty(store.Calls);
        }

        // 4. Việc chỉ Admin

        [Fact]
        public async Task Staff_DoesNotGetHd2_AndStoreForHd2IsNotCalled()
        {
            var (service, store) = Create(StaffOfA());
            store.HoaDonChoDuyet.Add(HoaDon(BranchA, "A", 3));
            store.HoaDonNhap.Add(HoaDon(BranchA, "A", 2));

            var tongHop = await service.GetTongHopAsync(Staff, null);

            Assert.DoesNotContain(tongHop.Items, i => i.MaViec == "HD2");
            Assert.False(store.WasCalled(nameof(store.DemHoaDonChoDuyetAsync)));
            Assert.Contains(tongHop.Items, i => i.MaViec == "HD1");
            Assert.Equal(2, tongHop.TongSo);
        }

        [Fact]
        public async Task Admin_GetsHd2()
        {
            var (service, store) = Create(AdminScope());
            store.HoaDonChoDuyet.Add(HoaDon(BranchA, "A", 3));

            var tongHop = await service.GetTongHopAsync(Admin, null);

            Assert.Equal(3, Item(tongHop, "HD2").SoLuong);
            Assert.True(store.WasCalled(nameof(store.DemHoaDonChoDuyetAsync)));
        }

        // 5. Truyền phạm vi xuống store

        [Fact]
        public async Task Staff_PassesAssignedBranchesAndBranchId_ToEveryStoreCall()
        {
            var (service, store) = Create(StaffOfA());

            await service.GetTongHopAsync(Staff, BranchA);

            Assert.NotEmpty(store.Calls);
            Assert.All(store.Calls, c =>
            {
                Assert.Equal(BranchA, c.BranchId);
                Assert.NotNull(c.Allowed);
                Assert.Equal(new[] { BranchA }, c.Allowed!.ToArray());
            });
        }

        [Fact]
        public async Task Admin_PassesNullAllowed_AndKeepsBranchId()
        {
            var (service, store) = Create(AdminScope());

            await service.GetTongHopAsync(Admin, BranchB);
            Assert.NotEmpty(store.Calls);
            Assert.All(store.Calls, c =>
            {
                Assert.Equal(BranchB, c.BranchId);
                Assert.Null(c.Allowed);
            });

            store.Calls.Clear();
            await service.GetTongHopAsync(Admin, null);
            Assert.All(store.Calls, c =>
            {
                Assert.Null(c.BranchId);
                Assert.Null(c.Allowed);
            });
        }

        // 6. KH1

        [Fact]
        public async Task Kh1_IncidentSent23h59mAgo_IsCanLam_WithoutOverdueNote()
        {
            var (service, store) = Create(AdminScope());
            store.SuCoChoTiepNhan.Add((BranchA, "A", Now.AddHours(-24).AddMinutes(1)));

            var item = Item(await service.GetTongHopAsync(Admin, null), "KH1");

            Assert.Equal(MucUuTienViec.CanLam, item.MucUuTien);
            Assert.Equal(1, item.SoLuong);
            Assert.DoesNotContain("quá 24 giờ", item.MoTa);
        }

        [Fact]
        public async Task Kh1_IncidentSentExactly24hAgo_IsKhan_WithNote()
        {
            var (service, store) = Create(AdminScope());
            store.SuCoChoTiepNhan.Add((BranchA, "A", Now.AddHours(-24)));

            var item = Item(await service.GetTongHopAsync(Admin, null), "KH1");

            Assert.Equal(MucUuTienViec.Khan, item.MucUuTien);
            Assert.Contains("1 sự cố quá 24 giờ", item.MoTa);
        }

        // 7. KH3 theo ngày VN. Hôm nay VN = 10/01/2027, đầu ngày = 2027-01-09T17:00Z.

        [Fact]
        public async Task Kh3_ExpiringWithin7VnDays_IsCanLam()
        {
            var (service, store) = Create(AdminScope());
            // 17/01 VN 23:59 = hôm nay + 7
            store.HopDongSapHetHan.Add((BranchA, "A", new DateTime(2027, 1, 17, 16, 59, 0, DateTimeKind.Utc)));

            var item = Item(await service.GetTongHopAsync(Admin, null), "KH3");

            Assert.Equal(MucUuTienViec.CanLam, item.MucUuTien);
            Assert.Contains("1 hợp đồng còn tối đa 7 ngày", item.MoTa);
        }

        [Fact]
        public async Task Kh3_ExpiringIn8VnDays_IsTheoDoi()
        {
            var (service, store) = Create(AdminScope());
            // 18/01 VN 00:00 = hôm nay + 8
            store.HopDongSapHetHan.Add((BranchA, "A", new DateTime(2027, 1, 17, 17, 0, 0, DateTimeKind.Utc)));

            var item = Item(await service.GetTongHopAsync(Admin, null), "KH3");

            Assert.Equal(MucUuTienViec.TheoDoi, item.MucUuTien);
            Assert.DoesNotContain("còn tối đa", item.MoTa);
        }

        [Fact]
        public async Task Kh3_ExpiringIn30VnDays_IsCounted_Plus31IsNot()
        {
            var (service, store) = Create(AdminScope());
            store.HopDongSapHetHan.Add((BranchA, "A", new DateTime(2027, 2, 9, 16, 59, 0, DateTimeKind.Utc))); // +30 cuối ngày
            store.HopDongSapHetHan.Add((BranchA, "A", new DateTime(2027, 2, 9, 17, 0, 0, DateTimeKind.Utc)));  // +31 đầu ngày

            var item = Item(await service.GetTongHopAsync(Admin, null), "KH3");

            Assert.Equal(1, item.SoLuong);
        }

        [Fact]
        public async Task Kh3_OnlyBeyond31Days_OrAlreadyEnded_ProducesNoItem()
        {
            var (service, store) = Create(AdminScope());
            store.HopDongSapHetHan.Add((BranchA, "A", new DateTime(2027, 2, 9, 17, 0, 0, DateTimeKind.Utc)));  // +31
            store.HopDongSapHetHan.Add((BranchA, "A", new DateTime(2027, 1, 9, 16, 59, 0, DateTimeKind.Utc))); // hôm qua VN

            var tongHop = await service.GetTongHopAsync(Admin, null);

            Assert.DoesNotContain(tongHop.Items, i => i.MaViec == "KH3");
        }

        [Fact]
        public async Task Kh3_PassesVnDayBoundariesToStore()
        {
            var (service, store) = Create(AdminScope());

            await service.GetTongHopAsync(Admin, null);

            var call = store.Single(nameof(store.DemHopDongSapHetHanAsync));
            Assert.Equal(new DateTime(2027, 1, 9, 17, 0, 0), (DateTime)call.Args[0]!);
            Assert.Equal(new DateTime(2027, 2, 9, 17, 0, 0), (DateTime)call.Args[1]!);
            Assert.Equal(new DateTime(2027, 1, 17, 17, 0, 0), (DateTime)call.Args[2]!);
        }

        // 8. CS1

        private static HopDongChoChotChiSoRow HopDong(int phongId, DateTime start, int branchId = BranchA, string ten = "A")
            => new() { PhongTroId = phongId, ThoiDiemBatDau = start, ChiNhanhId = branchId, TenChiNhanh = ten };

        [Fact]
        public async Task Cs1_TargetPeriodIsCanLam_OlderPeriodsAreKhan_WithTitleAndLink()
        {
            var (service, store) = Create(AdminScope());
            store.HopDongDangHoatDong.Add(HopDong(10, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            var tongHop = await service.GetTongHopAsync(Admin, null);
            var cs1 = tongHop.Items.Where(i => i.MaViec == "CS1").ToList();

            Assert.Equal(6, cs1.Count);
            var target = cs1.Single(i => i.Thang == 12 && i.Nam == 2026);
            Assert.Equal(MucUuTienViec.CanLam, target.MucUuTien);
            Assert.Equal("Phòng chưa chốt điện nước T12/2026", target.TieuDe);
            Assert.StartsWith("/QuanLyNhaTro/ChotDienNuoc?thang=12&nam=2026", target.Link);
            Assert.All(cs1.Where(i => i != target), i => Assert.Equal(MucUuTienViec.Khan, i.MucUuTien));
        }

        [Fact]
        public async Task Cs1_PeriodWithAllRoomsRecorded_HasNoItem()
        {
            var (service, store) = Create(AdminScope());
            store.HopDongDangHoatDong.Add(HopDong(10, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            store.KyDaGhi.Add(new KyChiSoDaChotRow { PhongTroId = 10, Thang = 10, Nam = 2026 });

            var cs1 = (await service.GetTongHopAsync(Admin, null)).Items.Where(i => i.MaViec == "CS1").ToList();

            Assert.Equal(5, cs1.Count);
            Assert.DoesNotContain(cs1, i => i.Thang == 10 && i.Nam == 2026);
        }

        [Fact]
        public async Task Cs1_ContractStartingAfterPeriod_IsNotCountedForThatPeriod()
        {
            var (service, store) = Create(AdminScope());
            // Bắt đầu 2026-10-31T17:30Z = 01/11/2026 00:30 VN: không tính T10, tính từ T11.
            store.HopDongDangHoatDong.Add(HopDong(10, new DateTime(2026, 10, 31, 17, 30, 0, DateTimeKind.Utc)));

            var cs1 = (await service.GetTongHopAsync(Admin, null)).Items.Where(i => i.MaViec == "CS1").ToList();

            Assert.Equal(new[] { (11, 2026), (12, 2026) }, cs1.Select(i => (i.Thang!.Value, i.Nam!.Value)).OrderBy(x => x.Item2).ThenBy(x => x.Item1).ToArray());
        }

        [Fact]
        public async Task Cs1_CountsDistinctRoomsPerBranch()
        {
            var (service, store) = Create(AdminScope());
            var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            store.HopDongDangHoatDong.Add(HopDong(10, start));
            store.HopDongDangHoatDong.Add(HopDong(10, start)); // cùng phòng, hợp đồng thứ hai
            store.HopDongDangHoatDong.Add(HopDong(11, start, BranchB, "B"));

            var target = (await service.GetTongHopAsync(Admin, null)).Items.Single(i => i.MaViec == "CS1" && i.Thang == 12);

            Assert.Equal(2, target.SoLuong);
        }

        // 9. Kỳ mục tiêu CS1 và kỳ đầu quét

        [Theory]
        [InlineData("2027-01-04T05:00:00Z", 11, 2026, 6, 2026)] // 04/01 VN, chưa tới ngày 5
        [InlineData("2027-01-05T05:00:00Z", 12, 2026, 7, 2026)] // 05/01 VN
        [InlineData("2027-01-04T17:00:00Z", 12, 2026, 7, 2026)] // 00:00 VN 05/01
        [InlineData("2027-01-04T16:59:00Z", 11, 2026, 6, 2026)] // 23:59 VN 04/01
        [InlineData("2027-02-03T05:00:00Z", 12, 2026, 7, 2026)] // lùi 2 tháng qua năm
        [InlineData("2027-03-05T05:00:00Z", 2, 2027, 9, 2026)]  // kỳ đầu quét qua năm
        public async Task Cs1_TargetPeriodAndScanStart_FollowChotDay(string nowIso, int thang, int nam, int tuThang, int tuNam)
        {
            var now = DateTime.Parse(nowIso, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            var (service, store) = Create(AdminScope(), now, new DashboardSettings { ChotDienNuocDay = 5 });
            store.HopDongDangHoatDong.Add(HopDong(10, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            var tongHop = await service.GetTongHopAsync(Admin, null);
            var cs1 = tongHop.Items.Where(i => i.MaViec == "CS1").ToList();

            var target = cs1.Single(i => i.MucUuTien == MucUuTienViec.CanLam);
            Assert.Equal(thang, target.Thang);
            Assert.Equal(nam, target.Nam);
            Assert.Equal(6, cs1.Count);
            var call = store.Single(nameof(store.GetKyChiSoDaChotAsync));
            Assert.Equal(tuThang, (int)call.Args[0]!);
            Assert.Equal(tuNam, (int)call.Args[1]!);
        }

        // 10. Sắp xếp

        [Fact]
        public async Task Items_AreSortedByPriorityThenCountDescThenCodeThenPeriod()
        {
            var (service, store) = Create(AdminScope());
            store.MinhChung.Add(Dem(BranchA, "A", 2));                    // TT1 Khan 2
            store.HoaDonQuaHan.Add(HoaDon(BranchA, "A", 5, conNo: 100));  // TT2 Khan 5
            store.HoaDonNhap.Add(HoaDon(BranchA, "A", 3));                // HD1 CanLam 3
            store.HoaDonDaChot.Add(HoaDon(BranchA, "A", 3));              // HD3 CanLam 3
            store.AnhChoXacNhan.Add(new DemTheoKyRow { ChiNhanhId = BranchA, TenChiNhanh = "A", Thang = 11, Nam = 2026, SoLuong = 4 });
            store.AnhChoXacNhan.Add(new DemTheoKyRow { ChiNhanhId = BranchA, TenChiNhanh = "A", Thang = 10, Nam = 2026, SoLuong = 4 });
            store.HoaDonChuaToiHan.Add(HoaDon(BranchA, "A", 9));          // TT3 TheoDoi 9

            var order = (await service.GetTongHopAsync(Admin, null)).Items
                .Select(i => i.Thang.HasValue ? $"{i.MaViec}:{i.Thang}" : i.MaViec).ToArray();

            Assert.Equal(new[] { "TT2", "TT1", "CS2:10", "CS2:11", "HD1", "HD3", "TT3" }, order);
        }

        [Fact]
        public async Task Items_SameCodeDifferentCount_LargerCountFirst_EvenIfNewerPeriod()
        {
            var (service, store) = Create(AdminScope());
            store.AnhChoXacNhan.Add(new DemTheoKyRow { ChiNhanhId = BranchA, TenChiNhanh = "A", Thang = 10, Nam = 2026, SoLuong = 2 });
            store.AnhChoXacNhan.Add(new DemTheoKyRow { ChiNhanhId = BranchA, TenChiNhanh = "A", Thang = 11, Nam = 2026, SoLuong = 4 });

            var items = (await service.GetTongHopAsync(Admin, null)).Items;

            Assert.Equal(new[] { 11, 10 }, items.Select(i => i.Thang!.Value).ToArray());
        }

        // 11. Mô tả

        [Fact]
        public async Task Description_FourBranches_ShowsTopThreeAndRemainder()
        {
            var (service, store) = Create(AdminScope());
            store.HoaDonNhap.Add(HoaDon(1, "Alpha", 5));
            store.HoaDonNhap.Add(HoaDon(2, "Gamma", 7));
            store.HoaDonNhap.Add(HoaDon(3, "Beta", 7));
            store.HoaDonNhap.Add(HoaDon(4, "Delta", 1));

            var item = Item(await service.GetTongHopAsync(Admin, null), "HD1");

            Assert.Equal("Beta: 7 · Gamma: 7 · Alpha: 5 · và 1 chi nhánh khác", item.MoTa);
            Assert.Equal(20, item.SoLuong);
        }

        [Fact]
        public async Task Description_OverdueInvoices_ShowsRemainingDebtFormatted()
        {
            var (service, store) = Create(AdminScope());
            store.HoaDonQuaHan.Add(HoaDon(BranchA, "Chi nhanh A", 2, conNo: 3590000m));

            var item = Item(await service.GetTongHopAsync(Admin, null), "TT2");

            Assert.Equal("Chi nhanh A: 2 · còn nợ 3.590.000 đ", item.MoTa);
            Assert.Equal(3590000m, item.TongConNo);
        }

        [Fact]
        public async Task OverdueAndNotYetDue_AreCalledWithNowUtc()
        {
            var (service, store) = Create(AdminScope());

            await service.GetTongHopAsync(Admin, null);

            Assert.Equal(Now, (DateTime)store.Single(nameof(store.DemHoaDonQuaHanAsync)).Args[0]!);
            Assert.Equal(Now, (DateTime)store.Single(nameof(store.DemHoaDonDaGuiChuaToiHanAsync)).Args[0]!);
        }

        // 12. Link

        [Fact]
        public async Task Link_WithBranchFilter_ContainsChiNhanhId()
        {
            var (service, store) = Create(AdminScope());
            store.HoaDonNhap.Add(HoaDon(BranchA, "A", 1, 2026));
            store.HoaDonNhap.Add(HoaDon(BranchB, "B", 1, 2026));

            var item = Item(await service.GetTongHopAsync(Admin, BranchA), "HD1");

            Assert.Equal("/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam=2026&trangThaiPhatHanh=0&chiNhanhId=1", item.Link);
        }

        [Fact]
        public async Task Link_SingleBranchRow_ContainsChiNhanhId_MultiBranchDoesNot()
        {
            var (service, store) = Create(AdminScope());
            store.MinhChung.Add(Dem(7, "Solo", 2));
            store.HoaDonNhap.Add(HoaDon(1, "A", 1, 2026));
            store.HoaDonNhap.Add(HoaDon(2, "B", 1, 2025));

            var tongHop = await service.GetTongHopAsync(Admin, null);

            Assert.Equal("/QuanLyNhaTro/DoiChieuThanhToan?chiNhanhId=7", Item(tongHop, "TT1").Link);
            var hd1 = Item(tongHop, "HD1");
            Assert.DoesNotContain("chiNhanhId", hd1.Link);
            Assert.Equal("/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam=2025&trangThaiPhatHanh=0", hd1.Link);
        }

        [Fact]
        public async Task Link_Tt2WithBranch_PutsChiNhanhIdBeforeAnchor()
        {
            var (service, store) = Create(AdminScope());
            store.HoaDonQuaHan.Add(HoaDon(BranchA, "A", 1, conNo: 10));

            var item = Item(await service.GetTongHopAsync(Admin, BranchA), "TT2");

            Assert.Equal("/QuanLyNhaTro/ViecCanLam?chiNhanhId=1#hoa-don-qua-han", item.Link);
        }

        [Fact]
        public async Task Link_Tt2WithoutBranchAndManyBranches_HasAnchorOnly()
        {
            var (service, store) = Create(AdminScope());
            store.HoaDonQuaHan.Add(HoaDon(1, "A", 1, conNo: 10));
            store.HoaDonQuaHan.Add(HoaDon(2, "B", 1, conNo: 10));

            var item = Item(await service.GetTongHopAsync(Admin, null), "TT2");

            Assert.Equal("/QuanLyNhaTro/ViecCanLam#hoa-don-qua-han", item.Link);
        }

        // 13. Tổng

        [Fact]
        public async Task Totals_AreSumOfCountsPerPriority()
        {
            var (service, store) = Create(AdminScope());
            store.MinhChung.Add(Dem(BranchA, "A", 2));                   // Khan
            store.HoaDonQuaHan.Add(HoaDon(BranchA, "A", 5, conNo: 1));   // Khan
            store.HoaDonNhap.Add(HoaDon(BranchA, "A", 3));               // CanLam
            store.HoaDonChuaToiHan.Add(HoaDon(BranchA, "A", 4));         // TheoDoi

            var tongHop = await service.GetTongHopAsync(Admin, null);

            Assert.Equal(14, tongHop.TongSo);
            Assert.Equal(7, tongHop.SoKhan);
            Assert.Equal(3, tongHop.SoCanLam);
            Assert.Equal(4, tongHop.SoTheoDoi);
        }

        // 14. Bảng quá hạn

        [Fact]
        public async Task OverdueTable_MapsRowsAndPassesLimitFromSettings()
        {
            var (service, store) = Create(AdminScope());
            store.BangQuaHan.Add(new HoaDonQuaHanRow
            {
                HoaDonId = 1, MaHoaDon = "HD-1", SoPhong = "P1", ChiNhanhId = BranchA, TenChiNhanh = "A", KhachThue = "Nguyen Van A",
                TongTien = 5000000m, DaThu = 1410000m,
                HanThanhToan = new DateTime(2027, 1, 7, 20, 0, 0, DateTimeKind.Utc) // 08/01 03:00 VN
            });
            store.BangQuaHan.Add(new HoaDonQuaHanRow
            {
                HoaDonId = 2, MaHoaDon = "HD-2", SoPhong = "P2", ChiNhanhId = BranchA, TenChiNhanh = "A", KhachThue = "Tran B",
                TongTien = 100m, DaThu = 0m,
                HanThanhToan = new DateTime(2027, 1, 9, 16, 0, 0, DateTimeKind.Utc) // 09/01 23:00 VN
            });
            store.HoaDonQuaHan.Add(HoaDon(BranchA, "A", 62, conNo: 1));

            var dto = await service.GetHoaDonQuaHanAsync(Admin, null);

            Assert.Equal(50, (int)store.Single(nameof(store.GetHoaDonQuaHanAsync)).Args[1]!);
            Assert.Equal(62, dto.TongSo);
            Assert.Equal(new[] { 1, 2 }, dto.Items.Select(i => i.HoaDonId).ToArray());
            Assert.Equal(3590000m, dto.Items[0].SoConNo);
            Assert.Equal(2, dto.Items[0].SoNgayQuaHan);
            Assert.Equal(1, dto.Items[1].SoNgayQuaHan);
            Assert.Equal("Nguyen Van A", dto.Items[0].KhachThue);
        }

        [Fact]
        public async Task OverdueTable_Staff_PassesAllowedBranches()
        {
            var (service, store) = Create(StaffOfA());

            await service.GetHoaDonQuaHanAsync(Staff, null);

            Assert.Equal(new[] { BranchA }, store.Single(nameof(store.GetHoaDonQuaHanAsync)).Allowed!.ToArray());
        }

        // 15. Rỗng

        [Fact]
        public async Task EmptyStore_ReturnsNoItems()
        {
            var (service, _) = Create(AdminScope());

            var tongHop = await service.GetTongHopAsync(Admin, null);

            Assert.Empty(tongHop.Items);
            Assert.Equal(0, tongHop.TongSo);
            Assert.Equal(0, tongHop.SoKhan);
        }

        [Fact]
        public async Task RowsWithZeroCount_DoNotProduceItem()
        {
            var (service, store) = Create(AdminScope());
            store.HoaDonNhap.Add(HoaDon(BranchA, "A", 0));

            var tongHop = await service.GetTongHopAsync(Admin, null);

            Assert.DoesNotContain(tongHop.Items, i => i.MaViec == "HD1");
        }
    }
}
