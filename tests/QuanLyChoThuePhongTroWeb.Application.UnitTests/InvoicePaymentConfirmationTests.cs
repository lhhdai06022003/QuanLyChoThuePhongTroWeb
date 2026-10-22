using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoicePaymentConfirmationTests
    {
        private const int Staff = PaymentTestHarness.StaffId;
        private const int OtherStaff = PaymentTestHarness.OtherStaffId;
        private const int Admin = PaymentTestHarness.AdminId;
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();

        private (YeuCauThanhToanHoaDon Request, MinhChungThanhToanHoaDon Proof) PendingProof(
            decimal tongTien = 2_000_000m, decimal soTienLuot = 2_000_000m, bool choPhepMotPhan = false, string? maKhaiBao = null)
        {
            _h.AddSentInvoice(tongTien: tongTien, choPhepMotPhan: choPhepMotPhan, toiThieu: choPhepMotPhan ? 100_000m : null);
            var request = _h.AddRequest(soTien: soTienLuot);
            return (request, _h.AddProof(request, maKhaiBao));
        }

        private ConfirmPaymentRequest Confirm(MinhChungThanhToanHoaDon proof, decimal thucNhan, string? ma = null, string? ghiChu = null) => new()
        {
            MinhChungId = proof.MinhChungThanhToanHoaDonId,
            SoTienThucNhan = thucNhan,
            MaGiaoDich = ma,
            NgayGiaoDichUtc = Now.AddHours(-1),
            GhiChu = ghiChu
        };

        // ---------- Xác nhận đúng số (§14) ----------

        [Fact]
        public async Task Confirm_ExactAmount_FullInvoice_BecomesPaid_WithReviewerInputs()
        {
            var (request, proof) = PendingProof(maKhaiBao: "FT-KHAI");

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ma: "FT-SAOKE"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
            var ledger = _h.Store.Ledger.Single();
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal("FT-SAOKE", ledger.MaGiaoDich);
            Assert.Equal(Now.AddHours(-1), ledger.NgayThanhToan);
            Assert.Equal(Now, ledger.NgayXacNhan);
            Assert.Equal(Staff, ledger.NguoiXacNhanId);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, ledger.PhuongThucThanhToan);
            Assert.Equal(proof.MinhChungThanhToanHoaDonId, ledger.MinhChungThanhToanHoaDonId);
            Assert.Equal(TrangThaiMinhChungThanhToan.DaXacNhan, proof.TrangThaiDoiChieu);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
            var hoaDon = _h.Store.Invoices[7];
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.Single(hoaDon.LichSuTrangThaiHoaDons);
            Assert.Contains(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
        }

        [Fact]
        public async Task Confirm_PartialRequest_LeavesInvoicePartiallyPaid()
        {
            var (_, proof) = PendingProof(soTienLuot: 800_000m, choPhepMotPhan: true);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 800_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1_200_000m, result.Data!.ConLai);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task Confirm_BlankBankCode_UsesRequestCode()
        {
            var (request, proof) = PendingProof();

            await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ma: "  "), Staff);

            Assert.Equal(request.MaYeuCau, _h.Store.Ledger.Single().MaGiaoDich);
        }

        [Fact]
        public async Task Confirm_SecondTime_IsRejected()
        {
            var (_, proof) = PendingProof();
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Staff);

            var again = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Admin);

            Assert.False(again.Success);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task Confirm_BankCodeAlreadyRecorded_IsRejected()
        {
            var (_, proof) = PendingProof();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddLedger(8, 100_000m, "FT-DUP");

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ma: "ft-dup"), Staff);

            Assert.False(result.Success);
            Assert.Equal("Mã giao dịch đã được ghi nhận.", result.Message);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, proof.TrangThaiDoiChieu);
        }

        // ---------- Ghi nhận số thực nhận (§15) ----------

        [Fact]
        public async Task Confirm_LowerThanRequest_RecordsActualAmount_WithTag_EvenWhenPartialDisabled()
        {
            var (request, proof) = PendingProof(choPhepMotPhan: false);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_900_000m, ghiChu: "Khách chuyển thiếu"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaGhiNhanSoThucNhan, result.Data!.KetQua);
            var ledger = _h.Store.Ledger.Single();
            Assert.Equal(1_900_000m, ledger.SoTienThanhToan);
            Assert.Equal("[CHENH-LECH:-100000] Khách chuyển thiếu", ledger.GhiChu);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, _h.Store.Invoices[7].TrangThaiHoaDon);
            // Hóa đơn không cho trả một phần mà còn nợ: Admin được báo để có người thứ hai biết.
            Assert.Contains(_h.Store.Notifications, n => n.NguoiDungId == Admin);
        }

        [Fact]
        public async Task Confirm_HigherThanPartialRequest_ButWithinDebt_RecordsActual()
        {
            var (_, proof) = PendingProof(soTienLuot: 1_000_000m, choPhepMotPhan: true);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ghiChu: "Chuyển dư"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1_500_000m, _h.Store.Ledger.Single().SoTienThanhToan);
            Assert.DoesNotContain(_h.Store.Notifications, n => n.NguoiDungId == Admin);
        }

        [Fact]
        public async Task Confirm_Difference_RequiresNote()
        {
            var (_, proof) = PendingProof();

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_900_000m), Staff);

            Assert.False(result.Success);
            Assert.Contains("ghi chú", result.Message);
            Assert.Empty(_h.Store.Ledger);
        }

        // ---------- Tiền thừa (§16) ----------

        [Fact]
        public async Task Confirm_StaffOverDebt_EscalatesToAdmin_AndCommitsHistory()
        {
            var (request, proof) = PendingProof();

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_500_000m, ghiChu: "Chuyển dư"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaChuyenAdmin, result.Data!.KetQua);
            Assert.Empty(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, proof.TrangThaiDoiChieu);
            Assert.StartsWith(PaymentNoteTags.ChoAdminPrefix, request.LichSuTrangThaiYeuCauThanhToanHoaDons.Last().LyDo);
            Assert.True(_h.LastTx.Committed);
            Assert.Contains(_h.Store.Notifications, n => n.NguoiDungId == Admin);
        }

        [Fact]
        public async Task WaitingAdmin_StaffCannotConfirmOrReject_AdminRecordsOverpay()
        {
            var (request, proof) = PendingProof();
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_500_000m, ghiChu: "dư"), Staff);

            var staffConfirm = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Staff);
            var staffReject = await _h.Confirmation.RejectAsync(proof.MinhChungThanhToanHoaDonId, Staff, "không thấy");
            Assert.Equal(ServiceErrorKind.Forbidden, staffConfirm.ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, staffReject.ErrorKind);

            var admin = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_500_000m, ghiChu: "Hoàn lại khách"), Admin);

            Assert.True(admin.Success, admin.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhanCoTienThua, admin.Data!.KetQua);
            var ledger = _h.Store.Ledger.Single();
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal("[TIEN-THUA:500000] Hoàn lại khách", ledger.GhiChu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
        }

        [Fact]
        public async Task Queue_WaitingAdminFilter_ShowsEscalatedOnly()
        {
            var (_, proof) = PendingProof();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddProof(_h.AddRequest(hoaDonId: 8));
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_500_000m, ghiChu: "dư"), Staff);

            var all = await _h.Confirmation.GetReviewQueueAsync(Admin, new ReviewQueueFilter(), new DataTableRequest { Length = 10 });
            var waiting = await _h.Confirmation.GetReviewQueueAsync(Admin, new ReviewQueueFilter { ChiChoAdmin = true }, new DataTableRequest { Length = 10 });

            Assert.Equal(2, all.Data!.recordsTotal);
            Assert.Equal(proof.MinhChungThanhToanHoaDonId, Assert.Single(waiting.Data!.data).MinhChungId);
        }

        // ---------- Từ chối (§17) ----------

        [Fact]
        public async Task Reject_ReturnsRequestToWaiting_WithNewDeadline_AndNotifiesTenant()
        {
            var (request, proof) = PendingProof();

            var result = await _h.Confirmation.RejectAsync(proof.MinhChungThanhToanHoaDonId, Staff, "Không thấy giao dịch trên sao kê");

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, request.TrangThai);
            Assert.Equal(Now.AddHours(24), request.HanThanhToan);
            Assert.Equal(TrangThaiMinhChungThanhToan.TuChoi, proof.TrangThaiDoiChieu);
            Assert.Contains(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Reject_EmptyReason_Fails(string lyDo)
        {
            var (_, proof) = PendingProof();

            var result = await _h.Confirmation.RejectAsync(proof.MinhChungThanhToanHoaDonId, Staff, lyDo);

            Assert.False(result.Success);
            Assert.Empty(_h.UnitOfWork.Transactions);
        }

        [Fact]
        public async Task Reject_ReasonOver500_Fails()
        {
            var (_, proof) = PendingProof();

            var result = await _h.Confirmation.RejectAsync(proof.MinhChungThanhToanHoaDonId, Staff, new string('x', 501));

            Assert.False(result.Success);
        }

        // ---------- Quyền chi nhánh ----------

        [Fact]
        public async Task OtherBranchStaff_IsForbidden_AdminAllowed()
        {
            var (_, proof) = PendingProof();

            var forbidden = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), OtherStaff);
            var detail = await _h.Confirmation.GetProofDetailAsync(proof.MinhChungThanhToanHoaDonId, OtherStaff);
            var image = await _h.Confirmation.GetProofImageAsync(proof.MinhChungThanhToanHoaDonId, OtherStaff);

            Assert.Equal(ServiceErrorKind.Forbidden, forbidden.ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, detail.ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, image.ErrorKind);
            Assert.True((await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Admin)).Success);
        }

        [Fact]
        public async Task Queue_StaffSeesOnlyAssignedBranches()
        {
            PendingProof();
            _h.AddSentInvoice(hoaDonId: 8, chiNhanhId: PaymentTestHarness.OtherBranch);
            _h.AddProof(_h.AddRequest(hoaDonId: 8));

            var staff = await _h.Confirmation.GetReviewQueueAsync(Staff, new ReviewQueueFilter(), new DataTableRequest { Length = 10 });
            var otherFilter = await _h.Confirmation.GetReviewQueueAsync(Staff, new ReviewQueueFilter { ChiNhanhId = PaymentTestHarness.OtherBranch }, new DataTableRequest { Length = 10 });

            Assert.Equal(7, Assert.Single(staff.Data!.data).HoaDonId);
            Assert.Empty(otherFilter.Data!.data);
        }

        // ---------- Cấu hình trả một phần (§19) ----------

        [Fact]
        public async Task ConfigurePartial_AdminOnly_AndWritesHistory()
        {
            _h.AddSentInvoice();
            var request = new ConfigurePartialPaymentRequest { HoaDonId = 7, ChoPhep = true, SoTienToiThieu = 500_000m };

            var staff = await _h.Confirmation.ConfigurePartialPaymentAsync(request, Staff);
            var admin = await _h.Confirmation.ConfigurePartialPaymentAsync(request, Admin);

            Assert.Equal(ServiceErrorKind.Forbidden, staff.ErrorKind);
            Assert.True(admin.Success, admin.Message);
            var hoaDon = _h.Store.Invoices[7];
            Assert.True(hoaDon.ChoPhepThanhToanMotPhan);
            Assert.Equal(500_000m, hoaDon.SoTienThanhToanToiThieu);
            Assert.Contains("Bật thanh toán một phần", hoaDon.LichSuTrangThaiHoaDons.Single().LyDo);
        }

        [Fact]
        public async Task ConfigurePartial_BlockedWhileRequestActive()
        {
            _h.AddSentInvoice();
            _h.AddRequest();

            var result = await _h.Confirmation.ConfigurePartialPaymentAsync(
                new ConfigurePartialPaymentRequest { HoaDonId = 7, ChoPhep = true, SoTienToiThieu = 500_000m }, Admin);

            Assert.False(result.Success);
            Assert.Contains("lượt thanh toán chưa hoàn tất", result.Message);
            Assert.False(_h.Store.Invoices[7].ChoPhepThanhToanMotPhan);
        }

        [Fact]
        public async Task ConfigurePartial_FractionalMinimum_Fails()
        {
            _h.AddSentInvoice();

            var result = await _h.Confirmation.ConfigurePartialPaymentAsync(
                new ConfigurePartialPaymentRequest { HoaDonId = 7, ChoPhep = true, SoTienToiThieu = 500_000.5m }, Admin);

            Assert.False(result.Success);
        }

        // ---------- Tra cứu theo mã (§21) ----------

        [Fact]
        public async Task Search_FindsExpiredAndCancelledRequests_CaseAndSpaceInsensitive()
        {
            _h.AddSentInvoice();
            var expired = _h.AddRequest(createdAt: Now.AddHours(-30), code: "TT7EXPIRE");
            expired.DanhDauHetHan(Now);
            var cancelled = _h.AddRequest(code: "TT7CANCEL");
            cancelled.Huy(PaymentTestHarness.TenantUserId, Now, "khách hủy");

            var a = await _h.Confirmation.SearchPaymentRequestAsync(" tt7 expire ", Staff);
            var b = await _h.Confirmation.SearchPaymentRequestAsync("TT7CANCEL", Staff);

            Assert.True(a.Success, a.Message);
            Assert.Equal("Hết hạn", a.Data!.LuotThanhToan.TrangThaiText);
            Assert.Equal("Đã hủy", b.Data!.LuotThanhToan.TrangThaiText);
            Assert.Equal(2_000_000m, a.Data.ConLai);
        }

        [Fact]
        public async Task Search_OtherBranchStaff_GetsNotFound()
        {
            _h.AddSentInvoice();
            _h.AddRequest(code: "TT7HIDDEN");

            var result = await _h.Confirmation.SearchPaymentRequestAsync("TT7HIDDEN", OtherStaff);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        // ---------- Đồng thời: thay đổi ngay lúc lấy khóa ----------

        [Fact]
        public async Task Confirm_WhenOtherPaymentLandsBeforeLock_RecomputesFromLedger()
        {
            var (_, proof) = PendingProof(soTienLuot: 2_000_000m);
            // Một khoản khác đã được ghi nhận trước khi luồng này lấy được khóa: tổng không được vượt TongTien.
            _h.Store.OnLock = hd =>
            {
                if (!_h.Store.Ledger.Any())
                {
                    _h.Store.Ledger.Add(new LichSuThanhToan { HoaDonId = hd.HoaDonId, MaGiaoDich = "TM-X", SoTienThanhToan = 500_000m, LichSuThanhToanId = 1 });
                }
            };

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ghiChu: "dư"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaChuyenAdmin, result.Data!.KetQua);
            Assert.Single(_h.Store.Ledger);
        }
    }
}
