using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoicePaymentRequestServiceTests
    {
        private const int Tenant = PaymentTestHarness.TenantId;
        private const int TenantUser = PaymentTestHarness.TenantUserId;
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();

        private SubmitPaymentProofRequest Proof(int yeuCauId, string? ma = null, UploadFile? file = null, DateTime? ngayChuyen = null) => new()
        {
            YeuCauId = yeuCauId,
            File = file ?? PaymentTestHarness.JpegFile(),
            MaGiaoDichNganHang = ma,
            NgayChuyenUtc = ngayChuyen ?? Now.AddMinutes(-10)
        };

        // ---------- Tạo lượt ----------

        [Fact]
        public async Task Create_WithoutAmount_UsesRemaining_WithCodeAsTransferContent()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            _h.AddLedger(7, 500_000m);
            _h.Codes.RequestCodes.Enqueue("TT7ABCDEF");

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1_500_000m, result.Data!.SoTien);
            Assert.Equal("TT7ABCDEF", result.Data.MaYeuCau);
            Assert.Equal("TT7ABCDEF", result.Data.NoiDungChuyenKhoan);
            Assert.Equal(Now.AddHours(24), result.Data.HanThanhToanUtc);
            Assert.True(result.Data.YeuCauId > 0);
            Assert.True(_h.LastTx.Committed);
        }

        [Fact]
        public async Task Create_PartialAllowed_AboveMinimum_Succeeds()
        {
            _h.AddSentInvoice(choPhepMotPhan: true, toiThieu: 500_000m);

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, 700_000m);

            Assert.True(result.Success, result.Message);
            Assert.Equal(700_000m, result.Data!.SoTien);
        }

        [Theory]
        [InlineData(true, 500_000, 400_000, "tối thiểu")]
        [InlineData(false, null, 400_000, "không cho phép")]
        [InlineData(true, 500_000, 600_000.5, "số nguyên")]
        public async Task Create_InvalidPartialAmount_Fails(bool choPhep, int? toiThieu, decimal soTien, string expected)
        {
            _h.AddSentInvoice(choPhepMotPhan: choPhep, toiThieu: toiThieu);

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, soTien);

            Assert.False(result.Success);
            Assert.Contains(expected, result.Message);
            Assert.Empty(_h.Store.Requests);
        }

        [Fact]
        public async Task Create_RemainingBelowMinimum_RequiresExactRemaining()
        {
            _h.AddSentInvoice(choPhepMotPhan: true, toiThieu: 500_000m);
            _h.AddLedger(7, 1_700_000m);

            var partial = await _h.Requests.CreateAsync(7, Tenant, TenantUser, 100_000m);
            var exact = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);

            Assert.False(partial.Success);
            Assert.True(exact.Success);
            Assert.Equal(300_000m, exact.Data!.SoTien);
        }

        [Fact]
        public async Task Create_FractionalInvoiceTotal_IsBlocked()
        {
            _h.AddSentInvoice(tongTien: 1_233_333.33m);

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);

            Assert.False(result.Success);
            Assert.Contains("số tiền lẻ", result.Message);
        }

        [Fact]
        public async Task Create_WhenActiveRequestExists_Fails()
        {
            _h.AddSentInvoice();
            _h.AddRequest();

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);

            Assert.False(result.Success);
            Assert.Contains("chưa hoàn tất", result.Message);
            Assert.Single(_h.Store.Requests);
        }

        [Fact]
        public async Task Create_ExpiredRequest_IsMarkedHetHan_AndDoesNotBlock()
        {
            _h.AddSentInvoice();
            var old = _h.AddRequest(createdAt: Now.AddHours(-30));

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, old.TrangThai);
            Assert.Equal(2, _h.Store.Requests.Count);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        public async Task Create_InvoiceNotSent_IsHiddenFromTenant(TrangThaiPhatHanhHoaDon status)
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = status;

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task Create_InvoiceOfAnotherTenant_ReturnsNotFound()
        {
            _h.AddSentInvoice();

            var result = await _h.Requests.CreateAsync(7, nguoiThueId: 999, TenantUser, null);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.Empty(_h.UnitOfWork.Transactions);
        }

        [Fact]
        public async Task Create_FullyPaid_Fails()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m);

            var result = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);

            Assert.False(result.Success);
            Assert.Contains("thanh toán đủ", result.Message);
        }

        [Fact]
        public async Task Create_CodeCollision_Regenerates_ThenGivesUpAfterThreeTries()
        {
            _h.AddSentInvoice();
            _h.Store.TakenRequestCodes.Add("TT7TAKEN1");
            _h.Codes.RequestCodes.Enqueue("TT7TAKEN1");
            _h.Codes.RequestCodes.Enqueue("TT7FRESH2");

            var ok = await _h.Requests.CreateAsync(7, Tenant, TenantUser, null);
            Assert.Equal("TT7FRESH2", ok.Data!.MaYeuCau);

            var h2 = new PaymentTestHarness();
            h2.AddSentInvoice();
            foreach (var code in new[] { "TT7X1", "TT7X2", "TT7X3" })
            {
                h2.Store.TakenRequestCodes.Add(code);
                h2.Codes.RequestCodes.Enqueue(code);
            }

            var fail = await h2.Requests.CreateAsync(7, Tenant, TenantUser, null);
            Assert.False(fail.Success);
            Assert.Equal("Vui lòng thử lại.", fail.Message);
        }

        // ---------- Hủy lượt ----------

        [Fact]
        public async Task Cancel_WaitingRequest_BecomesDaHuy_WithTenantAsActor()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();

            var result = await _h.Requests.CancelAsync(request.YeuCauThanhToanHoaDonId, Tenant, TenantUser);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, request.TrangThai);
            Assert.Equal(TenantUser, request.LichSuTrangThaiYeuCauThanhToanHoaDons.Last().NguoiThucHienId);
        }

        [Fact]
        public async Task Cancel_RequestUnderReview_Fails()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            _h.AddProof(request);

            var result = await _h.Requests.CancelAsync(request.YeuCauThanhToanHoaDonId, Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
        }

        [Theory]
        [InlineData(TrangThaiYeuCauThanhToan.DaHoanTat)]
        [InlineData(TrangThaiYeuCauThanhToan.DaHuy)]
        public async Task Cancel_FinishedRequest_Fails(TrangThaiYeuCauThanhToan status)
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            request.TrangThai = status;

            var result = await _h.Requests.CancelAsync(request.YeuCauThanhToanHoaDonId, Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Equal(status, request.TrangThai);
        }

        [Fact]
        public async Task Cancel_ExpiredRequest_Fails_AndCommitsHetHan()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(createdAt: Now.AddHours(-30));

            var result = await _h.Requests.CancelAsync(request.YeuCauThanhToanHoaDonId, Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, request.TrangThai);
        }

        // ---------- Nộp minh chứng ----------

        [Fact]
        public async Task SubmitProof_Valid_UploadsNamedByCode_AndNotifiesBranchReviewers()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(code: "TT7QWERTY");

            var result = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId, " FT123 "), Tenant, TenantUser);

            Assert.True(result.Success, result.Message);
            Assert.Equal(("TT7QWERTY.jpg", "payment-proofs"), _h.Storage.Uploads.Single());
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            var proof = request.MinhChungThanhToanHoaDons.Single();
            Assert.Equal(request.SoTien, proof.SoTienKhaiBao);
            Assert.Equal("FT123", proof.MaGiaoDichNganHang);
            Assert.Equal("payment-proofs/TT7QWERTY_abc", proof.PublicId);
            var recipients = _h.Store.Notifications.Select(n => n.NguoiDungId).ToList();
            Assert.Contains(PaymentTestHarness.StaffId, recipients);
            Assert.Contains(PaymentTestHarness.AdminId, recipients);
            Assert.DoesNotContain(PaymentTestHarness.OtherStaffId, recipients);
            Assert.Equal(2, _h.Notifier.Sent.Count);
        }

        [Fact]
        public async Task SubmitProof_CodeAlreadyRecorded_OrPending_Fails()
        {
            _h.AddSentInvoice();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddLedger(8, 100_000m, "FT-LEDGER");
            var otherRequest = _h.AddRequest(hoaDonId: 8, soTien: 500_000m);
            _h.AddProof(otherRequest, "FT-PENDING");
            var request = _h.AddRequest();

            var dupLedger = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId, "ft-ledger"), Tenant, TenantUser);
            var dupPending = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId, "FT-PENDING"), Tenant, TenantUser);

            Assert.Contains("đã được ghi nhận", dupLedger.Message);
            Assert.Contains("chờ đối chiếu", dupPending.Message);
            Assert.Empty(_h.Storage.Uploads);
        }

        [Fact]
        public async Task SubmitProof_ExpiredRequest_ReturnsError_AndCommitsHetHan()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(createdAt: Now.AddHours(-30));

            var result = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId), Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Contains("hết hạn", result.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, request.TrangThai);
            Assert.True(_h.LastTx.Committed);
            Assert.Empty(_h.Storage.Uploads);
        }

        [Fact]
        public async Task SubmitProof_FutureTransferTime_Fails()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();

            var result = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId, ngayChuyen: Now.AddMinutes(10)), Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Contains("tương lai", result.Message);
        }

        [Fact]
        public async Task SubmitProof_WrongMagicBytes_Fails()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

            var result = await _h.Requests.SubmitProofAsync(
                Proof(request.YeuCauThanhToanHoaDonId, file: new UploadFile(new MemoryStream(png), "a.jpg", "image/jpeg", png.Length)), Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Contains("không khớp", result.Message);
        }

        [Fact]
        public async Task SubmitProof_StateChangedDuringUpload_DeletesUploadedImage()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            _h.Storage.AfterUpload = () => request.Huy(TenantUser, Now, "hủy ở tab khác");

            var result = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId), Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Single(_h.Storage.Deleted);
        }

        [Fact]
        public async Task SubmitProof_SaveFails_DeletesUploadedImage()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            // Lần lưu 1 là bước kiểm trước khi tải ảnh; lần 2 là lúc ghi minh chứng.
            _h.UnitOfWork.FailOnSave = n => n == 2 ? new UniqueConstraintViolationException("IX_x", new Exception()) : null;

            var result = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId), Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Single(_h.Storage.Uploads);
            Assert.Single(_h.Storage.Deleted);
        }

        [Fact]
        public async Task SubmitProof_UploadFails_ReturnsError()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            _h.Storage.ThrowOnUpload = true;

            var result = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId), Tenant, TenantUser);

            Assert.False(result.Success);
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, request.TrangThai);
        }

        [Fact]
        public async Task SubmitProof_AfterRejection_GoesOnSameRequest()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            var first = _h.AddProof(request);
            request.TuChoiMinhChung(first, PaymentTestHarness.StaffId, "Không thấy giao dịch", Now.AddMinutes(-5), Now.AddHours(24));

            var result = await _h.Requests.SubmitProofAsync(Proof(request.YeuCauThanhToanHoaDonId), Tenant, TenantUser);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, request.MinhChungThanhToanHoaDons.Count);
        }

        // ---------- Khung thanh toán, QR, ảnh ----------

        [Fact]
        public async Task Panel_ShowsExpiredRequestAsHetHan_WithoutWriting()
        {
            _h.AddSentInvoice(choPhepMotPhan: true, toiThieu: 500_000m);
            var request = _h.AddRequest(createdAt: Now.AddHours(-30));

            var result = await _h.Requests.GetPaymentPanelAsync(7, Tenant);

            Assert.True(result.Success);
            Assert.Equal(AppTrangThaiYeuCauThanhToan.HetHan, result.Data!.LuotGanNhat!.TrangThaiHieuLuc);
            Assert.True(result.Data.CoTheTaoLuot);
            Assert.Equal(500_000m, result.Data.SoTienThanhToanToiThieu);
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, request.TrangThai);
            Assert.Empty(_h.UnitOfWork.Transactions);
        }

        [Fact]
        public async Task Panel_AnotherTenant_NotFound()
        {
            _h.AddSentInvoice();

            var result = await _h.Requests.GetPaymentPanelAsync(7, 999);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task Qr_UsesRequestAmountAndCode_OnlyWhileWaiting()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(soTien: 2_000_000m, code: "TT7ZXCVBN");

            var ok = await _h.Requests.GetQrAsync(request.YeuCauThanhToanHoaDonId, Tenant);
            Assert.True(ok.Success);
            Assert.Equal(2_000_000m, _h.VietQr.LastAmount);
            Assert.Equal("TT7ZXCVBN", _h.VietQr.LastMemo);

            _h.AddProof(request);
            var underReview = await _h.Requests.GetQrAsync(request.YeuCauThanhToanHoaDonId, Tenant);
            Assert.False(underReview.Success);
        }

        [Fact]
        public async Task ProofImage_OnlyOwnerCanRead()
        {
            _h.AddSentInvoice();
            var proof = _h.AddProof(_h.AddRequest());

            Assert.True((await _h.Requests.GetProofImageAsync(proof.MinhChungThanhToanHoaDonId, Tenant)).Success);
            Assert.Equal(ServiceErrorKind.NotFound, (await _h.Requests.GetProofImageAsync(proof.MinhChungThanhToanHoaDonId, 999)).ErrorKind);
        }
    }
}
