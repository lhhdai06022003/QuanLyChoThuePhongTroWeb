using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Ba truy vấn đọc cho thông báo sau khi ghi sổ và chặn ghi trùng §5.4 (spec thanh toán trực tuyến §3.4, §5.4, §6.2, §6.3)
    // trên PostgreSQL thật: dữ liệu biên lai (đợt, lũy kế), dòng SePay hệ thống ghi sau mốc, hóa đơn thay thế.
    [Collection("PostgreSqlCollection")]
    public class InvoicePaymentNotificationStoreTests
    {
        private static readonly DateTime T0 = new(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);

        private readonly PostgreSqlFixture _fixture;

        public InvoicePaymentNotificationStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed record Seeded(
            int InvoiceId, int HopDongId, int ChiNhanhId, int StaffId, string TenChiNhanh, string TenPhong, string TenKhach, string Email, string MaHoaDon);

        private static string NewCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..28];

        // Chi nhánh, phòng, người thuê, hợp đồng, một nhân viên và một hóa đơn DaGui kỳ 09/2026.
        private async Task<Seeded> SeedInvoiceAsync(string prefix, decimal tongTien = 3_000_000m)
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{prefix}_{suffix}", MaChiNhanh = TestCodes.MaChiNhanh("N"), DiaChi = $"12 Duong {suffix}", SoDienThoai = "0912345678", MoTa = "test" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2000000m, DienTich = 25, MoTa = "test" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {suffix}", SoDienThoai = "0987654321", Email = $"khach_{suffix}@test.com", CCCD = $"01234567{suffix[..4]}" };
            var staff = new NguoiDung { TenDangNhap = $"staff_{prefix}_{suffix}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            context.PhongTros.Add(room);
            context.NguoiThues.Add(tenant);
            context.NguoiDungs.Add(staff);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-3),
                ThoiDiemKetThuc = DateTime.UtcNow.AddMonths(9),
                TienCocPhong = 2000000m,
                TienThuePhong = 2000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var invoice = NewInvoice(contract.HopDongId, $"HD-NS-{prefix}-{suffix}", tongTien);
            context.HoaDons.Add(invoice);
            await context.SaveChangesAsync();

            return new Seeded(invoice.HoaDonId, contract.HopDongId, branch.ChiNhanhId, staff.NguoiDungId, branch.TenChiNhanh, room.SoPhong, tenant.HoVaTen, tenant.Email, invoice.MaHoaDon);
        }

        private static HoaDon NewInvoice(int hopDongId, string maHoaDon, decimal tongTien, bool cancelled = false) => new()
        {
            MaHoaDon = maHoaDon,
            HopDongId = hopDongId,
            Thang = 9,
            Nam = 2026,
            TongTien = tongTien,
            TrangThaiPhatHanh = cancelled ? TrangThaiPhatHanhHoaDon.DaHuy : TrangThaiPhatHanhHoaDon.DaGui,
            TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
            NgayGui = DateTime.UtcNow,
            HanThanhToan = DateTime.UtcNow.AddDays(7),
            IsDeleted = cancelled
        };

        private async Task<int> AddLedgerAsync(
            int invoiceId, decimal soTien, string maGiaoDich, DateTime ngayThanhToan, PhuongThucThanhToan phuongThuc = PhuongThucThanhToan.ChuyenKhoan,
            bool isDeleted = false, DateTime? ngayXacNhan = null, int? nguoiXacNhanId = null, string ghiChu = "")
        {
            await using var context = _fixture.CreateDbContext();
            var row = new LichSuThanhToan
            {
                HoaDonId = invoiceId,
                MaGiaoDich = maGiaoDich,
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = ngayThanhToan,
                NgayXacNhan = ngayXacNhan,
                NguoiXacNhanId = nguoiXacNhanId,
                IsDeleted = isDeleted,
                GhiChu = ghiChu
            };
            context.LichSuThanhToans.Add(row);
            await context.SaveChangesAsync();
            return row.LichSuThanhToanId;
        }

        // ---------- GetReceiptSnapshotAsync ----------

        [Fact]
        public async Task Receipt_NumbersInstalmentsAndSumsUpToThisRow_SkippingVoidedRows_AndBreakingTiesById()
        {
            var seeded = await SeedInvoiceAsync("bien_lai");
            var r1 = await AddLedgerAsync(seeded.InvoiceId, 500_000m, NewCode("SEPAY-R1"), T0);
            var voided = await AddLedgerAsync(seeded.InvoiceId, 200_000m, NewCode("GD-HUY"), T0.AddHours(12), isDeleted: true);
            var r2 = await AddLedgerAsync(seeded.InvoiceId, 300_000m, NewCode("TM-R2"), T0.AddDays(1), PhuongThucThanhToan.TienMat);
            var r3a = await AddLedgerAsync(seeded.InvoiceId, 400_000m, NewCode("GD-R3A"), T0.AddDays(2));
            var r3b = await AddLedgerAsync(seeded.InvoiceId, 600_000m, NewCode("GD-R3B"), T0.AddDays(2));

            await using var ctx = _fixture.CreateDbContext();
            var store = new InvoicePaymentStore(ctx);

            var first = await store.GetReceiptSnapshotAsync(r1);
            var second = await store.GetReceiptSnapshotAsync(r2);
            var thirdA = await store.GetReceiptSnapshotAsync(r3a);
            var thirdB = await store.GetReceiptSnapshotAsync(r3b);
            var voidedRow = await store.GetReceiptSnapshotAsync(voided);

            Assert.Equal((1, 500_000m), (first!.DotThu, first.LuyKeDaThanhToan));
            Assert.Equal((2, 800_000m), (second!.DotThu, second.LuyKeDaThanhToan));
            Assert.Equal((3, 1_200_000m), (thirdA!.DotThu, thirdA.LuyKeDaThanhToan));
            Assert.Equal((4, 1_800_000m), (thirdB!.DotThu, thirdB.LuyKeDaThanhToan));
            Assert.False(first.IsDeleted);
            // Dòng đã hủy ghi nhận vẫn đọc được, nhưng chỉ tính các dòng chưa hủy đứng trước nó.
            Assert.True(voidedRow!.IsDeleted);
            Assert.Equal((1, 500_000m), (voidedRow.DotThu, voidedRow.LuyKeDaThanhToan));
        }

        [Fact]
        public async Task Receipt_CarriesTheBranchRoomTenantAndLedgerFields()
        {
            var seeded = await SeedInvoiceAsync("truong");
            var code = NewCode("SEPAY-FT");
            var id = await AddLedgerAsync(seeded.InvoiceId, 1_000_000m, code, T0, ghiChu: "[TIEN-THUA:5] [SEPAY] ghi chú", nguoiXacNhanId: null);

            await using var ctx = _fixture.CreateDbContext();
            var receipt = await new InvoicePaymentStore(ctx).GetReceiptSnapshotAsync(id);

            Assert.NotNull(receipt);
            Assert.Equal(id, receipt.LichSuThanhToanId);
            Assert.Equal(seeded.InvoiceId, receipt.HoaDonId);
            Assert.Equal(seeded.MaHoaDon, receipt.MaHoaDon);
            Assert.Equal(seeded.ChiNhanhId, receipt.ChiNhanhId);
            Assert.Equal(seeded.TenChiNhanh, receipt.TenChiNhanh);
            Assert.StartsWith("12 Duong ", receipt.DiaChiChiNhanh);
            Assert.Equal(seeded.TenPhong, receipt.TenPhong);
            Assert.Equal(seeded.TenKhach, receipt.TenKhachThue);
            Assert.Equal(seeded.Email, receipt.EmailKhachThue);
            Assert.Equal(9, receipt.Thang);
            Assert.Equal(2026, receipt.Nam);
            Assert.Equal(3_000_000m, receipt.TongTien);
            Assert.Equal(1_000_000m, receipt.SoTienThanhToan);
            Assert.Equal(T0, receipt.NgayThanhToanUtc);
            Assert.Equal(code, receipt.MaGiaoDich);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, receipt.PhuongThucThanhToan);
            Assert.Equal("[TIEN-THUA:5] [SEPAY] ghi chú", receipt.GhiChu);
        }

        [Fact]
        public async Task Receipt_UnknownLedgerRow_IsNull()
        {
            await using var ctx = _fixture.CreateDbContext();

            Assert.Null(await new InvoicePaymentStore(ctx).GetReceiptSnapshotAsync(int.MaxValue));
        }

        [Fact]
        public async Task Receipt_OnlyCountsRowsOfTheSameInvoice()
        {
            var a = await SeedInvoiceAsync("hd_a");
            var b = await SeedInvoiceAsync("hd_b");
            await AddLedgerAsync(a.InvoiceId, 700_000m, NewCode("GD-A"), T0.AddDays(-1));
            var inB = await AddLedgerAsync(b.InvoiceId, 100_000m, NewCode("GD-B"), T0);

            await using var ctx = _fixture.CreateDbContext();
            var receipt = await new InvoicePaymentStore(ctx).GetReceiptSnapshotAsync(inB);

            Assert.Equal((1, 100_000m), (receipt!.DotThu, receipt.LuyKeDaThanhToan));
        }

        // ---------- FindSystemSePayEntryRecordedAfterAsync ----------

        [Fact]
        public async Task SystemSePayRow_FilterIgnoresVoidedPersonConfirmedNonSePayAndEarlierRows()
        {
            var seeded = await SeedInvoiceAsync("loc");
            var since = T0;
            await AddLedgerAsync(seeded.InvoiceId, 1m, NewCode("SEPAY-TRUOC"), since.AddHours(-2), ngayXacNhan: since.AddHours(-1));
            await AddLedgerAsync(seeded.InvoiceId, 1m, NewCode("SEPAY-HUY"), since, ngayXacNhan: since.AddHours(1), isDeleted: true);
            await AddLedgerAsync(seeded.InvoiceId, 1m, NewCode("SEPAY-NGUOI"), since, ngayXacNhan: since.AddHours(1), nguoiXacNhanId: seeded.StaffId);
            await AddLedgerAsync(seeded.InvoiceId, 1m, NewCode("GD-TAY"), since, ngayXacNhan: since.AddHours(1));
            await AddLedgerAsync(seeded.InvoiceId, 1m, NewCode("SEPAY-CHUAXN"), since, ngayXacNhan: null);

            await using var ctx = _fixture.CreateDbContext();

            Assert.Null(await new InvoicePaymentStore(ctx).FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since));
        }

        [Fact]
        public async Task SystemSePayRow_ReturnsTheEarliestMatchingRow_AndTheBoundaryIsInclusive()
        {
            var seeded = await SeedInvoiceAsync("som_nhat");
            var since = T0;
            var late = NewCode("SEPAY-MUON");
            var early = NewCode("SEPAY-SOM");
            var atBoundary = NewCode("SEPAY-MOC");
            await AddLedgerAsync(seeded.InvoiceId, 1m, late, since, ngayXacNhan: since.AddHours(2));

            await using (var ctx = _fixture.CreateDbContext())
            {
                Assert.Equal(late, await new InvoicePaymentStore(ctx).FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since));
            }

            await AddLedgerAsync(seeded.InvoiceId, 1m, early, since, ngayXacNhan: since.AddHours(1));
            await using (var ctx = _fixture.CreateDbContext())
            {
                Assert.Equal(early, await new InvoicePaymentStore(ctx).FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since));
            }

            await AddLedgerAsync(seeded.InvoiceId, 1m, atBoundary, since, ngayXacNhan: since);
            await using (var ctx = _fixture.CreateDbContext())
            {
                var store = new InvoicePaymentStore(ctx);
                Assert.Equal(atBoundary, await store.FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since));
                // Mốc muộn hơn thì dòng đúng mốc không còn được tính.
                Assert.Equal(early, await store.FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since.AddMinutes(30)));
            }
        }

        [Fact]
        public async Task SystemSePayRow_OnTheSameInstant_ReturnsTheLowestId()
        {
            var seeded = await SeedInvoiceAsync("dong_gio");
            var first = NewCode("SEPAY-A");
            var second = NewCode("SEPAY-B");
            var instant = T0.AddHours(1);
            await AddLedgerAsync(seeded.InvoiceId, 1m, first, instant, ngayXacNhan: instant);
            await AddLedgerAsync(seeded.InvoiceId, 1m, second, instant, ngayXacNhan: instant);

            await using var ctx = _fixture.CreateDbContext();

            Assert.Equal(first, await new InvoicePaymentStore(ctx).FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, T0));
        }

        [Fact]
        public async Task SystemSePayRow_IsScopedToTheInvoice()
        {
            var a = await SeedInvoiceAsync("rieng_a");
            var b = await SeedInvoiceAsync("rieng_b");
            var inB = NewCode("SEPAY-CHI-B");
            await AddLedgerAsync(b.InvoiceId, 1m, inB, T0, ngayXacNhan: T0.AddHours(1));

            await using var ctx = _fixture.CreateDbContext();
            var store = new InvoicePaymentStore(ctx);

            Assert.Null(await store.FindSystemSePayEntryRecordedAfterAsync(a.InvoiceId, T0));
            Assert.Equal(inB, await store.FindSystemSePayEntryRecordedAfterAsync(b.InvoiceId, T0));
        }

        // ---------- GetTransferAlertContextAsync ----------

        [Fact]
        public async Task AlertContext_ForACancelledInvoice_NamesTheLiveReplacementOfTheSameContractAndPeriod()
        {
            var seeded = await SeedInvoiceAsync("thay_the");
            int cancelledId;
            int replacementId;
            string replacementCode;
            await using (var ctx = _fixture.CreateDbContext())
            {
                var cancelled = await ctx.HoaDons.SingleAsync(h => h.HoaDonId == seeded.InvoiceId);
                cancelled.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                cancelled.IsDeleted = true;
                // Lưu trước: unique index (HopDongId, Thang, Nam) chỉ cho một hóa đơn chưa xóa mỗi kỳ.
                await ctx.SaveChangesAsync();
                // Một bản đã hủy khác cùng kỳ: không được chọn làm bản thay thế.
                ctx.HoaDons.Add(NewInvoice(seeded.HopDongId, seeded.MaHoaDon + "-X", 3_000_000m, cancelled: true));
                replacementCode = seeded.MaHoaDon + "-R1";
                var replacement = NewInvoice(seeded.HopDongId, replacementCode, 3_000_000m);
                ctx.HoaDons.Add(replacement);
                await ctx.SaveChangesAsync();
                cancelledId = cancelled.HoaDonId;
                replacementId = replacement.HoaDonId;
            }

            await using var queryCtx = _fixture.CreateDbContext();
            var context = await new InvoicePaymentStore(queryCtx).GetTransferAlertContextAsync(cancelledId);

            Assert.NotNull(context);
            Assert.Equal(cancelledId, context.HoaDonId);
            Assert.Equal(seeded.MaHoaDon, context.MaHoaDon);
            Assert.Equal(seeded.ChiNhanhId, context.ChiNhanhId);
            Assert.Equal(replacementId, context.HoaDonThayTheId);
            Assert.Equal(replacementCode, context.MaHoaDonThayThe);
        }

        [Fact]
        public async Task AlertContext_ForACancelledInvoiceWithoutAReplacement_HasNone()
        {
            var seeded = await SeedInvoiceAsync("khong_thay");
            await using (var ctx = _fixture.CreateDbContext())
            {
                var invoice = await ctx.HoaDons.SingleAsync(h => h.HoaDonId == seeded.InvoiceId);
                invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                invoice.IsDeleted = true;
                await ctx.SaveChangesAsync();
            }

            await using var queryCtx = _fixture.CreateDbContext();
            var context = await new InvoicePaymentStore(queryCtx).GetTransferAlertContextAsync(seeded.InvoiceId);

            Assert.NotNull(context);
            Assert.Null(context.HoaDonThayTheId);
            Assert.Null(context.MaHoaDonThayThe);
        }

        [Fact]
        public async Task AlertContext_ForALiveInvoice_HasNoReplacement()
        {
            var seeded = await SeedInvoiceAsync("hoat_dong");
            await using (var ctx = _fixture.CreateDbContext())
            {
                // Hóa đơn khác cùng hợp đồng nhưng kỳ khác.
                var otherPeriod = NewInvoice(seeded.HopDongId, seeded.MaHoaDon + "-K8", 1m);
                otherPeriod.Thang = 8;
                ctx.HoaDons.Add(otherPeriod);
                await ctx.SaveChangesAsync();
            }

            await using var queryCtx = _fixture.CreateDbContext();
            var context = await new InvoicePaymentStore(queryCtx).GetTransferAlertContextAsync(seeded.InvoiceId);

            Assert.NotNull(context);
            Assert.Equal(seeded.ChiNhanhId, context.ChiNhanhId);
            Assert.Null(context.HoaDonThayTheId);
            Assert.Null(context.MaHoaDonThayThe);
        }

        [Fact]
        public async Task AlertContext_IgnoresReplacementCandidatesOfAnotherPeriod()
        {
            var seeded = await SeedInvoiceAsync("ky_khac");
            int cancelledId;
            await using (var ctx = _fixture.CreateDbContext())
            {
                var cancelled = await ctx.HoaDons.SingleAsync(h => h.HoaDonId == seeded.InvoiceId);
                cancelled.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                cancelled.IsDeleted = true;
                var otherPeriod = NewInvoice(seeded.HopDongId, seeded.MaHoaDon + "-K8", 1m);
                otherPeriod.Thang = 8;
                ctx.HoaDons.Add(otherPeriod);
                await ctx.SaveChangesAsync();
                cancelledId = cancelled.HoaDonId;
            }

            await using var queryCtx = _fixture.CreateDbContext();
            var context = await new InvoicePaymentStore(queryCtx).GetTransferAlertContextAsync(cancelledId);

            Assert.Null(context!.HoaDonThayTheId);
        }

        [Fact]
        public async Task AlertContext_UnknownInvoice_IsNull()
        {
            await using var ctx = _fixture.CreateDbContext();

            Assert.Null(await new InvoicePaymentStore(ctx).GetTransferAlertContextAsync(int.MaxValue));
        }
    }
}
