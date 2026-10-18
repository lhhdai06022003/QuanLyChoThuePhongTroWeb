using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Ghi sổ hóa đơn từ giao dịch SePay (spec thanh toán trực tuyến §5) trên store giả.
    public class InvoiceTransferHandlerTests
    {
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly PaymentTestHarness _h = new();
        private readonly InvoiceTransferHandler _handler;

        public InvoiceTransferHandlerTests()
        {
            _handler = new InvoiceTransferHandler(_h.Transaction);
        }

        private static IncomingBankTransfer Transfer(
            decimal amount = 2_000_000m, string? reference = "FT26ABC", string? content = "TTPTHD7",
            string? gateway = "MBBank", long id = 92704) => new(
                id, gateway, TransferTime, "0123456789", null, null, content, "in", amount, reference, content);

        private Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code = "TTPTHD7")
            => _handler.HandleAsync(transfer, code);

        // ---------- Ghi sổ ----------

        [Fact]
        public async Task FullAmount_RecordsLedgerLikeAdmin_AndPaysInvoice()
        {
            var hoaDon = _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            var ledger = Assert.Single(_h.Store.Ledger);
            Assert.Equal("SEPAY-FT26ABC", ledger.MaGiaoDich);
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, ledger.PhuongThucThanhToan);
            Assert.Null(ledger.NguoiXacNhanId);
            Assert.Equal(TransferTime, ledger.NgayThanhToan);
            Assert.Equal(Now, ledger.NgayXacNhan);
            Assert.StartsWith("[SEPAY] MBBank · REF FT26ABC · ND: TTPTHD7", ledger.GhiChu);
            Assert.False(ledger.IsDeleted);
            Assert.Null(ledger.MinhChungThanhToanHoaDonId);

            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            var history = Assert.Single(hoaDon.LichSuTrangThaiHoaDons);
            Assert.StartsWith("[TU_DONG] SePay ghi nhận 2.000.000đ", history.LyDo);
            Assert.Equal("[TU_DONG] SePay ghi nhận 2.000.000đ, REF FT26ABC", history.LyDo);
            Assert.Null(history.NguoiThucHienId);
            Assert.Equal(Now, history.NgayThucHien);
            Assert.True(_h.LastTx.Committed);

            Assert.Equal(7, outcome.HoaDonId);
            Assert.Equal("HD-7", outcome.MaHoaDon);
            Assert.Equal("TTPTHD7", outcome.Code);
            Assert.Equal(ledger.LichSuThanhToanId, outcome.LichSuThanhToanId);
            Assert.True(outcome.LichSuThanhToanId > 0);
            Assert.Equal(2_000_000m, outcome.SoTienGhiNhan);
            Assert.Equal(0m, outcome.TienThua);
            Assert.Equal(2_000_000m, outcome.TongTien);
            Assert.Equal(2_000_000m, outcome.DaThu);
            Assert.Equal(0m, outcome.ConLai);
            Assert.True(outcome.DaThanhToanDu);
            Assert.Equal("Đã thanh toán", outcome.TrangThaiThanhToan);
            Assert.Null(outcome.MaLuotDaHuy);
            Assert.False(outcome.ConMinhChungChoDoiChieu);
            Assert.Equal("SEPAY-FT26ABC", outcome.DedupKey);
        }

        [Fact]
        public async Task PartialAmount_IsRecorded_EvenWhenInvoiceDoesNotAllowPartialPayment()
        {
            var hoaDon = _h.AddSentInvoice(choPhepMotPhan: false);

            var outcome = await HandleAsync(Transfer(amount: 1_500_000m));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(1_500_000m, Assert.Single(_h.Store.Ledger).SoTienThanhToan);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, hoaDon.TrangThaiHoaDon);
            Assert.Equal(500_000m, outcome.ConLai);
            Assert.Equal(1_500_000m, outcome.DaThu);
            Assert.False(outcome.DaThanhToanDu);
            Assert.Equal("Thanh toán một phần", outcome.TrangThaiThanhToan);
            Assert.Equal("[TU_DONG] SePay ghi nhận 1.500.000đ, REF FT26ABC", hoaDon.LichSuTrangThaiHoaDons.Single().LyDo);
        }

        [Fact]
        public async Task PartialAmountBelowMinimum_IsStillRecorded()
        {
            _h.AddSentInvoice(choPhepMotPhan: true, toiThieu: 1_000_000m);

            var outcome = await HandleAsync(Transfer(amount: 100_000m));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(100_000m, Assert.Single(_h.Store.Ledger).SoTienThanhToan);
        }

        [Fact]
        public async Task TwoPartialPayments_CompleteTheInvoice()
        {
            var hoaDon = _h.AddSentInvoice();

            var first = await HandleAsync(Transfer(amount: 1_200_000m, reference: "FT-A"));
            var second = await HandleAsync(Transfer(amount: 800_000m, reference: "FT-B", id: 92705));

            Assert.Equal(TransferOutcomeKind.Recorded, first.Kind);
            Assert.Equal(TransferOutcomeKind.Recorded, second.Kind);
            Assert.True(second.DaThanhToanDu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.Equal(new[] { "SEPAY-FT-A", "SEPAY-FT-B" }, _h.Store.Ledger.Select(l => l.MaGiaoDich).ToArray());
            Assert.Equal(
                new[] { "[TU_DONG] SePay ghi nhận 1.200.000đ, REF FT-A", "[TU_DONG] SePay ghi nhận 800.000đ, REF FT-B" },
                hoaDon.LichSuTrangThaiHoaDons.OrderBy(l => l.NgayThucHien).Select(l => l.LyDo).ToArray());
        }

        [Fact]
        public async Task Overpayment_RecordsTheDebtOnly_AndTagsTheSurplus()
        {
            var hoaDon = _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer(amount: 2_500_000m));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            var ledger = Assert.Single(_h.Store.Ledger);
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.StartsWith("[TIEN-THUA:500000] [SEPAY]", ledger.GhiChu);
            Assert.Equal(500_000m, PaymentNoteTags.Parse(ledger.GhiChu).TienThua);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.Equal(2_000_000m, outcome.SoTienGhiNhan);
            Assert.Equal(500_000m, outcome.TienThua);
            Assert.Equal(0m, outcome.ConLai);
            Assert.StartsWith("[TU_DONG] SePay ghi nhận 2.000.000đ", hoaDon.LichSuTrangThaiHoaDons.Single().LyDo);
        }

        [Fact]
        public async Task PaymentWithPreviousLedger_UsesTheRemainingAmount()
        {
            var hoaDon = _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m);

            var outcome = await HandleAsync(Transfer(amount: 1_700_000m));

            Assert.Equal(1_500_000m, outcome.SoTienGhiNhan);
            Assert.Equal(200_000m, outcome.TienThua);
            Assert.Equal(2_000_000m, outcome.DaThu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
        }

        // ---------- Thông báo trong app (spec §6.4) ----------

        private const string ExpectedTitle = "Đã nhận chuyển khoản 2.000.000đ — P101";
        private const string StaffLink = "/QuanLyNhaTro/QuanLyHoaDon?hoaDonId=7";
        private const string TenantLink = "/KhachThue/HoaDon?hoaDonId=7";

        [Fact]
        public async Task Recorded_NotifiesAdmin_BranchStaff_AndTenant_ButNotStaffOfAnotherBranch()
        {
            _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(
                new[] { PaymentTestHarness.AdminId, PaymentTestHarness.StaffId, PaymentTestHarness.TenantUserId }.OrderBy(id => id),
                _h.Store.Notifications.Select(n => n.NguoiDungId!.Value).OrderBy(id => id));
            Assert.All(_h.Store.Notifications, n =>
            {
                Assert.Equal(ExpectedTitle, n.TieuDe);
                Assert.Equal("HoaDon", n.LinhVuc);
                Assert.Equal(Now, n.CreatedAt);
                Assert.False(n.IsRead);
            });

            var admin = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.AdminId);
            var staff = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId);
            var tenant = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
            const string staffContent = "Hóa đơn HD-7: ghi nhận 2.000.000đ qua chuyển khoản tự động (REF FT26ABC), đã thu 2.000.000/2.000.000đ, còn lại 0đ.";
            Assert.Equal(staffContent, admin.NoiDung);
            Assert.Equal(staffContent, staff.NoiDung);
            Assert.Equal(StaffLink, admin.LinkDieuHuong);
            Assert.Equal(StaffLink, staff.LinkDieuHuong);
            Assert.Equal("Hóa đơn HD-7: đã ghi nhận 2.000.000đ, còn lại 0đ.", tenant.NoiDung);
            Assert.Equal(TenantLink, tenant.LinkDieuHuong);
        }

        [Fact]
        public async Task Recorded_ReportsTheTenantUserId_ForTheCustomerPanel()
        {
            _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(PaymentTestHarness.TenantUserId, outcome.TenantUserId);
        }

        [Fact]
        public async Task Recorded_TenantWithoutAnActiveAccount_GetsNoNotification_AndNoTenantUserId()
        {
            var hoaDon = _h.AddSentInvoice();
            _h.Store.TenantUserIdByHopDong.Remove(hoaDon.HopDongId);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Null(outcome.TenantUserId);
            Assert.Equal(2, _h.Store.Notifications.Count);
            Assert.DoesNotContain(_h.Store.Notifications, n => n.LinkDieuHuong == TenantLink);
        }

        [Fact]
        public async Task Recorded_PushesEachNotificationOverSignalR_AfterCommit()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer());

            Assert.True(_h.LastTx.Committed);
            Assert.Equal(
                new[] { PaymentTestHarness.AdminId, PaymentTestHarness.StaffId, PaymentTestHarness.TenantUserId }.OrderBy(id => id),
                _h.Notifier.Sent.Select(s => s.UserId).OrderBy(id => id));
        }

        [Fact]
        public async Task Recorded_UsesTheAmountReceived_InTheTitle_AndTheRecordedAmount_InTheContent()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m);

            await HandleAsync(Transfer(amount: 1_700_000m));

            Assert.All(_h.Store.Notifications, n => Assert.Equal("Đã nhận chuyển khoản 1.700.000đ — P101", n.TieuDe));
            var staff = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId);
            Assert.Contains("ghi nhận 1.500.000đ", staff.NoiDung);
            Assert.Contains("đã thu 2.000.000/2.000.000đ", staff.NoiDung);
        }

        [Fact]
        public async Task PartialPayment_ContentShowsWhatIsStillOwed()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer(amount: 1_500_000m));

            var staff = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId);
            Assert.Contains("đã thu 1.500.000/2.000.000đ, còn lại 500.000đ.", staff.NoiDung);
            var tenant = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
            Assert.Equal("Hóa đơn HD-7: đã ghi nhận 1.500.000đ, còn lại 500.000đ.", tenant.NoiDung);
        }

        [Fact]
        public async Task Overpayment_OnlyAdminSeesTheSurplusSentence()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer(amount: 2_500_000m));

            var admin = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.AdminId);
            var staff = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId);
            var tenant = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
            Assert.EndsWith(" Khách chuyển dư 500.000đ, cần xử lý ngoài hệ thống.", admin.NoiDung);
            Assert.DoesNotContain("Khách chuyển dư", staff.NoiDung);
            Assert.DoesNotContain("Khách chuyển dư", tenant.NoiDung);
            Assert.All(_h.Store.Notifications, n => Assert.Equal("Đã nhận chuyển khoản 2.500.000đ — P101", n.TieuDe));
        }

        [Fact]
        public async Task RequestUnderReview_AddsTheCheckForDuplicateSentence_ForStaffAndAdmin_NotForTheTenant()
        {
            _h.AddSentInvoice();
            _h.AddProof(_h.AddRequest());

            await HandleAsync(Transfer());

            const string sentence = "Hóa đơn còn minh chứng chờ đối chiếu, kiểm tra trùng trước khi xác nhận.";
            Assert.Contains(sentence, Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.AdminId).NoiDung);
            Assert.Contains(sentence, Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId).NoiDung);
            Assert.DoesNotContain("kiểm tra trùng", Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId).NoiDung);
        }

        [Fact]
        public async Task LongRoomName_KeepsTheTitleWithinTheColumnLimit()
        {
            _h.AddSentInvoice();
            _h.Store.Meta[7] = _h.Store.Meta[7] with { TenPhong = new string('P', 400) };

            await HandleAsync(Transfer());

            Assert.All(_h.Store.Notifications, n => Assert.Equal(255, n.TieuDe.Length));
        }

        [Fact]
        public async Task NonRecordedOutcomes_CreateNoNotification()
        {
            _h.AddSentInvoice();
            _h.AddSentInvoice(hoaDonId: 8).TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot;
            _h.AddSentInvoice(hoaDonId: 9);
            _h.AddLedger(9, 2_000_000m);

            var notPayable = await HandleAsync(Transfer(reference: "FT-NP"), "TTPTHD8");
            var alreadyPaid = await HandleAsync(Transfer(reference: "FT-AP"), "TTPTHD9");
            var missing = await HandleAsync(Transfer(reference: "FT-MS"), "TTPTHD99");
            _h.AddSePayLedger(7, 500_000m, "FT-DUP", Now.AddDays(-1));
            var duplicate = await HandleAsync(Transfer(reference: "FT-DUP"));

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, notPayable.Kind);
            Assert.Equal(TransferOutcomeKind.AlreadyPaid, alreadyPaid.Kind);
            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, missing.Kind);
            Assert.Equal(TransferOutcomeKind.Duplicate, duplicate.Kind);
            Assert.Empty(_h.Store.Notifications);
            Assert.Empty(_h.Notifier.Sent);
            Assert.All(new[] { notPayable, alreadyPaid, missing, duplicate }, o => Assert.Null(o.TenantUserId));
        }

        // Thông báo trong app nằm trong transaction ghi sổ: lưu thông báo hỏng thì rollback cả sổ,
        // không đẩy SignalR và không báo khách có tiền vào (store giả không hoàn tác dữ liệu nên kiểm qua commit/rollback và đẩy tin).
        [Fact]
        public async Task WhenSavingTheNotificationsFails_TheTransactionRollsBack_AndNothingIsPushed()
        {
            _h.AddSentInvoice();
            // Lần lưu 1: dòng sổ (ApplyLedgerChange). Lần lưu 2: thông báo, ngay trước commit.
            _h.UnitOfWork.FailOnSave = n => n == 2 ? new InvalidOperationException("SECRET-DB-DETAIL") : null;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            Assert.Equal(7, outcome.HoaDonId);
            Assert.Equal("Đã có lỗi hệ thống, vui lòng thử lại.", outcome.LyDo);
            Assert.Null(outcome.TenantUserId);
            Assert.Null(outcome.LichSuThanhToanId);
            Assert.Equal(2, _h.UnitOfWork.SaveChangesCalls);
            Assert.True(_h.LastTx.RolledBack);
            Assert.False(_h.LastTx.Committed);
            Assert.Empty(_h.Notifier.Sent);
            Assert.Empty(_h.Notifier.PaymentUpdates);
        }

        [Fact]
        public async Task AfterAFailedAttempt_TheSameTransferCanBeRecordedOnRetry_AndNotifiesOnce()
        {
            _h.AddSentInvoice();
            _h.UnitOfWork.FailOnSave = n => n == 2 ? new InvalidOperationException("boom") : null;
            var failed = await HandleAsync(Transfer());
            _h.UnitOfWork.FailOnSave = null;
            // Store giả giữ thực thể đã thêm dù rollback; dọn như DB thật sẽ làm.
            _h.Store.Ledger.Clear();
            _h.Store.Notifications.Clear();
            _h.Notifier.Sent.Clear();

            var retried = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Failed, failed.Kind);
            Assert.Equal(TransferOutcomeKind.Recorded, retried.Kind);
            Assert.Equal(PaymentTestHarness.TenantUserId, retried.TenantUserId);
            Assert.Equal(3, _h.Store.Notifications.Count);
            Assert.Equal(3, _h.Notifier.Sent.Count);
        }

        [Fact]
        public async Task Recorded_NotificationsAreDeliveredOnlyAfterTheCommit()
        {
            _h.AddSentInvoice();
            var committedWhenPushed = new System.Collections.Generic.List<bool>();
            _h.Notifier.OnSend = () => committedWhenPushed.Add(_h.LastTx.Committed);

            await HandleAsync(Transfer());

            Assert.Equal(3, committedWhenPushed.Count);
            Assert.All(committedWhenPushed, Assert.True);
        }

        // ---------- Không ghi ----------

        [Fact]
        public async Task AlreadyPaid_IsNotRecordedAgain()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.AlreadyPaid, outcome.Kind);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(7, outcome.HoaDonId);
            Assert.Equal(2_000_000m, outcome.TongTien);
            Assert.Equal(2_000_000m, outcome.DaThu);
            Assert.Equal(0m, outcome.ConLai);
            Assert.False(outcome.DaThanhToanDu);
        }

        [Fact]
        public async Task CancelledInvoice_IsNotPayable()
        {
            var hoaDon = _h.AddSentInvoice();
            hoaDon.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            hoaDon.IsDeleted = true;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal("Hóa đơn đã hủy", outcome.LyDo);
            Assert.Equal(7, outcome.HoaDonId);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task DeletedInvoice_IsNotPayable_EvenIfStillSent()
        {
            _h.AddSentInvoice().IsDeleted = true;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Empty(_h.Store.Ledger);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        public async Task UnpublishedInvoice_IsNotPayable(TrangThaiPhatHanhHoaDon status)
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = status;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal("Hóa đơn chưa công bố", outcome.LyDo);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task InvoiceWithFractionalTotal_IsNotPayable()
        {
            _h.AddSentInvoice(tongTien: 2_000_000.5m);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal("Hóa đơn có số tiền lẻ", outcome.LyDo);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task MissingInvoice_IsNotPayable()
        {
            var outcome = await HandleAsync(Transfer(), "TTPTHD99");

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal("Không tìm thấy hóa đơn", outcome.LyDo);
            Assert.Equal(99, outcome.HoaDonId);
            Assert.Empty(_h.Store.Ledger);
        }

        // ---------- Chống trùng ----------

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SameTransferAlreadyOnInvoice_IsDuplicate_EvenWhenVoided(bool voided)
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, "SEPAY-FT26ABC", isDeleted: voided);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(7, outcome.HoaDonId);
        }

        [Fact]
        public async Task Duplicate_IsCaseInsensitive()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, "sepay-ft26abc");

            var outcome = await HandleAsync(Transfer(reference: " ft26 abc "));

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
        }

        [Fact]
        public async Task Duplicate_IsCheckedBeforeAlreadyPaid()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m, "SEPAY-FT26ABC");

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
        }

        [Fact]
        public async Task SameTransferSentTwice_IsRecordedOnce()
        {
            var hoaDon = _h.AddSentInvoice();

            var first = await HandleAsync(Transfer());
            var second = await HandleAsync(Transfer());
            var third = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, first.Kind);
            Assert.Equal(TransferOutcomeKind.Duplicate, second.Kind);
            Assert.Equal(TransferOutcomeKind.Duplicate, third.Kind);
            Assert.Single(_h.Store.Ledger);
            Assert.Single(hoaDon.LichSuTrangThaiHoaDons);
        }

        [Fact]
        public async Task UniqueIndexViolationOnSave_BecomesDuplicate()
        {
            var hoaDon = _h.AddSentInvoice();
            _h.UnitOfWork.FailOnSave = _ => new UniqueConstraintViolationException("IX_lich_su_thanh_toan_MaGiaoDich", new Exception());

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            Assert.Equal(7, outcome.HoaDonId);
            Assert.True(_h.LastTx.RolledBack);
            Assert.False(_h.LastTx.Committed);
            // Lưu thất bại trước khi tính lại trạng thái nên hóa đơn không bị đổi.
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.Empty(hoaDon.LichSuTrangThaiHoaDons);
        }

        [Fact]
        public async Task OtherUniqueIndexViolation_IsFailed()
        {
            _h.AddSentInvoice();
            _h.UnitOfWork.FailOnSave = _ => new UniqueConstraintViolationException("IX_khac", new Exception());

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            Assert.True(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task UnexpectedErrorOnSave_IsFailed_WithoutLeakingTheException()
        {
            _h.AddSentInvoice();
            _h.UnitOfWork.FailOnSave = _ => new InvalidOperationException("SECRET-DB-DETAIL");

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            Assert.Equal("Đã có lỗi hệ thống, vui lòng thử lại.", outcome.LyDo);
            Assert.DoesNotContain("SECRET", outcome.LyDo);
            Assert.Equal(7, outcome.HoaDonId);
            Assert.True(_h.LastTx.RolledBack);
        }

        // ---------- Lượt thanh toán cũ ----------

        [Theory]
        [InlineData(2_000_000)]
        [InlineData(700_000)]
        public async Task WaitingRequest_IsCancelled_EvenForAPartialPayment(int amount)
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();

            var outcome = await HandleAsync(Transfer(amount: amount));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, request.TrangThai);
            var history = request.LichSuTrangThaiYeuCauThanhToanHoaDons.Last();
            Assert.Equal("Đã nhận chuyển khoản tự động", history.LyDo);
            Assert.Null(history.NguoiThucHienId);
            Assert.Equal(Now, history.NgayThucHien);
            Assert.Equal(request.MaYeuCau, outcome.MaLuotDaHuy);
            Assert.False(outcome.ConMinhChungChoDoiChieu);
        }

        [Fact]
        public async Task RequestUnderReview_IsKept_ButStillRecorded_AndFlagged()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            var proof = _h.AddProof(request);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, proof.TrangThaiDoiChieu);
            Assert.True(outcome.ConMinhChungChoDoiChieu);
            Assert.Null(outcome.MaLuotDaHuy);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task ExpiredRequest_IsMarkedExpired_NotCancelled()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(createdAt: Now.AddHours(-30));

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, request.TrangThai);
            Assert.Null(outcome.MaLuotDaHuy);
            Assert.False(outcome.ConMinhChungChoDoiChieu);
        }

        // ---------- Ghi chú và lý do ----------

        [Fact]
        public async Task BracketsFromTheBank_AreRemoved_SoSystemTagsCannotBeForged()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer(content: "TTPTHD7 [DA-HUY x] [TIEN-THUA:99] [CHO-ADMIN] [CHENH-LECH:+5]", gateway: "MB[Bank]"));

            var ghiChu = Assert.Single(_h.Store.Ledger).GhiChu;
            Assert.DoesNotContain("[DA-HUY", ghiChu);
            Assert.DoesNotContain("[TIEN-THUA", ghiChu);
            Assert.DoesNotContain("[CHO-ADMIN", ghiChu);
            Assert.DoesNotContain("[CHENH-LECH", ghiChu);
            Assert.Contains("MBBank", ghiChu);
            Assert.Contains("DA-HUY x", ghiChu);
            Assert.False(PaymentNoteTags.ContainsReservedTag(ghiChu));
            var info = PaymentNoteTags.Parse(ghiChu);
            Assert.False(info.DaHuy);
            Assert.Null(info.TienThua);
            Assert.Null(info.ChenhLech);
        }

        [Fact]
        public async Task BracketsInTheReference_AreRemovedFromNoteAndHistory_ButKeptInTheDedupKey()
        {
            var hoaDon = _h.AddSentInvoice();

            await HandleAsync(Transfer(reference: "FT[1]"));

            var ledger = Assert.Single(_h.Store.Ledger);
            Assert.Equal("SEPAY-FT[1]", ledger.MaGiaoDich);
            Assert.Contains("REF FT1", ledger.GhiChu);
            Assert.DoesNotContain("[1]", ledger.GhiChu);
            Assert.Equal("[TU_DONG] SePay ghi nhận 2.000.000đ, REF FT1", hoaDon.LichSuTrangThaiHoaDons.Single().LyDo);
        }

        [Fact]
        public async Task OverpaymentTag_SurvivesTruncation_AndNoteIsCappedAt500()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer(amount: 2_500_000m, content: "TTPTHD7 " + new string('x', 2000)));

            var ghiChu = Assert.Single(_h.Store.Ledger).GhiChu;
            Assert.Equal(500, ghiChu.Length);
            Assert.StartsWith("[TIEN-THUA:500000] [SEPAY]", ghiChu);
            Assert.Equal(500_000m, PaymentNoteTags.Parse(ghiChu).TienThua);
        }

        [Fact]
        public async Task Truncation_DoesNotSplitASurrogatePair()
        {
            _h.AddSentInvoice();
            var prefixLength = "[SEPAY] MBBank · REF FT26ABC · ND: ".Length;
            // Cặp surrogate của U+1F600 bắt đầu đúng ở ký tự thứ 500 nên chỗ cắt nằm giữa cặp.
            var content = new string('a', 500 - prefixLength - 1) + "\U0001F600" + "tail";

            await HandleAsync(Transfer(content: content));

            var ghiChu = Assert.Single(_h.Store.Ledger).GhiChu;
            Assert.True(ghiChu.Length <= 500);
            Assert.False(char.IsHighSurrogate(ghiChu[^1]));
        }

        [Fact]
        public async Task WithoutReference_UsesTheSePayId_InNoteHistoryAndKey()
        {
            var hoaDon = _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer(reference: null));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            var ledger = Assert.Single(_h.Store.Ledger);
            Assert.Equal("SEPAY-ID-92704", ledger.MaGiaoDich);
            Assert.StartsWith("[SEPAY] MBBank · ID 92704 · ND: TTPTHD7", ledger.GhiChu);
            Assert.DoesNotContain("REF", ledger.GhiChu);
            Assert.Equal("[TU_DONG] SePay ghi nhận 2.000.000đ, ID 92704", hoaDon.LichSuTrangThaiHoaDons.Single().LyDo);
        }

        [Fact]
        public async Task MissingGatewayAndContent_StillProduceANote()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer(gateway: null, content: null));

            var ghiChu = Assert.Single(_h.Store.Ledger).GhiChu;
            Assert.StartsWith("[SEPAY]", ghiChu);
            Assert.Contains("REF FT26ABC", ghiChu);
        }

        // ---------- Mã không dùng được ----------

        [Theory]
        [InlineData("TTPTHDABC")]
        [InlineData("TTPTHD0")]
        [InlineData("TTPTHD000")]
        [InlineData("TTPTHD")]
        [InlineData("TTPTHD-5")]
        [InlineData("TTPTHD1234567890")]
        [InlineData("TTPTHD12 5")]
        [InlineData("TTPTHD٥")]
        [InlineData("OTHER7")]
        public async Task InvalidInvoiceNumber_IsUnmatched_WithoutOpeningATransaction(string code)
        {
            _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer(), code);

            Assert.Equal(TransferOutcomeKind.Unmatched, outcome.Kind);
            Assert.Equal(code, outcome.Code);
            Assert.Null(outcome.HoaDonId);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(0, _h.Store.LockCalls);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task NineDigitInvoiceNumber_IsLookedUp()
        {
            var outcome = await HandleAsync(Transfer(), "TTPTHD999999999");

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal(999_999_999, outcome.HoaDonId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task NonPositiveAmount_IsIgnored_WithoutOpeningATransaction(int amount)
        {
            _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer(amount: amount));

            Assert.Equal(TransferOutcomeKind.Ignored, outcome.Kind);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public void Prefix_IsTheInvoicePrefix()
        {
            Assert.Equal("TTPTHD", _handler.Prefix);
            Assert.Equal(InvoiceTransferHandler.InvoicePrefix, _handler.Prefix);
        }
    }
}
