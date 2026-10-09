using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Truy vấn của Trung tâm việc cần làm: lọc chi nhánh, xóa mềm, trạng thái, biên thời gian.
    // DB dùng chung: luôn lọc theo chi nhánh vừa seed, không assert tổng toàn hệ thống.
    [Collection("PostgreSqlCollection")]
    public class ViecCanLamStoreTests
    {
        private static readonly DateTime Now = new(2003, 6, 15, 5, 0, 0, DateTimeKind.Utc);
        // Hôm nay theo giờ VN (15/06/2003) bắt đầu lúc 14/06 17:00 UTC.
        private static readonly DateTime Tu = new(2003, 6, 14, 17, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Den = Tu.AddDays(31);
        private static readonly DateTime Gap = Tu.AddDays(8);

        private readonly PostgreSqlFixture _fixture;

        public ViecCanLamStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class World
        {
            public DebtTestSeeder Seed = null!;
            public ChiNhanh A = null!;
            public ChiNhanh B = null!;
            public HashSet<int> RoomsA = new();
            public HashSet<int> RoomsB = new();
            public HashSet<int> ActiveRoomsA = new();
            public PhongTro MeterRoom1 = null!;
            public Dictionary<string, HoaDon> Inv = new();
        }

        private static async Task<World> SeedAsync(ApplicationDbContext db)
        {
            var seed = new DebtTestSeeder(db);
            var w = new World { Seed = seed };
            w.A = await seed.BranchAsync("A");
            w.B = await seed.BranchAsync("B");
            var tenA = await seed.TenantAsync("A");
            var tenB = await seed.TenantAsync("B");

            async Task<(PhongTro Room, HopDong Contract)> Ctr(ChiNhanh b, NguoiThue t, DateTime? end, TrangThaiHopDong st = TrangThaiHopDong.DangHoatDong, bool deleted = false, DateTime? start = null)
            {
                var room = await seed.RoomAsync(b, b == w.A ? "A" : "B");
                (b == w.A ? w.RoomsA : w.RoomsB).Add(room.PhongTroId);
                var c = await seed.ContractAsync(room, t, start, end, st, deleted);
                if (b == w.A && st == TrangThaiHopDong.DangHoatDong && !deleted)
                {
                    w.ActiveRoomsA.Add(room.PhongTroId);
                }

                return (room, c);
            }

            // Hợp đồng chủ của hóa đơn / sự cố (không có ngày kết thúc nên không vào KH3).
            var (_, ctrA) = await Ctr(w.A, tenA, null, start: new DateTime(2002, 3, 10, 0, 0, 0, DateTimeKind.Utc));
            var (_, ctrB) = await Ctr(w.B, tenB, null);

            // KH3: hợp đồng đang hoạt động theo ngày kết thúc.
            await Ctr(w.A, tenA, Tu.AddSeconds(-1));      // ngay trước cửa sổ: không đếm
            await Ctr(w.A, tenA, Tu);                     // đầu cửa sổ: đếm, gấp
            await Ctr(w.A, tenA, Gap.AddSeconds(-1));     // đếm, gấp
            await Ctr(w.A, tenA, Gap);                    // đếm, không gấp
            await Ctr(w.A, tenA, Den.AddSeconds(-1));     // đếm, không gấp
            await Ctr(w.A, tenA, Den);                    // đúng mốc cuối: không đếm
            await Ctr(w.A, tenA, Tu.AddDays(1), deleted: true);                          // xóa mềm
            await Ctr(w.A, tenA, Tu.AddDays(1), TrangThaiHopDong.DaKetThuc);            // không còn hoạt động
            await Ctr(w.B, tenB, Tu.AddDays(1));          // chi nhánh B

            // Hóa đơn chi nhánh A.
            async Task<HoaDon> Inv(string key, TrangThaiPhatHanhHoaDon issue, TrangThaiHoaDon pay, decimal total, DateTime? due = null, bool deleted = false, decimal paid = 0, decimal paidDeleted = 0, int? month = null, int? year = null)
            {
                var h = await seed.InvoiceAsync(ctrA, issue, pay, total, due, deleted, paid, paidDeleted, month, year);
                w.Inv[key] = h;
                return h;
            }

            const TrangThaiHoaDon Chua = TrangThaiHoaDon.ChuaThanhToan;
            await Inv("nhap1", TrangThaiPhatHanhHoaDon.Nhap, Chua, 100_000m, month: 3, year: 2001);
            await Inv("nhap2", TrangThaiPhatHanhHoaDon.Nhap, Chua, 100_000m, month: 3, year: 2002);
            await Inv("nhapDel", TrangThaiPhatHanhHoaDon.Nhap, Chua, 100_000m, deleted: true, month: 3, year: 1999);
            await Inv("cd1", TrangThaiPhatHanhHoaDon.ChoDuyet, Chua, 100_000m);
            await Inv("cd2", TrangThaiPhatHanhHoaDon.ChoDuyet, Chua, 100_000m);
            await Inv("cdDel", TrangThaiPhatHanhHoaDon.ChoDuyet, Chua, 100_000m, deleted: true);
            await Inv("dc1", TrangThaiPhatHanhHoaDon.DaChot, Chua, 100_000m);
            await Inv("dc2", TrangThaiPhatHanhHoaDon.DaChot, TrangThaiHoaDon.ThanhToanMotPhan, 100_000m);
            await Inv("dcPaid", TrangThaiPhatHanhHoaDon.DaChot, TrangThaiHoaDon.DaThanhToan, 100_000m);
            await Inv("dcDel", TrangThaiPhatHanhHoaDon.DaChot, Chua, 100_000m, deleted: true);

            // TT2 (quá hạn), tiền còn nợ = tổng - ledger chưa xóa.
            await Inv("over1", TrangThaiPhatHanhHoaDon.DaGui, Chua, 5_000_000m, Now.AddDays(-1), paid: 1_000_000m, paidDeleted: 700_000m); // 4.000.000
            await Inv("over2", TrangThaiPhatHanhHoaDon.DaGui, Chua, 2_000_000m, Now.AddDays(-3));                                          // 2.000.000
            await Inv("overPart", TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m, Now.AddDays(-2), paid: 400_000m); // 600.000
            // Không phải quá hạn.
            await Inv("overPaid", TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 900_000m, Now.AddDays(-5));
            await Inv("overNhap", TrangThaiPhatHanhHoaDon.Nhap, Chua, 900_000m, Now.AddDays(-5), month: 4, year: 2000);
            await Inv("overCd", TrangThaiPhatHanhHoaDon.ChoDuyet, Chua, 900_000m, Now.AddDays(-5));
            await Inv("overDc", TrangThaiPhatHanhHoaDon.DaChot, Chua, 900_000m, Now.AddDays(-5));
            await Inv("overDel", TrangThaiPhatHanhHoaDon.DaGui, Chua, 900_000m, Now.AddDays(-5), deleted: true);
            await Inv("overHuy", TrangThaiPhatHanhHoaDon.DaHuy, Chua, 900_000m, Now.AddDays(-5), deleted: true);
            // TT3 (đã gửi, chưa tới hạn).
            await Inv("edge", TrangThaiPhatHanhHoaDon.DaGui, Chua, 300_000m, Now);                 // đúng nowUtc
            await Inv("future", TrangThaiPhatHanhHoaDon.DaGui, Chua, 200_000m, Now.AddDays(2));
            await Inv("noDue", TrangThaiPhatHanhHoaDon.DaGui, Chua, 100_000m, null);

            // TT1: yêu cầu thanh toán và minh chứng.
            var ycOk = await Inv("ycOk", TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 10m);
            var ycDelInvoice = await Inv("ycDelInv", TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 10m, deleted: true);
            var k = 0;
            async Task Proof(HoaDon h, TrangThaiMinhChungThanhToan st, bool proofDeleted = false, bool requestDeleted = false)
            {
                k++;
                var yc = new YeuCauThanhToanHoaDon
                {
                    HoaDonId = h.HoaDonId,
                    MaYeuCau = $"VY{seed.Suffix}{seed.Next()}",
                    SoTien = 10m,
                    NoiDungChuyenKhoan = "x",
                    TrangThai = TrangThaiYeuCauThanhToan.DangDoiChieu,
                    IsDeleted = requestDeleted
                };
                db.YeuCauThanhToanHoaDons.Add(yc);
                await db.SaveChangesAsync();
                db.MinhChungThanhToanHoaDons.Add(new MinhChungThanhToanHoaDon
                {
                    YeuCauThanhToanHoaDonId = yc.YeuCauThanhToanHoaDonId,
                    HinhAnhUrl = $"http://x/{k}.jpg",
                    SoTienKhaiBao = 10m,
                    NgayChuyenKhaiBao = Now,
                    TrangThaiDoiChieu = st,
                    IsDeleted = proofDeleted
                });
                await db.SaveChangesAsync();
            }

            await Proof(ycOk, TrangThaiMinhChungThanhToan.ChoXacNhan);
            await Proof(ycOk, TrangThaiMinhChungThanhToan.ChoXacNhan);
            await Proof(ycOk, TrangThaiMinhChungThanhToan.DaXacNhan);
            await Proof(ycOk, TrangThaiMinhChungThanhToan.TuChoi);
            await Proof(ycOk, TrangThaiMinhChungThanhToan.ChoXacNhan, proofDeleted: true);
            await Proof(ycOk, TrangThaiMinhChungThanhToan.ChoXacNhan, requestDeleted: true);
            await Proof(ycDelInvoice, TrangThaiMinhChungThanhToan.ChoXacNhan);

            // Sự cố (KH1, KH2).
            async Task Incident(HopDong c, TrangThaiSuCo st, DateTime sent, bool deleted = false)
            {
                db.YeuCauSuCos.Add(new YeuCauSuCo
                {
                    PhongTroId = c.PhongTroId,
                    NguoiThueId = c.NguoiThueId,
                    TieuDe = "x",
                    MoTa = "x",
                    TrangThai = st,
                    NgayGui = sent,
                    IsDeleted = deleted
                });
                await db.SaveChangesAsync();
            }

            await Incident(ctrA, TrangThaiSuCo.ChoTiepNhan, Now.AddHours(-30));                  // gấp
            await Incident(ctrA, TrangThaiSuCo.ChoTiepNhan, Now.AddHours(-24));                  // đúng 24 giờ: gấp
            await Incident(ctrA, TrangThaiSuCo.ChoTiepNhan, Now.AddHours(-1));                   // chưa gấp
            await Incident(ctrA, TrangThaiSuCo.ChoTiepNhan, Now.AddHours(-48), deleted: true);   // xóa mềm
            await Incident(ctrA, TrangThaiSuCo.DaHoanThanh, Now.AddHours(-48));                  // khác trạng thái
            await Incident(ctrA, TrangThaiSuCo.DangXuLy, Now.AddDays(-8));                       // KH2
            await Incident(ctrA, TrangThaiSuCo.DangXuLy, Now.AddDays(-7));                       // đúng 7 ngày: tính
            await Incident(ctrA, TrangThaiSuCo.DangXuLy, Now.AddDays(-6));                       // chưa tới
            await Incident(ctrA, TrangThaiSuCo.DangXuLy, Now.AddDays(-20), deleted: true);       // xóa mềm
            await Incident(ctrA, TrangThaiSuCo.ChoTiepNhan, Now.AddDays(-20), deleted: false);   // thêm 1 vào KH1 (gấp)

            // Chi nhánh B: mỗi loại việc một bản ghi.
            await seed.InvoiceAsync(ctrB, TrangThaiPhatHanhHoaDon.Nhap, Chua, 1m, null);
            await seed.InvoiceAsync(ctrB, TrangThaiPhatHanhHoaDon.ChoDuyet, Chua, 1m, null);
            await seed.InvoiceAsync(ctrB, TrangThaiPhatHanhHoaDon.DaChot, Chua, 1m, null);
            await seed.InvoiceAsync(ctrB, TrangThaiPhatHanhHoaDon.DaGui, Chua, 7_000_000m, Now.AddDays(-4));
            await seed.InvoiceAsync(ctrB, TrangThaiPhatHanhHoaDon.DaGui, Chua, 1_000m, Now.AddDays(4));
            var bYc = await seed.InvoiceAsync(ctrB, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 1m, null);
            await Proof(bYc, TrangThaiMinhChungThanhToan.ChoXacNhan);
            await Incident(ctrB, TrangThaiSuCo.ChoTiepNhan, Now.AddDays(-2));
            await Incident(ctrB, TrangThaiSuCo.DangXuLy, Now.AddDays(-10));

            // Kỳ chỉ số và ảnh.
            DichVuDienNuocCuaPhong Dv(PhongTro r, int thang, int nam, bool deleted = false, TrangThaiGhiNhan trangThai = TrangThaiGhiNhan.DaDuyet) => new()
            {
                PhongTroId = r.PhongTroId, Thang = thang, Nam = nam, ChiSoDienCu = 1, ChiSoDienMoi = 2, DonGiaDien = 1,
                ChiSoNuocCu = 1, ChiSoNuocMoi = 2, DonGiaNuoc = 1, TrangThaiGhiNhan = trangThai, IsDeleted = deleted
            };

            var m1 = await seed.RoomAsync(w.A, "M");
            var m2 = await seed.RoomAsync(w.A, "M");
            var mB = await seed.RoomAsync(w.B, "M");
            w.RoomsA.Add(m1.PhongTroId);
            w.RoomsA.Add(m2.PhongTroId);
            w.RoomsB.Add(mB.PhongTroId);
            w.MeterRoom1 = m1;
            var dv42002 = Dv(m1, 12, 2002);
            var dv4 = Dv(m1, 4, 2003);
            var dv5 = Dv(m1, 5, 2003);
            var dv2Jan = Dv(m2, 1, 2004);
            var dvDel = Dv(m2, 5, 2003, deleted: true);
            var dvB = Dv(mB, 6, 2003);
            // Kỳ chưa chốt (chưa DaDuyet): CS1 không được tính là đã chốt.
            var dvNhap = Dv(m2, 3, 2004, trangThai: TrangThaiGhiNhan.Nhap);
            var dvChoDuyet = Dv(m2, 7, 2003, trangThai: TrangThaiGhiNhan.ChoDuyet);
            var dvTuChoi = Dv(m2, 8, 2003, trangThai: TrangThaiGhiNhan.TuChoi);
            db.DichVuDienNuocCuaPhongs.AddRange(dv42002, dv4, dv5, dv2Jan, dvDel, dvB, dvNhap, dvChoDuyet, dvTuChoi);
            await db.SaveChangesAsync();

            AnhChiSoDongHo Img(DichVuDienNuocCuaPhong dv, TrangThaiXuLyAnhChiSo st, bool deleted = false) => new()
            {
                DichVuDienNuocCuaPhongId = dv.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "http://x/img.jpg",
                TrangThaiXuLy = st,
                IsDeleted = deleted
            };

            db.AnhChiSoDongHos.AddRange(
                Img(dv4, TrangThaiXuLyAnhChiSo.DocDuoc),
                Img(dv4, TrangThaiXuLyAnhChiSo.KhongDocDuoc),
                Img(dv4, TrangThaiXuLyAnhChiSo.Loi),
                Img(dv4, TrangThaiXuLyAnhChiSo.MoiTaiLen),
                Img(dv4, TrangThaiXuLyAnhChiSo.DangXuLy),
                Img(dv4, TrangThaiXuLyAnhChiSo.CanChupLai),
                Img(dv4, TrangThaiXuLyAnhChiSo.DaXacNhan),
                Img(dv4, TrangThaiXuLyAnhChiSo.DaThayThe),
                Img(dv4, TrangThaiXuLyAnhChiSo.DocDuoc, deleted: true),
                Img(dv5, TrangThaiXuLyAnhChiSo.DocDuoc),
                Img(dvDel, TrangThaiXuLyAnhChiSo.DocDuoc),
                Img(dvB, TrangThaiXuLyAnhChiSo.DocDuoc));
            await db.SaveChangesAsync();

            return w;
        }

        private static ViecCanLamStore Store(ApplicationDbContext db) => new(db);

        // 1. CS1

        [Fact]
        public async Task HopDongDangHoatDong_OnlyActiveNotDeletedOfBranch()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var rows = await Store(db).GetHopDongDangHoatDongAsync(w.A.ChiNhanhId, null);

            Assert.Equal(w.ActiveRoomsA.OrderBy(x => x), rows.Select(r => r.PhongTroId).OrderBy(x => x));
            Assert.All(rows, r =>
            {
                Assert.Equal(w.A.ChiNhanhId, r.ChiNhanhId);
                Assert.Equal(w.A.TenChiNhanh, r.TenChiNhanh);
            });
            Assert.Contains(rows, r => r.ThoiDiemBatDau == new DateTime(2002, 3, 10, 0, 0, 0, DateTimeKind.Utc));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task KyChiSoDaChot_FiltersByBranchPeriodStatusAndSoftDelete()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = Store(db);

            var rows = await store.GetKyChiSoDaChotAsync(w.A.ChiNhanhId, null, 5, 2003);

            // Giữ (5/2003) và (1/2004); loại 12/2002, 4/2003 (trước kỳ đầu), bản ghi xóa mềm 5/2003 của phòng khác,
            // và các kỳ chưa chốt (Nhap 3/2004, ChoDuyet 7/2003, TuChoi 8/2003).
            var got = rows.Select(r => (r.Thang, r.Nam)).OrderBy(x => x.Nam).ThenBy(x => x.Thang).ToArray();
            Assert.Equal(new[] { (5, 2003), (1, 2004) }, got);
            Assert.DoesNotContain(rows, r => r.PhongTroId == w.RoomsB.First());

            // Chi nhánh B không lẫn vào A (H5).
            var rowsB = await store.GetKyChiSoDaChotAsync(w.B.ChiNhanhId, null, 1, 2003);
            var rowB = Assert.Single(rowsB);
            Assert.Equal(6, rowB.Thang);
            Assert.Equal(2003, rowB.Nam);

            await tx.RollbackAsync();
        }

        // 2. CS2

        [Fact]
        public async Task AnhChiSoChoXacNhan_CountsOnlyReviewableStatuses_GroupedByPeriod()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var rows = await Store(db).DemAnhChiSoChoXacNhanAsync(w.A.ChiNhanhId, null);

            var got = rows.OrderBy(r => r.Thang).Select(r => (r.Thang, r.Nam, r.SoLuong)).ToArray();
            // 4/2003: DocDuoc + KhongDocDuoc + Loi = 3 (MoiTaiLen, DangXuLy, CanChupLai, DaXacNhan, DaThayThe, ảnh xóa mềm bị loại)
            // 5/2003: 1 ảnh. Kỳ đã xóa mềm không tính.
            Assert.Equal(new[] { (4, 2003, 3), (5, 2003, 1) }, got);
            Assert.All(rows, r => Assert.Equal(w.A.ChiNhanhId, r.ChiNhanhId));

            await tx.RollbackAsync();
        }

        // 3. HD1, HD2, HD3

        [Fact]
        public async Task HoaDonNhap_CountsDraftsNotDeleted_WithOldestYear()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemHoaDonNhapAsync(w.A.ChiNhanhId, null));

            Assert.Equal(w.A.ChiNhanhId, row.ChiNhanhId);
            Assert.Equal(w.A.TenChiNhanh, row.TenChiNhanh);
            // nhap1, nhap2, overNhap (năm 2000). nhapDel (1999) đã xóa mềm không tính.
            Assert.Equal(3, row.SoLuong);
            Assert.Equal(2000, row.NamCuNhat);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HoaDonChoDuyet_CountsPendingNotDeleted()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemHoaDonChoDuyetAsync(w.A.ChiNhanhId, null));

            Assert.Equal(3, row.SoLuong); // cd1, cd2, overCd; cdDel xóa mềm
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HoaDonDaChot_ExcludesPaidAndDeleted()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemHoaDonDaChotChuaGuiAsync(w.A.ChiNhanhId, null));

            Assert.Equal(3, row.SoLuong); // dc1, dc2, overDc; dcPaid đã thanh toán, dcDel xóa mềm
            await tx.RollbackAsync();
        }

        // 4. TT1

        [Fact]
        public async Task MinhChungChoDoiChieu_ExcludesDecidedAndSoftDeletedChain()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemMinhChungChoDoiChieuAsync(w.A.ChiNhanhId, null));

            // 2 minh chứng hợp lệ; DaXacNhan, TuChoi, minh chứng xóa mềm, yêu cầu xóa mềm, hóa đơn xóa mềm bị loại.
            Assert.Equal(2, row.SoLuong);
            Assert.Equal(w.A.ChiNhanhId, row.ChiNhanhId);
            await tx.RollbackAsync();
        }

        // 5. TT2, TT3

        [Fact]
        public async Task HoaDonQuaHan_OnlySentUnpaidPastDue_WithRemainingDebt()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemHoaDonQuaHanAsync(w.A.ChiNhanhId, null, Now));

            Assert.Equal(3, row.SoLuong); // over1, over2, overPart
            Assert.Equal(4_000_000m + 2_000_000m + 600_000m, row.TongConNo); // ledger xóa mềm (700.000) không trừ
            Assert.Equal(new[] { w.Inv["over1"].Nam, w.Inv["over2"].Nam, w.Inv["overPart"].Nam }.Min(), row.NamCuNhat);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HoaDonChuaToiHan_IncludesBoundaryFutureAndNoDueDate()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemHoaDonDaGuiChuaToiHanAsync(w.A.ChiNhanhId, null, Now));

            Assert.Equal(3, row.SoLuong); // edge (HanThanhToan == nowUtc), future, noDue
            Assert.Equal(600_000m, row.TongConNo);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task DueDateEqualToNow_IsNotOverdue_ButNotYetDue()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = Store(db);

            // Quá hạn khi now = đúng hạn của "edge" thì không tính; lùi now thêm 1 micro giây thì tính.
            var overAtNow = Assert.Single(await store.DemHoaDonQuaHanAsync(w.A.ChiNhanhId, null, Now));
            var overJustAfter = Assert.Single(await store.DemHoaDonQuaHanAsync(w.A.ChiNhanhId, null, Now.AddTicks(10)));

            Assert.Equal(3, overAtNow.SoLuong);
            Assert.Equal(4, overJustAfter.SoLuong);
            Assert.Equal(300_000m, overJustAfter.TongConNo - overAtNow.TongConNo);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task BangQuaHan_SortedByDueDate_RespectsLimit_AndIgnoresDeletedLedger()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = Store(db);

            var all = await store.GetHoaDonQuaHanAsync(w.A.ChiNhanhId, null, Now, 50);
            var limited = await store.GetHoaDonQuaHanAsync(w.A.ChiNhanhId, null, Now, 2);

            Assert.Equal(new[] { w.Inv["over2"].MaHoaDon, w.Inv["overPart"].MaHoaDon, w.Inv["over1"].MaHoaDon }, all.Select(r => r.MaHoaDon).ToArray());
            Assert.Equal(new[] { w.Inv["over2"].MaHoaDon, w.Inv["overPart"].MaHoaDon }, limited.Select(r => r.MaHoaDon).ToArray());

            var over1 = all.Single(r => r.MaHoaDon == w.Inv["over1"].MaHoaDon);
            Assert.Equal(5_000_000m, over1.TongTien);
            Assert.Equal(1_000_000m, over1.DaThu);
            Assert.Equal(Now.AddDays(-1), over1.HanThanhToan);
            Assert.Equal(w.A.TenChiNhanh, over1.TenChiNhanh);
            Assert.Equal($"Vc Khach A {w.Seed.Suffix}", over1.KhachThue);
            Assert.False(string.IsNullOrEmpty(over1.SoPhong));

            await tx.RollbackAsync();
        }

        // 6. KH1, KH2, KH3

        [Fact]
        public async Task SuCoChoTiepNhan_CountsAndUrgentCount()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            // mốc gấp = Now - 24h: sự cố 30h, đúng 24h, 20 ngày đều gấp; 1h chưa gấp. Xóa mềm và trạng thái khác bị loại.
            var row = Assert.Single(await Store(db).DemSuCoChoTiepNhanAsync(w.A.ChiNhanhId, null, Now.AddHours(-24)));

            Assert.Equal(4, row.SoLuong);
            Assert.Equal(3, row.SoLuongGap);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task SuCoXuLyQuaLau_UsesInclusiveThreshold()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemSuCoXuLyQuaLauAsync(w.A.ChiNhanhId, null, Now.AddDays(-7)));

            Assert.Equal(2, row.SoLuong); // 8 ngày và đúng 7 ngày
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HopDongSapHetHan_WindowIsHalfOpen_AndUrgentCountIsSubset()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var row = Assert.Single(await Store(db).DemHopDongSapHetHanAsync(w.A.ChiNhanhId, null, Tu, Den, Gap));

            Assert.Equal(4, row.SoLuong);     // Tu, Gap-1s, Gap, Den-1s
            Assert.Equal(2, row.SoLuongGap);  // Tu, Gap-1s
            await tx.RollbackAsync();
        }

        // 7. Phạm vi chi nhánh

        private static async Task<List<(string Name, int Count, int[]? Branches)>> RunAllAsync(
            ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed)
        {
            var list = new List<(string, int, int[]?)>();
            void Add(string n, IEnumerable<int> branchIds) { var a = branchIds.ToArray(); list.Add((n, a.Length, a)); }

            Add("CS1", (await s.GetHopDongDangHoatDongAsync(branchId, allowed)).Select(r => r.ChiNhanhId));
            var ky = await s.GetKyChiSoDaChotAsync(branchId, allowed, 1, 2003);
            list.Add(("CS1-ky", ky.Count, null));
            Add("CS2", (await s.DemAnhChiSoChoXacNhanAsync(branchId, allowed)).Select(r => r.ChiNhanhId));
            Add("HD1", (await s.DemHoaDonNhapAsync(branchId, allowed)).Select(r => r.ChiNhanhId));
            Add("HD2", (await s.DemHoaDonChoDuyetAsync(branchId, allowed)).Select(r => r.ChiNhanhId));
            Add("HD3", (await s.DemHoaDonDaChotChuaGuiAsync(branchId, allowed)).Select(r => r.ChiNhanhId));
            Add("TT1", (await s.DemMinhChungChoDoiChieuAsync(branchId, allowed)).Select(r => r.ChiNhanhId));
            Add("TT2", (await s.DemHoaDonQuaHanAsync(branchId, allowed, Now)).Select(r => r.ChiNhanhId));
            Add("TT3", (await s.DemHoaDonDaGuiChuaToiHanAsync(branchId, allowed, Now)).Select(r => r.ChiNhanhId));
            Add("KH1", (await s.DemSuCoChoTiepNhanAsync(branchId, allowed, Now.AddHours(-24))).Select(r => r.ChiNhanhId));
            Add("KH2", (await s.DemSuCoXuLyQuaLauAsync(branchId, allowed, Now.AddDays(-7))).Select(r => r.ChiNhanhId));
            Add("KH3", (await s.DemHopDongSapHetHanAsync(branchId, allowed, Tu, Den, Gap)).Select(r => r.ChiNhanhId));
            Add("Bang", (await s.GetHoaDonQuaHanAsync(branchId, allowed, Now, 50)).Select(r => r.ChiNhanhId));
            return list;
        }

        [Fact]
        public async Task AllowedBranchB_EveryMethodReturnsOnlyB_AndNeverA()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = Store(db);

            var results = await RunAllAsync(store, null, new[] { w.B.ChiNhanhId });

            foreach (var (name, count, branches) in results)
            {
                Assert.True(count > 0, $"{name} phải thấy dữ liệu chi nhánh B");
                if (branches != null)
                {
                    Assert.DoesNotContain(w.A.ChiNhanhId, branches);
                    Assert.Contains(w.B.ChiNhanhId, branches);
                }
            }

            var ky = await store.GetKyChiSoDaChotAsync(null, new[] { w.B.ChiNhanhId }, 1, 2003);
            Assert.DoesNotContain(ky, r => w.RoomsA.Contains(r.PhongTroId));
            Assert.Contains(ky, r => w.RoomsB.Contains(r.PhongTroId));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task BranchFilterA_EveryMethodReturnsOnlyA()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = Store(db);

            var results = await RunAllAsync(store, w.A.ChiNhanhId, null);

            foreach (var (name, count, branches) in results)
            {
                Assert.True(count > 0, $"{name} phải thấy dữ liệu chi nhánh A");
                if (branches != null)
                {
                    Assert.All(branches, id => Assert.Equal(w.A.ChiNhanhId, id));
                }
            }

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task BranchOutsideAllowed_EveryMethodReturnsEmpty()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var results = await RunAllAsync(Store(db), w.A.ChiNhanhId, new[] { w.B.ChiNhanhId });

            Assert.All(results, r => Assert.True(r.Count == 0, $"{r.Name} phải rỗng khi chi nhánh ngoài phạm vi"));
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task EmptyAllowed_EveryMethodReturnsEmpty()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            await SeedAsync(db);

            var results = await RunAllAsync(Store(db), null, Array.Empty<int>());

            Assert.All(results, r => Assert.True(r.Count == 0, $"{r.Name} phải rỗng khi chưa phân công"));
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task AdminUnrestricted_SeesBothBranches()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var rows = await Store(db).DemHoaDonQuaHanAsync(null, null, Now);

            Assert.Contains(rows, r => r.ChiNhanhId == w.A.ChiNhanhId);
            Assert.Contains(rows, r => r.ChiNhanhId == w.B.ChiNhanhId);
            Assert.Equal(7_000_000m, rows.Single(r => r.ChiNhanhId == w.B.ChiNhanhId).TongConNo);
            await tx.RollbackAsync();
        }
    }
}
