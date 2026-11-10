using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Lọc ngày của màn Lịch sử thanh toán theo ngày lịch Việt Nam (dữ liệu lưu UTC) và ô Cấn trừ cọc.
    [Collection("PostgreSqlCollection")]
    public class LichSuThanhToanStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public LichSuThanhToanStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static async Task AddLedgerAsync(ApplicationDbContext db, HoaDon hoaDon, decimal soTien, PhuongThucThanhToan phuongThuc, DateTime ngayUtc, string tag)
        {
            db.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = hoaDon.HoaDonId,
                MaGiaoDich = $"LS-{tag}-{Guid.NewGuid():N}",
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = ngayUtc
            });
            await db.SaveChangesAsync();
        }

        private static DataTableRequest Request() => new() { Draw = 1, Start = 0, Length = 50 };

        private static LichSuThanhToanLoc Loc(int chiNhanhId, DateTime? tu = null, DateTime? den = null, KenhThu? kenh = null) =>
            new() { ChiNhanhId = chiNhanhId, TuNgay = tu, DenNgay = den, Kenh = kenh };

        private static async Task<LichSuThanhToan> AddLedgerCodeAsync(ApplicationDbContext db, HoaDon hoaDon, string maGiaoDich, decimal soTien, PhuongThucThanhToan phuongThuc, DateTime ngayUtc, bool deleted = false)
        {
            var row = new LichSuThanhToan
            {
                HoaDonId = hoaDon.HoaDonId,
                MaGiaoDich = maGiaoDich,
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = ngayUtc,
                IsDeleted = deleted
            };
            db.LichSuThanhToans.Add(row);
            await db.SaveChangesAsync();
            return row;
        }

        [Fact]
        public async Task LocNgay_TheoNgayLichVn_CaThongKeVaDanhSach()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("L");
            var tenant = await seed.TenantAsync("L");
            var room = await seed.RoomAsync(branch, "L");
            var contract = await seed.ContractAsync(room, tenant);
            var invoice = await seed.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m, null, month: 3, year: 2003);

            // 16:59:59Z là 23:59:59 ngày 09/03 giờ VN; 17:00:00Z là 00:00:00 ngày 10/03.
            await AddLedgerAsync(db, invoice, 100m, PhuongThucThanhToan.TienMat, new DateTime(2003, 3, 9, 16, 59, 59, DateTimeKind.Utc), "A");
            await AddLedgerAsync(db, invoice, 200m, PhuongThucThanhToan.ChuyenKhoan, new DateTime(2003, 3, 9, 17, 0, 0, DateTimeKind.Utc), "B");

            var store = new LichSuThanhToanStore(db);
            var ngay9 = new DateTime(2003, 3, 9);
            var ngay10 = new DateTime(2003, 3, 10);

            var stats9 = await store.GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, ngay9, ngay9));
            Assert.Equal(1, stats9.TongSoGiaoDich);
            Assert.Equal(100m, stats9.TongDoanhThu);

            var stats10 = await store.GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, ngay10, ngay10));
            Assert.Equal(1, stats10.TongSoGiaoDich);
            Assert.Equal(200m, stats10.TongDoanhThu);

            var statsCa = await store.GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, ngay9, ngay10));
            Assert.Equal(2, statsCa.TongSoGiaoDich);

            var list9 = await store.GetDanhSachThanhToanAsync(Request(), Loc(branch.ChiNhanhId, ngay9, ngay9));
            Assert.Equal(new[] { 100m }, list9.data.Select(r => r.SoTienThanhToan).ToArray());
            var list10 = await store.GetDanhSachThanhToanAsync(Request(), Loc(branch.ChiNhanhId, ngay10, ngay10));
            Assert.Equal(new[] { 200m }, list10.data.Select(r => r.SoTienThanhToan).ToArray());
            var listCa = await store.GetDanhSachThanhToanAsync(Request(), Loc(branch.ChiNhanhId, ngay9, ngay10));
            Assert.Equal(2, listCa.recordsTotal);

            // Chỉ có ngày bắt đầu hoặc chỉ có ngày kết thúc.
            Assert.Equal(1, (await store.GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, ngay10, null))).TongSoGiaoDich);
            Assert.Equal(1, (await store.GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, null, ngay9))).TongSoGiaoDich);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task ThongKe_CoDongCanTru_TongBangTienMatCongChuyenKhoanCongCanTru()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("L");
            var tenant = await seed.TenantAsync("L");
            var contract = await seed.ContractAsync(await seed.RoomAsync(branch, "L"), tenant);
            var invoice = await seed.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m, null, month: 3, year: 2003);
            var ngay = new DateTime(2003, 3, 9, 3, 0, 0, DateTimeKind.Utc);

            await AddLedgerAsync(db, invoice, 100m, PhuongThucThanhToan.TienMat, ngay, "T");
            await AddLedgerAsync(db, invoice, 200m, PhuongThucThanhToan.ChuyenKhoan, ngay, "C");
            await AddLedgerAsync(db, invoice, 50m, PhuongThucThanhToan.CanTruTienCoc, ngay, "X");

            var stats = await new LichSuThanhToanStore(db).GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, null, null));

            Assert.Equal(50m, stats.DoanhThuCanTruCoc);
            Assert.Equal(350m, stats.TongDoanhThu);
            Assert.Equal(stats.DoanhThuTienMat + stats.DoanhThuChuyenKhoan + stats.DoanhThuSePay + stats.DoanhThuVnPay + stats.DoanhThuCanTruCoc, stats.TongDoanhThu);

            var rong = await new LichSuThanhToanStore(db).GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, new DateTime(2010, 1, 1), new DateTime(2010, 1, 1)));
            Assert.Equal(0m, rong.DoanhThuCanTruCoc);
            await tx.RollbackAsync();
        }

        // Test 6: ô "Chuyển khoản" không còn gồm SePay/VNPay; năm ô kênh cộng lại bằng "Tiền đã nhận"; bộ lọc kênh khớp PaymentChannels.Of.
        [Fact]
        public async Task Kenh_TachSePayVnPayKhoiChuyenKhoan_KhopPaymentChannels()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("K");
            var tenant = await seed.TenantAsync("K");
            var contract = await seed.ContractAsync(await seed.RoomAsync(branch, "K"), tenant);
            var invoice = await seed.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 9_000_000m, null, month: 3, year: 2003);
            var ngay = new DateTime(2003, 3, 9, 3, 0, 0, DateTimeKind.Utc);
            var g = Guid.NewGuid().ToString("N");

            var rows = new[]
            {
                await AddLedgerCodeAsync(db, invoice, $"TM-{g}", 1_000m, PhuongThucThanhToan.TienMat, ngay),
                await AddLedgerCodeAsync(db, invoice, $"CK-{g}", 2_000m, PhuongThucThanhToan.ChuyenKhoan, ngay),
                await AddLedgerCodeAsync(db, invoice, $"SEPAY-{g}", 4_000m, PhuongThucThanhToan.ChuyenKhoan, ngay),
                await AddLedgerCodeAsync(db, invoice, $"sepay-{g}", 8_000m, PhuongThucThanhToan.ChuyenKhoan, ngay),
                await AddLedgerCodeAsync(db, invoice, $"VNPAY-{g}", 16_000m, PhuongThucThanhToan.ChuyenKhoan, ngay),
                await AddLedgerCodeAsync(db, invoice, $"CANTRU-HD1-{g}", 32_000m, PhuongThucThanhToan.CanTruTienCoc, ngay),
                await AddLedgerCodeAsync(db, invoice, $"TM-DEL-{g}", 64_000m, PhuongThucThanhToan.TienMat, ngay, deleted: true)
            };

            decimal Expected(KenhThu kenh) => rows
                .Where(r => !r.IsDeleted && PaymentChannels.Of(r.MaGiaoDich, r.PhuongThucThanhToan) == kenh)
                .Sum(r => r.SoTienThanhToan);

            var store = new LichSuThanhToanStore(db);
            var stats = await store.GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId));

            Assert.Equal(Expected(KenhThu.TienMat), stats.DoanhThuTienMat);
            Assert.Equal(Expected(KenhThu.ChuyenKhoan), stats.DoanhThuChuyenKhoan);
            Assert.Equal(Expected(KenhThu.SePay), stats.DoanhThuSePay);
            Assert.Equal(Expected(KenhThu.VnPay), stats.DoanhThuVnPay);
            Assert.Equal(Expected(KenhThu.CanTruCoc), stats.DoanhThuCanTruCoc);
            Assert.Equal(1_000m, stats.DoanhThuTienMat);
            Assert.Equal(2_000m, stats.DoanhThuChuyenKhoan);
            Assert.Equal(12_000m, stats.DoanhThuSePay);
            Assert.Equal(16_000m, stats.DoanhThuVnPay);
            Assert.Equal(32_000m, stats.DoanhThuCanTruCoc);
            Assert.Equal(63_000m, stats.TongDoanhThu);
            Assert.Equal(stats.DoanhThuTienMat + stats.DoanhThuChuyenKhoan + stats.DoanhThuSePay + stats.DoanhThuVnPay + stats.DoanhThuCanTruCoc, stats.TongDoanhThu);

            foreach (var kenh in Enum.GetValues<KenhThu>())
            {
                var list = await store.GetDanhSachThanhToanAsync(Request(), Loc(branch.ChiNhanhId, kenh: kenh));
                var expectedCodes = rows
                    .Where(r => !r.IsDeleted && PaymentChannels.Of(r.MaGiaoDich, r.PhuongThucThanhToan) == kenh)
                    .Select(r => r.MaGiaoDich).OrderBy(x => x).ToArray();
                Assert.Equal(expectedCodes, list.data.Select(d => d.MaGiaoDich).OrderBy(x => x).ToArray());
            }

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task LocKy_ChiHoaDonDaGuiChuaXoa_BoQuaKhoangNgay()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("Y");
            var tenant = await seed.TenantAsync("Y");
            async Task<HoaDon> Inv(TrangThaiPhatHanhHoaDon issue, int month, bool deleted = false) =>
                await seed.InvoiceAsync(await seed.ContractAsync(await seed.RoomAsync(branch, "Y"), tenant), issue, TrangThaiHoaDon.ChuaThanhToan, 9_000m, null, deleted: deleted, month: month, year: 2003);

            var ngay = new DateTime(2003, 3, 9, 3, 0, 0, DateTimeKind.Utc);
            var kyBa = await Inv(TrangThaiPhatHanhHoaDon.DaGui, 3);
            var kyTu = await Inv(TrangThaiPhatHanhHoaDon.DaGui, 4);
            var nhap = await Inv(TrangThaiPhatHanhHoaDon.Nhap, 3);
            var choDuyet = await Inv(TrangThaiPhatHanhHoaDon.ChoDuyet, 3);
            var daChot = await Inv(TrangThaiPhatHanhHoaDon.DaChot, 3);
            var daHuy = await Inv(TrangThaiPhatHanhHoaDon.DaHuy, 3, deleted: true);
            var daGuiXoa = await Inv(TrangThaiPhatHanhHoaDon.DaGui, 3, deleted: true);

            // Ngày thanh toán cố tình nằm ngoài khoảng ngày sẽ lọc để chứng minh khoảng ngày bị bỏ qua khi có kỳ.
            await AddLedgerAsync(db, kyBa, 100m, PhuongThucThanhToan.TienMat, ngay, "K3a");
            await AddLedgerAsync(db, kyBa, 200m, PhuongThucThanhToan.CanTruTienCoc, ngay.AddMonths(2), "K3b");
            await AddLedgerCodeAsync(db, kyBa, $"SEPAY-K3c-{Guid.NewGuid():N}", 400m, PhuongThucThanhToan.ChuyenKhoan, ngay);
            await AddLedgerCodeAsync(db, kyBa, $"K3x-{Guid.NewGuid():N}", 777m, PhuongThucThanhToan.TienMat, ngay, deleted: true);
            await AddLedgerAsync(db, kyTu, 800m, PhuongThucThanhToan.TienMat, ngay, "K4");
            foreach (var inv in new[] { nhap, choDuyet, daChot, daHuy, daGuiXoa })
            {
                await AddLedgerAsync(db, inv, 5_000m, PhuongThucThanhToan.TienMat, ngay, "KX");
            }

            var store = new LichSuThanhToanStore(db);
            var loc = new LichSuThanhToanLoc
            {
                ChiNhanhId = branch.ChiNhanhId,
                KyThang = 3,
                KyNam = 2003,
                TuNgay = new DateTime(2010, 1, 1),
                DenNgay = new DateTime(2010, 1, 2)
            };

            var stats = await store.GetThongKeThanhToanAsync(loc);
            Assert.Equal(700m, stats.TongDoanhThu);
            Assert.Equal(3, stats.TongSoGiaoDich);
            Assert.Equal(200m, stats.DoanhThuCanTruCoc);
            Assert.Equal(400m, stats.DoanhThuSePay);

            var list = await store.GetDanhSachThanhToanAsync(Request(), loc);
            Assert.Equal(700m, list.data.Sum(r => r.SoTienThanhToan));
            Assert.Equal(3, list.recordsTotal);

            // Không có kỳ thì khoảng ngày xa như trên cho kết quả rỗng.
            var khongKy = await store.GetThongKeThanhToanAsync(Loc(branch.ChiNhanhId, new DateTime(2010, 1, 1), new DateTime(2010, 1, 2)));
            Assert.Equal(0, khongKy.TongSoGiaoDich);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task LocNguoiThu_TheoIdVaHeThong()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("N");
            var tenant = await seed.TenantAsync("N");
            var invoice = await seed.InvoiceAsync(await seed.ContractAsync(await seed.RoomAsync(branch, "N"), tenant), TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 9_000m, null, month: 3, year: 2003);
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var u1 = new NguoiDung { TenDangNhap = $"nt1_{suffix}", MatKhauHash = "x", Role = Role.NhanVien, IsActive = true };
            var u2 = new NguoiDung { TenDangNhap = $"nt2_{suffix}", MatKhauHash = "x", Role = Role.NhanVien, IsActive = true };
            db.NguoiDungs.AddRange(u1, u2);
            await db.SaveChangesAsync();
            var ngay = new DateTime(2003, 3, 9, 3, 0, 0, DateTimeKind.Utc);

            async Task Them(decimal soTien, int? nguoiId, string tag)
            {
                db.LichSuThanhToans.Add(new LichSuThanhToan
                {
                    HoaDonId = invoice.HoaDonId,
                    MaGiaoDich = $"NT-{tag}-{Guid.NewGuid():N}",
                    SoTienThanhToan = soTien,
                    PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                    NgayThanhToan = ngay,
                    NguoiXacNhanId = nguoiId
                });
                await db.SaveChangesAsync();
            }

            await Them(100m, u1.NguoiDungId, "a");
            await Them(200m, u2.NguoiDungId, "b");
            await Them(400m, null, "c");

            var store = new LichSuThanhToanStore(db);
            LichSuThanhToanLoc Nguoi(NguoiThuLoai loai, int? id = null) => new() { ChiNhanhId = branch.ChiNhanhId, NguoiThu = loai, NguoiThuId = id };

            Assert.Equal(100m, (await store.GetThongKeThanhToanAsync(Nguoi(NguoiThuLoai.NguoiDung, u1.NguoiDungId))).TongDoanhThu);
            Assert.Equal(200m, (await store.GetThongKeThanhToanAsync(Nguoi(NguoiThuLoai.NguoiDung, u2.NguoiDungId))).TongDoanhThu);
            Assert.Equal(400m, (await store.GetThongKeThanhToanAsync(Nguoi(NguoiThuLoai.HeThong))).TongDoanhThu);
            Assert.Equal(700m, (await store.GetThongKeThanhToanAsync(Nguoi(NguoiThuLoai.TatCa))).TongDoanhThu);

            var listU1 = await store.GetDanhSachThanhToanAsync(Request(), Nguoi(NguoiThuLoai.NguoiDung, u1.NguoiDungId));
            Assert.Equal(new[] { 100m }, listU1.data.Select(r => r.SoTienThanhToan).ToArray());
            var listHeThong = await store.GetDanhSachThanhToanAsync(Request(), Nguoi(NguoiThuLoai.HeThong));
            Assert.Equal(new[] { 400m }, listHeThong.data.Select(r => r.SoTienThanhToan).ToArray());
            Assert.Equal("Hệ thống", listHeThong.data.Single().NguoiXacNhan);
            await tx.RollbackAsync();
        }
    }
}
