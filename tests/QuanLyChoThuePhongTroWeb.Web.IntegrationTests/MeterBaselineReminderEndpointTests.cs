using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Nhắc truy thu và đơn giá kỳ trên chi tiết hóa đơn phía quản lý (/HoaDon/GetById): dùng ở sửa nháp, chi tiết và bước Admin chốt.
    [Collection(WebTestCollection.Name)]
    public class MeterBaselineReminderEndpointTests
    {
        private const string DienNote = "[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 0; không có số cuối đồng hồ cũ; lý do: Thay đồng hồ";
        private const string LostNoteSentenceDien = "Kỳ này có đổi đồng hồ hoặc quay vòng điện (không còn ghi chú xác nhận). Kiểm tra đã thêm dòng truy thu nếu cần thu.";

        private readonly CustomWebApplicationFactory _factory;

        public MeterBaselineReminderEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Kỳ 10/2026 của phòng A: điện Cũ dienCu → Mới 85,5; nước Cũ nuocCu → Mới 60. Kỳ 09 (nếu có) kết thúc ở prevDienMoi / prevNuocMoi.
        private async Task<(BranchScopeWorld World, HoaDon Invoice)> SeedInvoiceAsync(
            TrangThaiPhatHanhHoaDon status,
            bool withPeriod = true,
            decimal? prevDienMoi = 4520m,
            decimal? prevNuocMoi = 50m,
            decimal dienCu = 0m,
            decimal nuocCu = 50m,
            string? dienNote = DienNote,
            string? nuocNote = null,
            bool prevDeleted = false)
        {
            var w = await SeedAsync(_factory);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            int? periodId = null;
            if (withPeriod)
            {
                if (prevDienMoi.HasValue || prevNuocMoi.HasValue)
                {
                    db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                    {
                        PhongTroId = w.RoomA, Thang = 9, Nam = 2026,
                        ChiSoDienCu = 0, ChiSoDienMoi = prevDienMoi ?? 0, ChiSoNuocCu = 0, ChiSoNuocMoi = prevNuocMoi ?? 0,
                        DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet, IsDeleted = prevDeleted
                    });
                }

                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = w.RoomA, Thang = 10, Nam = 2026,
                    ChiSoDienCu = dienCu, ChiSoDienMoi = dienCu + 85.5m, ChiSoNuocCu = nuocCu, ChiSoNuocMoi = 60,
                    DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                };
                db.DichVuDienNuocCuaPhongs.Add(period);
                await db.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var staff = await db.NguoiDungs.AsNoTracking().FirstAsync(u => u.TenDangNhap == w.Staff);
                foreach (var (loai, note, value) in new[] { (LoaiDongHo.Dien, dienNote, 85.5m), (LoaiDongHo.Nuoc, nuocNote, 60m) })
                {
                    if (note == null) continue;
                    db.AnhChiSoDongHos.Add(new AnhChiSoDongHo
                    {
                        DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                        LoaiDongHo = loai,
                        Url = $"https://example.test/{s}-{loai}.jpg",
                        TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                        GiaTriXacNhan = value,
                        NguoiXacNhanId = staff.NguoiDungId,
                        NgayXacNhan = DateTime.UtcNow,
                        GhiChuXacNhan = note,
                        DuocChonLamChiSoChinhThuc = true
                    });
                }
            }

            var invoice = new HoaDon
            {
                HopDongId = w.ContractA, MaHoaDon = $"HD-RM-{s}", Thang = 10, Nam = 2026,
                TongTien = 3_270_000m,
                TrangThaiPhatHanh = status, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = status == TrangThaiPhatHanhHoaDon.DaGui ? DateTime.UtcNow.AddHours(-1) : null,
                HanPhanHoi = status == TrangThaiPhatHanhHoaDon.DaGui ? DateTime.UtcNow.AddHours(24) : null,
                DichVuDienNuocCuaPhongId = periodId, NgayTao = DateTime.UtcNow,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new() { TenDichVu = "Tiền phòng", DonGia = 3_000_000m, SoLuong = 1, TongTien = 3_000_000m },
                    new() { TenDichVu = "Tiền điện", DonGia = 3500m, SoLuong = 85.5m, TongTien = 299_250m }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            return (w, invoice);
        }

        private async Task<JsonElement> GetDetailAsync(string username, int hoaDonId)
        {
            var (client, _) = await LoginAsync(_factory, username);
            var res = await client.GetAsync($"/HoaDon/GetById/{hoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return await JsonAsync(res);
        }

        private static List<string> Reminders(JsonElement detail)
            => detail.GetProperty("nhacTruyThus").EnumerateArray().Select(x => x.GetString()!).ToList();

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaGui)]
        public async Task Staff_GetById_ChangedElectricBaseline_ReturnsReminderWithNoteAndPeriodPrices(TrangThaiPhatHanhHoaDon status)
        {
            var (w, invoice) = await SeedInvoiceAsync(status);

            var detail = await GetDetailAsync(w.Staff, invoice.HoaDonId);

            var reminders = Reminders(detail);
            var nhac = Assert.Single(reminders);
            Assert.Equal($"Kỳ này có đổi đồng hồ điện: {DienNote}. Kiểm tra đã thêm dòng truy thu nếu cần thu.", nhac);
            Assert.Equal(3500m, detail.GetProperty("donGiaDienKy").GetDecimal());
            Assert.Equal(20000m, detail.GetProperty("donGiaNuocKy").GetDecimal());
        }

        [Fact]
        public async Task Admin_GetById_ChoDuyet_ReturnsReminder_ForTheFinalizeStep()
        {
            var (w, invoice) = await SeedInvoiceAsync(TrangThaiPhatHanhHoaDon.ChoDuyet);

            var detail = await GetDetailAsync(w.Admin, invoice.HoaDonId);

            Assert.Single(Reminders(detail));
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, detail.GetProperty("trangThaiPhatHanhValue").GetInt32());
        }

        [Fact]
        public async Task Staff_GetById_WhenConfirmNoteIsGone_ReturnsGenericSentence()
        {
            // Ghi chú xác nhận bị ghi đè sau khi sửa số (lựa chọn B2 a): kỳ vẫn là đổi mốc nên nhắc vẫn hiện.
            var (w, invoice) = await SeedInvoiceAsync(TrangThaiPhatHanhHoaDon.Nhap, dienNote: "[Sửa 80 → 85,5] Đọc lại số");

            var detail = await GetDetailAsync(w.Staff, invoice.HoaDonId);

            Assert.Equal(LostNoteSentenceDien, Assert.Single(Reminders(detail)));
        }

        [Fact]
        public async Task Staff_GetById_WhenNoOfficialImageAtAll_ReturnsGenericSentence()
        {
            // Duyệt kỳ nhập tay hủy ảnh chính thức: không còn ghi chú nhưng kỳ vẫn đổi mốc.
            var (w, invoice) = await SeedInvoiceAsync(TrangThaiPhatHanhHoaDon.ChoDuyet, dienNote: null);

            var detail = await GetDetailAsync(w.Staff, invoice.HoaDonId);

            Assert.Equal(LostNoteSentenceDien, Assert.Single(Reminders(detail)));
        }

        [Fact]
        public async Task Staff_GetById_BothMetersChanged_ListsElectricThenWater()
        {
            var (w, invoice) = await SeedInvoiceAsync(
                TrangThaiPhatHanhHoaDon.Nhap, prevNuocMoi: 9990m, nuocCu: 0m,
                nuocNote: "[QUAY_VONG] Chỉ số cũ 9.990; trần 10.000; phần trước mốc 10 m³; lý do: Quay vòng");

            var reminders = Reminders(await GetDetailAsync(w.Staff, invoice.HoaDonId));

            Assert.Equal(2, reminders.Count);
            Assert.StartsWith("Kỳ này có đổi đồng hồ điện:", reminders[0]);
            Assert.StartsWith("Kỳ này có quay vòng nước: [QUAY_VONG]", reminders[1]);
        }

        [Fact]
        public async Task Staff_GetById_BaselineEqualsPreviousNew_HasNoReminderButKeepsPeriodPrices()
        {
            var (w, invoice) = await SeedInvoiceAsync(TrangThaiPhatHanhHoaDon.Nhap, prevDienMoi: 4520m, dienCu: 4520m, dienNote: null);

            var detail = await GetDetailAsync(w.Staff, invoice.HoaDonId);

            Assert.Empty(Reminders(detail));
            Assert.Equal(3500m, detail.GetProperty("donGiaDienKy").GetDecimal());
        }

        [Fact]
        public async Task Staff_GetById_FirstEverPeriod_HasNoReminder_ButOffersPeriodPrices()
        {
            // B4: kỳ đầu tiên đổi đồng hồ có ghi chú nhưng không có nhắc.
            var (w, invoice) = await SeedInvoiceAsync(TrangThaiPhatHanhHoaDon.Nhap, prevDienMoi: null, prevNuocMoi: null);

            var detail = await GetDetailAsync(w.Staff, invoice.HoaDonId);

            Assert.Empty(Reminders(detail));
            Assert.Equal(3500m, detail.GetProperty("donGiaDienKy").GetDecimal());
            Assert.Equal(20000m, detail.GetProperty("donGiaNuocKy").GetDecimal());
        }

        [Fact]
        public async Task Staff_GetById_PreviousPeriodSoftDeleted_IsTreatedAsFirstPeriod()
        {
            var (w, invoice) = await SeedInvoiceAsync(TrangThaiPhatHanhHoaDon.Nhap, prevDeleted: true);

            var detail = await GetDetailAsync(w.Staff, invoice.HoaDonId);

            Assert.Empty(Reminders(detail));
        }

        [Fact]
        public async Task InvoicePage_ShipsTheTruyThuButtonsAndReminderContainers_ForEditDetailAndFinalize()
        {
            var w = await SeedAsync(_factory);
            var (staff, _) = await LoginAsync(_factory, w.Staff, "/QuanLyNhaTro/QuanLyHoaDon");

            var res = await staff.GetAsync("/QuanLyNhaTro/QuanLyHoaDon");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
            Assert.Contains("id=\"editNhacTruyThu\"", html);
            Assert.Contains("id=\"btnAddTruyThuDien\"", html);
            Assert.Contains("id=\"btnAddTruyThuNuoc\"", html);
            Assert.Contains("Truy thu điện đồng hồ cũ", html);
            Assert.Contains("Truy thu nước đồng hồ cũ", html);
            // Chi tiết và bước Admin chốt đọc cùng trường nhắc từ /HoaDon/GetById.
            Assert.Contains("res.nhacTruyThus", html);
            Assert.Contains("confirmChot(res && res.nhacTruyThus)", html);
        }

        [Fact]
        public async Task Staff_GetById_InvoiceWithoutMeterPeriod_HasNoReminderAndNoPeriodPrices()
        {
            var (w, invoice) = await SeedInvoiceAsync(TrangThaiPhatHanhHoaDon.Nhap, withPeriod: false);

            var detail = await GetDetailAsync(w.Staff, invoice.HoaDonId);

            Assert.Empty(Reminders(detail));
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("donGiaDienKy").ValueKind);
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("donGiaNuocKy").ValueKind);
        }
    }
}
