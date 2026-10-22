using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // 2026-09-30T18:00:00Z = 01/10/2026 01:00 giờ Việt Nam.
    public class AiToolExecutorTests
    {
        private readonly FakeAiAssistantStore _store = new();
        private readonly CapturingLogger<AiToolExecutor> _logger = new();
        private readonly AiToolExecutor _executor;

        private static readonly AiCallerContext Admin = new(1, AiCallerRole.Admin, null, null);
        private static readonly AiCallerContext Staff = new(2, AiCallerRole.NhanVien, new[] { 10, 11 }, null);
        private static readonly AiCallerContext Tenant = new(3, AiCallerRole.KhachThue, null, 55);

        public AiToolExecutorTests()
        {
            _executor = new AiToolExecutor(_store, _logger, new FixedTimeProvider(DateTimeOffset.Parse("2026-09-30T18:00:00Z")));
        }

        private static AiFunctionCall Call(string name, string json = "{}") => new(name, json, null);

        private static JsonElement Parse(AiToolResult result) => JsonDocument.Parse(result.Json).RootElement.Clone();

        private static AiUnpaidInvoiceRow Invoice(string ma, decimal tong, decimal daThu)
            => new(1, ma, 7, "101", "Chi nhánh A", "Nguyễn Văn A", 9, 2026, tong, daThu);

        [Fact]
        public async Task NoArgs_Q1Q8Q9K2_UseCurrentVietnamMonth()
        {
            _store.RoomsByNumber.Add(new AiRoomRow(7, "101", 1, 10, "Chi nhánh A", 1_000_000m));
            _store.TenantContracts.Add(new AiTenantContractRow("HD1", 7, "101", "Chi nhánh A", DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime(), null, 1m, 1m));

            await _executor.ExecuteAsync(Admin, Call(AiToolNames.PhongTroChuaChotDienNuoc));
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.DoanhThuChiNhanh));
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.ChiSoDienNuoc, "{\"soPhong\":\"101\"}"));
            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyChiSoDienNuoc));

            Assert.Equal(10, _store.CallsOf("GetRentedRoomsWithoutMeterReadingAsync").Single().Args["thang"]);
            Assert.Equal(2026, _store.CallsOf("GetRentedRoomsWithoutMeterReadingAsync").Single().Args["nam"]);
            Assert.Equal(10, _store.CallsOf("GetRevenueByBranchAsync").Single().Args["thang"]);
            Assert.Equal(2026, _store.CallsOf("GetRevenueByBranchAsync").Single().Args["nam"]);
            Assert.All(_store.CallsOf("GetMeterReadingsAsync"), c =>
            {
                Assert.Equal(10, c.Args["thang"]);
                Assert.Equal(2026, c.Args["nam"]);
            });
            Assert.Equal(2, _store.CallsOf("GetMeterReadingsAsync").Count());
        }

        [Fact]
        public async Task Revenue_DefaultsToStartOfMonthUntilToday_AsUtcRange()
        {
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.DoanhThuThucThu));

            var call = _store.CallsOf("GetRevenueAsync").Single();
            Assert.Equal(DateTime.Parse("2026-09-30T17:00:00Z").ToUniversalTime(), call.Args["fromUtc"]);
            Assert.Equal(DateTime.Parse("2026-10-01T17:00:00Z").ToUniversalTime(), call.Args["toExclusiveUtc"]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)call.Args["fromUtc"]!).Kind);
        }

        [Fact]
        public async Task Revenue_ExplicitVietnamDate_ConvertsToUtcRange()
        {
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.DoanhThuThucThu, "{\"tuNgay\":\"2026-10-05\",\"denNgay\":\"2026-10-05\"}"));

            var call = _store.CallsOf("GetRevenueAsync").Single();
            Assert.Equal(DateTime.Parse("2026-10-04T17:00:00Z").ToUniversalTime(), call.Args["fromUtc"]);
            Assert.Equal(DateTime.Parse("2026-10-05T17:00:00Z").ToUniversalTime(), call.Args["toExclusiveUtc"]);
        }

        [Fact]
        public async Task OptionalPeriod_Q2K3_Defaults()
        {
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.HoaDonChuaThanhToan));
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.HoaDonChuaThanhToan, "{\"thang\":9}"));
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.HoaDonChuaThanhToan, "{\"nam\":2025}"));
            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyHoaDonChuaThanhToan));

            var admin = _store.CallsOf("GetUnpaidInvoicesAsync").ToList();
            Assert.Null(admin[0].Args["thang"]);
            Assert.Null(admin[0].Args["nam"]);
            Assert.Equal(9, admin[1].Args["thang"]);
            Assert.Equal(2026, admin[1].Args["nam"]);
            Assert.Null(admin[2].Args["thang"]);
            Assert.Equal(2025, admin[2].Args["nam"]);

            var tenant = _store.CallsOf("GetTenantUnpaidInvoicesAsync").Single();
            Assert.Null(tenant.Args["thang"]);
            Assert.Null(tenant.Args["nam"]);
        }

        [Fact]
        public async Task ExpiringContracts_DefaultsTo30Days()
        {
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.HopDongSapHetHan));

            var call = _store.CallsOf("GetExpiringContractsAsync").Single();
            Assert.Equal(DateTime.Parse("2026-09-30T18:00:00Z").ToUniversalTime(), call.Args["fromUtc"]);
            Assert.Equal(DateTime.Parse("2026-10-30T18:00:00Z").ToUniversalTime(), call.Args["toUtc"]);
        }

        [Theory]
        [InlineData(AiToolNames.PhongTroChuaChotDienNuoc, "{\"thang\":13}")]
        [InlineData(AiToolNames.PhongTroChuaChotDienNuoc, "{\"thang\":-1}")]
        [InlineData(AiToolNames.PhongTroChuaChotDienNuoc, "{\"nam\":\"abc\"}")]
        [InlineData(AiToolNames.PhongTroChuaChotDienNuoc, "{\"nam\":1999}")]
        [InlineData(AiToolNames.PhongTroChuaChotDienNuoc, "{\"nam\":2101}")]
        [InlineData(AiToolNames.HopDongSapHetHan, "{\"soNgay\":-5}")]
        [InlineData(AiToolNames.HopDongSapHetHan, "{\"soNgay\":0}")]
        [InlineData(AiToolNames.HopDongSapHetHan, "{\"soNgay\":366}")]
        [InlineData(AiToolNames.DoanhThuThucThu, "{\"tuNgay\":\"2026/10/01\"}")]
        [InlineData(AiToolNames.DoanhThuThucThu, "{\"tuNgay\":\"2026-02-30\"}")]
        [InlineData(AiToolNames.DoanhThuThucThu, "{\"tuNgay\":\"2026-10-05\",\"denNgay\":\"2026-10-01\"}")]
        [InlineData(AiToolNames.PhongTrong, "{\"mucGiaToiDa\":0}")]
        [InlineData(AiToolNames.PhongTrong, "{\"mucGiaToiDa\":\"rẻ\"}")]
        [InlineData(AiToolNames.ThongTinKhachThue, "{\"tuKhoa\":\"  \"}")]
        [InlineData(AiToolNames.ThongTinKhachThue, "{}")]
        [InlineData(AiToolNames.CongNoPhong, "{}")]
        [InlineData(AiToolNames.ChiSoDienNuoc, "{\"thang\":5}")]
        [InlineData(AiToolNames.HoaDonChuaThanhToan, "[1]")]
        [InlineData(AiToolNames.HoaDonChuaThanhToan, "khong phai json")]
        public async Task InvalidArgs_ReturnVietnameseError_AndDoNotQueryStore(string tool, string json)
        {
            var result = await _executor.ExecuteAsync(Admin, Call(tool, json));

            Assert.False(result.Success);
            Assert.Equal(AiToolResult.LoiThamSo, result.Outcome);
            var root = Parse(result);
            Assert.Equal("THAM_SO_KHONG_HOP_LE", root.GetProperty("maLoi").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("thongBao").GetString()));
            Assert.Empty(_store.Calls);
        }

        [Fact]
        public async Task ZeroMonthOrYear_IsTreatedAsMissing()
        {
            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.PhongTroChuaChotDienNuoc, "{\"thang\":0,\"nam\":0}"));

            Assert.True(result.Success);
            Assert.Equal(10, _store.Calls.Single().Args["thang"]);
        }

        [Fact]
        public async Task UnknownArgs_AreIgnored()
        {
            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.PhongTrong, "{\"nguoiThueId\":999}"));

            Assert.True(result.Success);
        }

        [Fact]
        public async Task Scope_AdminNull_StaffBranches_StaffWithoutListGetsEmpty()
        {
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.PhongTrong));
            await _executor.ExecuteAsync(Staff, Call(AiToolNames.PhongTrong));
            await _executor.ExecuteAsync(new AiCallerContext(4, AiCallerRole.NhanVien, null, null), Call(AiToolNames.PhongTrong));

            var calls = _store.CallsOf("GetVacantRoomsAsync").ToList();
            Assert.Null(calls[0].Allowed);
            Assert.Equal(new[] { 10, 11 }, calls[1].Allowed!.OrderBy(x => x));
            Assert.NotNull(calls[2].Allowed);
            Assert.Empty(calls[2].Allowed!);
        }

        [Fact]
        public async Task WrongRoleOrUnknownTool_IsUnavailable_AndDoesNotQueryStore()
        {
            var tenantCallsManagerTool = await _executor.ExecuteAsync(Tenant, Call(AiToolNames.PhongTrong));
            var staffCallsTenantTool = await _executor.ExecuteAsync(Staff, Call(AiToolNames.MyHoaDonChuaThanhToan));
            var unknown = await _executor.ExecuteAsync(Admin, Call("DeleteEverything"));
            var tenantWithoutId = await _executor.ExecuteAsync(new AiCallerContext(9, AiCallerRole.KhachThue, null, null), Call(AiToolNames.MyHopDongInfo));

            foreach (var r in new[] { tenantCallsManagerTool, staffCallsTenantTool, unknown, tenantWithoutId })
            {
                Assert.False(r.Success);
                Assert.Equal(AiToolResult.KhongKhaDung, r.Outcome);
                Assert.Equal("CONG_CU_KHONG_KHA_DUNG", Parse(r).GetProperty("maLoi").GetString());
            }
            Assert.Empty(_store.Calls);
        }

        [Fact]
        public async Task UnpaidInvoices_ComputesRemainingAndTotals()
        {
            _store.UnpaidInvoices.Add(Invoice("HD-1", 1_000_000m, 400_000m));
            _store.UnpaidInvoices.Add(Invoice("HD-2", 500_000m, 0m));

            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.HoaDonChuaThanhToan));

            var root = Parse(result);
            Assert.True(root.GetProperty("thanhCong").GetBoolean());
            Assert.Equal(2, root.GetProperty("soHoaDon").GetInt32());
            Assert.Equal(2, root.GetProperty("soLuong").GetInt32());
            Assert.Equal(1_100_000m, root.GetProperty("tongConNo").GetDecimal());
            var hoaDons = root.GetProperty("hoaDons");
            Assert.Equal(600_000m, hoaDons[0].GetProperty("conLai").GetDecimal());
            Assert.Equal(500_000m, hoaDons[1].GetProperty("conLai").GetDecimal());
            Assert.Equal("tất cả các kỳ", root.GetProperty("kyLoc").GetString());
        }

        [Fact]
        public async Task RoomLookup_NoRoom_ReturnsNotFound()
        {
            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.CongNoPhong, "{\"soPhong\":\"999\"}"));

            var root = Parse(result);
            Assert.True(result.Success);
            Assert.False(root.GetProperty("timThayPhong").GetBoolean());
            Assert.Equal("Không tìm thấy phòng.", root.GetProperty("thongBao").GetString());
            Assert.Empty(_store.CallsOf("GetUnpaidInvoicesAsync"));
        }

        [Theory]
        [InlineData(AiToolNames.CongNoPhong)]
        [InlineData(AiToolNames.ChiSoDienNuoc)]
        public async Task RoomLookup_MultipleRooms_AsksForBranch_WithoutFurtherQueries(string tool)
        {
            _store.RoomsByNumber.Add(new AiRoomRow(1, "101", 1, 10, "Chi nhánh A", 1m));
            _store.RoomsByNumber.Add(new AiRoomRow(2, "101", 1, 11, "Chi nhánh B", 1m));

            var result = await _executor.ExecuteAsync(Admin, Call(tool, "{\"soPhong\":\"101\"}"));

            var root = Parse(result);
            Assert.True(root.GetProperty("canChonChiNhanh").GetBoolean());
            Assert.Equal(2, root.GetProperty("soLuong").GetInt32());
            Assert.Equal(2, root.GetProperty("cacChiNhanh").GetArrayLength());
            Assert.Empty(_store.CallsOf("GetUnpaidInvoicesAsync"));
            Assert.Empty(_store.CallsOf("GetMeterReadingsAsync"));
        }

        [Fact]
        public async Task RoomDebt_SingleRoom_QueriesInvoicesOfThatRoomOnly()
        {
            _store.RoomsByNumber.Add(new AiRoomRow(42, "101", 1, 10, "Chi nhánh A", 1m));
            _store.UnpaidInvoices.Add(Invoice("HD-1", 1_000_000m, 250_000m));

            var result = await _executor.ExecuteAsync(Staff, Call(AiToolNames.CongNoPhong, "{\"soPhong\":\"101\"}"));

            var call = _store.CallsOf("GetUnpaidInvoicesAsync").Single();
            Assert.Null(call.Args["thang"]);
            Assert.Null(call.Args["nam"]);
            Assert.Equal(42, call.Args["phongTroId"]);
            Assert.Equal(new[] { 10, 11 }, call.Allowed!.OrderBy(x => x));
            Assert.Equal(750_000m, Parse(result).GetProperty("tongConNo").GetDecimal());
        }

        [Fact]
        public async Task MeterReading_SingleRoom_ComputesConsumption()
        {
            _store.RoomsByNumber.Add(new AiRoomRow(42, "101", 1, 10, "Chi nhánh A", 1m));
            _store.MeterReadings.Add(new AiMeterReadingRow(42, "101", "Chi nhánh A", 10, 2026, 100m, 150m, 20m, 28m));

            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.ChiSoDienNuoc, "{\"soPhong\":\"101\"}"));

            var chiSo = Parse(result).GetProperty("chiSo");
            Assert.Equal(50m, chiSo.GetProperty("dienTieuThu").GetDecimal());
            Assert.Equal(8m, chiSo.GetProperty("nuocTieuThu").GetDecimal());
        }

        [Fact]
        public async Task TenantTools_AlwaysUseCallerNguoiThueId_NotArguments()
        {
            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyHopDongInfo, "{\"nguoiThueId\":999}"));
            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyHoaDonChuaThanhToan, "{\"nguoiThueId\":999}"));
            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyChiSoDienNuoc, "{\"nguoiThueId\":999}"));

            Assert.Equal(55, _store.CallsOf("GetTenantUnpaidInvoicesAsync").Single().Args["nguoiThueId"]);
            Assert.All(_store.CallsOf("GetActiveContractsOfTenantAsync"), c => Assert.Equal(55, c.Args["nguoiThueId"]));
        }

        [Fact]
        public async Task TenantMeter_WithoutContracts_ReturnsFriendlyMessage()
        {
            var result = await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyChiSoDienNuoc));

            var root = Parse(result);
            Assert.Equal(0, root.GetProperty("soLuong").GetInt32());
            Assert.Equal("Bạn không có hợp đồng đang hoạt động.", root.GetProperty("thongBao").GetString());
            Assert.Empty(_store.CallsOf("GetMeterReadingsAsync"));
        }

        [Fact]
        public async Task TenantContracts_ShowRentAndDeposit_AndOpenEndedLabel()
        {
            _store.TenantContracts.Add(new AiTenantContractRow("HD1", 7, "101", "Chi nhánh A",
                DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime(), null, 3_000_000m, 6_000_000m));

            var result = await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyHopDongInfo));

            var hd = Parse(result).GetProperty("hopDongs")[0];
            Assert.Equal(3_000_000m, hd.GetProperty("giaThue").GetDecimal());
            Assert.Equal(6_000_000m, hd.GetProperty("tienCoc").GetDecimal());
            Assert.Equal("Không thời hạn", hd.GetProperty("ngayKetThuc").GetString());
            Assert.Equal("01/01/2026", hd.GetProperty("ngayBatDau").GetString());
        }

        [Fact]
        public async Task ExpiringContracts_FormatsVietnamDateAndDaysLeft()
        {
            _store.ExpiringContracts.Add(new AiExpiringContractRow("101", "Chi nhánh A", "Nguyễn Văn A", DateTime.Parse("2026-10-10T20:00:00Z").ToUniversalTime()));

            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.HopDongSapHetHan));

            var hd = Parse(result).GetProperty("hopDongs")[0];
            Assert.Equal("11/10/2026", hd.GetProperty("ngayKetThuc").GetString());
            Assert.Equal(10, hd.GetProperty("soNgayConLai").GetInt32());
        }

        [Fact]
        public async Task TenantStatusLabels_AreVietnamese()
        {
            _store.TenantLookup.Add(new AiTenantLookupRow("101", "Chi nhánh A", "An", "0900", TrangThaiHopDong.DaKetThuc));

            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.ThongTinKhachThue, "{\"tuKhoa\":\"an\"}"));

            Assert.Equal("Đã kết thúc", Parse(result).GetProperty("ketQua")[0].GetProperty("trangThaiHopDong").GetString());
        }

        [Fact]
        public async Task BranchRevenue_SumsTotalAndKeepsPerBranchCounts()
        {
            _store.BranchRevenue.Add(new AiBranchRevenueRow(10, "Chi nhánh A", 1_000_000m, 2));
            _store.BranchRevenue.Add(new AiBranchRevenueRow(11, "Chi nhánh B", 500_000m, 1));

            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.DoanhThuChiNhanh));

            var root = Parse(result);
            Assert.Equal(1_500_000m, root.GetProperty("tongDoanhThu").GetDecimal());
            Assert.Equal(2, root.GetProperty("soLuong").GetInt32());
            Assert.Equal(2, root.GetProperty("chiNhanhs")[0].GetProperty("soGiaoDich").GetInt32());
        }

        [Fact]
        public async Task StoreThrows_ReturnsGenericError_WithoutLeakingMessage()
        {
            _store.ThrowOnCall = new InvalidOperationException("password=SECRET connection failed");

            var result = await _executor.ExecuteAsync(Admin, Call(AiToolNames.PhongTrong));

            Assert.False(result.Success);
            Assert.Equal(AiToolResult.LoiTruyVan, result.Outcome);
            Assert.DoesNotContain("SECRET", result.Json);
            Assert.Equal("LOI_TRUY_VAN", Parse(result).GetProperty("maLoi").GetString());
        }

        [Fact]
        public async Task Cancellation_IsNotSwallowed()
        {
            _store.ThrowOnCall = new OperationCanceledException();

            await Assert.ThrowsAsync<OperationCanceledException>(() => _executor.ExecuteAsync(Admin, Call(AiToolNames.PhongTrong)));
        }

        [Fact]
        public async Task Logging_WritesOneInformationLinePerCall_WithoutArgumentValues()
        {
            await _executor.ExecuteAsync(Admin, Call(AiToolNames.ThongTinKhachThue, "{\"tuKhoa\":\"BiMat123\"}"));

            var info = _logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
            Assert.Single(info);
            Assert.Contains(AiToolNames.ThongTinKhachThue, info[0].Message);
            Assert.DoesNotContain("BiMat123", string.Join("\n", _logger.Entries.Select(e => e.Message)));
        }

        [Fact]
        public async Task Logging_AlsoLogsRejectedCalls()
        {
            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.PhongTrong));

            var info = _logger.Entries.Single(e => e.Level == LogLevel.Information);
            Assert.Contains(AiToolResult.KhongKhaDung, info.Message);
        }

        [Fact]
        public async Task UnpaidInvoices_MoreThan50_ListsFirst50_ButTotalsCoverAll()
        {
            for (var i = 1; i <= 60; i++)
            {
                _store.UnpaidInvoices.Add(Invoice($"HD{i}", 1_000_000m, 250_000m));
            }

            var root = Parse(await _executor.ExecuteAsync(Admin, Call(AiToolNames.HoaDonChuaThanhToan)));

            Assert.Equal(60, root.GetProperty("soLuong").GetInt32());
            Assert.Equal(60, root.GetProperty("soHoaDon").GetInt32());
            Assert.Equal(45_000_000m, root.GetProperty("tongConNo").GetDecimal());
            Assert.Equal(50, root.GetProperty("soHoaDonHienThi").GetInt32());
            Assert.Equal(50, root.GetProperty("hoaDons").GetArrayLength());
            Assert.Contains("còn 10 hóa đơn", root.GetProperty("ghiChu").GetString());
        }

        [Fact]
        public async Task UnpaidInvoices_AtMost50_HasNoNote()
        {
            _store.UnpaidInvoices.Add(Invoice("HD1", 1_000_000m, 0m));

            var root = Parse(await _executor.ExecuteAsync(Admin, Call(AiToolNames.HoaDonChuaThanhToan)));

            Assert.Equal(JsonValueKind.Null, root.GetProperty("ghiChu").ValueKind);
            Assert.Equal(1, root.GetProperty("hoaDons").GetArrayLength());
        }

        [Fact]
        public async Task TenantLookup_MoreThan50_ListsFirst50_WithNote()
        {
            for (var i = 1; i <= 51; i++)
            {
                _store.TenantLookup.Add(new AiTenantLookupRow($"{i}", "Chi nhánh A", $"Khách {i}", "090", TrangThaiHopDong.DangHoatDong));
            }

            var root = Parse(await _executor.ExecuteAsync(Admin, Call(AiToolNames.ThongTinKhachThue, "{\"tuKhoa\":\"a\"}")));

            Assert.Equal(51, root.GetProperty("soKetQua").GetInt32());
            Assert.Equal(50, root.GetProperty("ketQua").GetArrayLength());
            Assert.Contains("còn 1 kết quả", root.GetProperty("ghiChu").GetString());
        }

        [Fact]
        public async Task TenantMeter_PeriodBeforeContractStart_ReturnsNoDataWithoutQuery()
        {
            // Bắt đầu 01/03/2026 giờ Việt Nam (28/02 17:00 UTC).
            _store.TenantContracts.Add(new AiTenantContractRow("HD1", 7, "101", "Chi nhánh A",
                DateTime.Parse("2026-02-28T17:00:00Z").ToUniversalTime(), null, 1m, 1m));

            var before = Parse(await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyChiSoDienNuoc, "{\"thang\":2,\"nam\":2026}")));
            Assert.Equal(0, before.GetProperty("soLuong").GetInt32());
            Assert.Equal("Kỳ này trước thời điểm bạn bắt đầu thuê phòng.", before.GetProperty("thongBao").GetString());
            Assert.Empty(_store.CallsOf("GetMeterReadingsAsync"));

            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyChiSoDienNuoc, "{\"thang\":3,\"nam\":2026}"));
            var call = _store.CallsOf("GetMeterReadingsAsync").Single();
            Assert.Equal(new[] { 7 }, (IEnumerable<int>)call.Args["phongTroIds"]!);
        }

        [Fact]
        public async Task TenantMeter_TwoContracts_OnlyRoomsStartedByThePeriod()
        {
            _store.TenantContracts.Add(new AiTenantContractRow("HD1", 7, "101", "Chi nhánh A", DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime(), null, 1m, 1m));
            _store.TenantContracts.Add(new AiTenantContractRow("HD2", 8, "102", "Chi nhánh A", DateTime.Parse("2026-06-01T00:00:00Z").ToUniversalTime(), null, 1m, 1m));

            await _executor.ExecuteAsync(Tenant, Call(AiToolNames.MyChiSoDienNuoc, "{\"thang\":4,\"nam\":2026}"));

            Assert.Equal(new[] { 7 }, (IEnumerable<int>)_store.CallsOf("GetMeterReadingsAsync").Single().Args["phongTroIds"]!);
        }

        [Theory]
        [InlineData("ten cong cu\nGIA MAO LOG")]
        [InlineData("a_very_long_tool_name_that_goes_on_and_on_and_on_and_on_and_on_and_on")]
        public async Task Logging_UnsafeToolName_IsNotWrittenToLog(string name)
        {
            await _executor.ExecuteAsync(Admin, Call(name));

            var all = string.Join("\n", _logger.Entries.Select(e => e.Message));
            Assert.DoesNotContain(name, all);
            Assert.Contains("(không hợp lệ)", all);
        }

        [Fact]
        public void Catalog_ManagerHasNineTools_TenantHasThree_AndTenantToolsHaveNoIdParameter()
        {
            Assert.Equal(9, AiToolCatalog.ForRole(AiCallerRole.Admin).Count);
            Assert.Equal(9, AiToolCatalog.ForRole(AiCallerRole.NhanVien).Count);
            var tenantTools = AiToolCatalog.ForRole(AiCallerRole.KhachThue);
            Assert.Equal(3, tenantTools.Count);
            Assert.DoesNotContain(tenantTools.SelectMany(t => t.Parameters), p => p.Name.Contains("Id", StringComparison.OrdinalIgnoreCase));
        }
    }
}
