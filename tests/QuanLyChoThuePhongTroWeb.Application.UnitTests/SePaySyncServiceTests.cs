using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Đồng bộ bù giao dịch tiền vào qua API SePay (spec thanh toán trực tuyến §4.3, §5.2): chỉ Admin, phân trang, ánh xạ kết quả,
    // chế độ chỉ đối chiếu. Client và điểm ghi sổ là fake; hai test cuối chạy qua ghi sổ thật để kiểm chống trùng với webhook.
    public class SePaySyncServiceTests
    {
        private const int AdminId = PaymentTestHarness.AdminId;
        private const int StaffId = PaymentTestHarness.StaffId;
        private const string BankAccount = "0123456789";

        private static readonly DateTime From = new(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new(2026, 10, 18, 16, 59, 59, DateTimeKind.Utc);
        private static readonly DateTime TxTime = new(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc);

        private readonly FakeSePayTransactionClient _client = new();
        private readonly RecordingBankTransferIngestion _ingestion = new();
        private readonly PaymentAccessFake _access = new();
        private readonly SePayOptions _options = new() { ApiToken = "api-token-secret", SyncRecordEnabled = true };
        private readonly FakeInvoicePaymentStore _store = new();
        private readonly CapturingLogger<SePaySyncService> _log = new();

        public SePaySyncServiceTests()
        {
            _access.Admins.Add(AdminId);
            _access.StaffBranches[StaffId] = new HashSet<int> { PaymentTestHarness.Branch };
        }

        private SePaySyncService Service() => new(_client, _ingestion, _access, _options, _store, _log);

        private static SePayApiTransaction Tx(
            string id = "tx-1", string? reference = "FT26ABC", string? type = "in", decimal amount = 2_000_000m,
            string? content = "Nguyen Van A TTPTHD7", string? code = null, string? account = BankAccount, string? bank = "MBBank",
            string? subAccount = null)
            => new(id, TxTime, account, subAccount, type, amount, content, reference, code, bank);

        private static SePaySyncRequest Range() => new(From, To);

        private async Task<SePaySyncResult> SyncOkAsync(SePaySyncRequest? request = null)
        {
            var result = await Service().SyncAsync(AdminId, request ?? Range());
            Assert.True(result.Success, result.Message);
            return result.Data!;
        }

        // ---------- Phân quyền (fail closed) ----------

        [Theory]
        [InlineData(StaffId)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(4040)]
        public async Task NonAdmin_IsForbidden_AndTheClientIsNeverCalled(int actorId)
        {
            var sync = await Service().SyncAsync(actorId, Range());
            var settings = await Service().GetSettingsAsync(actorId);

            Assert.Equal(ServiceErrorKind.Forbidden, sync.ErrorKind);
            Assert.Equal("Chỉ Admin được đồng bộ giao dịch SePay.", sync.Message);
            Assert.Equal(ServiceErrorKind.Forbidden, settings.ErrorKind);
            Assert.Empty(_client.Calls);
            Assert.Empty(_ingestion.Transfers);
        }

        [Fact]
        public async Task ActorWithoutAScope_IsForbidden()
        {
            var access = new FakeEmployeeAccessService { ScopeToReturn = null };
            var service = new SePaySyncService(_client, _ingestion, access, _options, _store, _log);

            Assert.Equal(ServiceErrorKind.Forbidden, (await service.SyncAsync(AdminId, Range())).ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.GetSettingsAsync(AdminId)).ErrorKind);
            Assert.Empty(_client.Calls);
        }

        // ---------- Cài đặt ----------

        [Fact]
        public async Task Settings_ReportTokenPresenceAndLimits_AndNeverTheToken()
        {
            var settings = (await Service().GetSettingsAsync(AdminId)).Data!;

            Assert.True(settings.DaCauHinhApiToken);
            Assert.Equal(3, settings.SoNgayMacDinh);
            Assert.Equal(31, settings.SoNgayToiDa);
            Assert.False(settings.ChiDoiChieu);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Settings_WithoutAnApiToken_ReportItIsNotConfigured(string token)
        {
            _options.ApiToken = token;

            Assert.False((await Service().GetSettingsAsync(AdminId)).Data!.DaCauHinhApiToken);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task Settings_ChiDoiChieu_IsTheOppositeOfSyncRecordEnabled(bool syncRecordEnabled, bool chiDoiChieu)
        {
            _options.SyncRecordEnabled = syncRecordEnabled;

            Assert.Equal(chiDoiChieu, (await Service().GetSettingsAsync(AdminId)).Data!.ChiDoiChieu);
        }

        [Fact]
        public void TheRangeLimits_AreThreeDaysByDefaultAndThirtyOneAtMost()
        {
            Assert.Equal(3, SePaySyncService.DefaultRangeDays);
            Assert.Equal(31, SePaySyncService.MaxRangeDays);
            Assert.Equal(100, SePaySyncService.MaxPages);
        }

        // ---------- Điều kiện đầu vào ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WithoutAnApiToken_Fails_AndTheClientIsNotCalled(string token)
        {
            _options.ApiToken = token;

            var result = await Service().SyncAsync(AdminId, Range());

            Assert.False(result.Success);
            Assert.Equal("Chưa cấu hình SePay:ApiToken nên chưa đồng bộ được.", result.Message);
            Assert.Empty(_client.Calls);
        }

        [Fact]
        public async Task ReversedRange_Fails()
        {
            var result = await Service().SyncAsync(AdminId, new SePaySyncRequest(To, From));

            Assert.False(result.Success);
            Assert.Equal("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.", result.Message);
            Assert.Empty(_client.Calls);
        }

        [Fact]
        public async Task ThirtyOneCalendarDays_IsAllowed_ButThirtyOneWholeDaysIsNot()
        {
            var start = new DateTime(2026, 9, 14, 17, 0, 0, DateTimeKind.Utc);
            _client.Enqueue(Array.Empty<SePayApiTransaction>(), hasMore: false);

            var allowed = await Service().SyncAsync(AdminId, new SePaySyncRequest(start, start.AddDays(31).AddSeconds(-1)));
            var tooLong = await Service().SyncAsync(AdminId, new SePaySyncRequest(start, start.AddDays(31)));

            Assert.True(allowed.Success, allowed.Message);
            Assert.False(tooLong.Success);
            Assert.Equal("Khoảng ngày tối đa 31 ngày.", tooLong.Message);
            Assert.Single(_client.Calls);
        }

        [Fact]
        public async Task ARangeWithEqualEnds_IsAllowed()
        {
            _client.Enqueue(Array.Empty<SePayApiTransaction>(), hasMore: false);

            Assert.True((await Service().SyncAsync(AdminId, new SePaySyncRequest(From, From))).Success);
        }

        // ---------- Phân trang ----------

        [Fact]
        public async Task ThreePages_AreFetchedInOrderWithTheSameRange_AndEveryTransactionIsHandled()
        {
            _client.Enqueue(new[] { Tx("a1", "R1"), Tx("a2", "R2") }, hasMore: true);
            _client.Enqueue(new[] { Tx("b1", "R3") }, hasMore: true);
            _client.Enqueue(new[] { Tx("c1", "R4"), Tx("c2", "R5") }, hasMore: false);

            var data = await SyncOkAsync();

            Assert.Equal(new[] { (From, To, 1), (From, To, 2), (From, To, 3) }, _client.Calls);
            Assert.Equal(5, data.Rows.Count);
            Assert.Equal(new[] { "a1", "a2", "b1", "c1", "c2" }, data.Rows.Select(r => r.SePayApiId));
            Assert.Equal(new[] { "R1", "R2", "R3", "R4", "R5" }, _ingestion.Transfers.Select(t => t.ReferenceCode));
            Assert.True(data.HoanTat);
            Assert.Null(data.LoiDongBo);
        }

        [Fact]
        public async Task WhenALaterPageFails_TheEarlierRowsAreKept_AndTheRunIsNotComplete()
        {
            _client.Enqueue(new[] { Tx("a1", "R1") }, hasMore: true);
            _client.EnqueueFailure("SePay đang giới hạn số request, thử lại sau ít phút.");

            var result = await Service().SyncAsync(AdminId, Range());

            Assert.True(result.Success, result.Message);
            var data = result.Data!;
            Assert.Equal("a1", Assert.Single(data.Rows).SePayApiId);
            Assert.False(data.HoanTat);
            Assert.Equal("SePay đang giới hạn số request, thử lại sau ít phút.", data.LoiDongBo);
            Assert.Equal(2, _client.Calls.Count);
        }

        [Fact]
        public async Task WhenTheFirstPageFails_ThereAreNoRows_AndTheRunIsNotComplete()
        {
            _client.EnqueueFailure("SePay từ chối API token (mã 401). Kiểm tra SePay:ApiToken.");

            var data = await SyncOkAsync();

            Assert.Empty(data.Rows);
            Assert.False(data.HoanTat);
            Assert.Equal("SePay từ chối API token (mã 401). Kiểm tra SePay:ApiToken.", data.LoiDongBo);
            Assert.Empty(_ingestion.Transfers);
        }

        [Fact]
        public async Task WhenThereIsAlwaysAnotherPage_ItStopsAtTheMaximumNumberOfPages()
        {
            _client.Fallback = page => ServiceResult<SePayTransactionPage>.Ok(
                new SePayTransactionPage(new[] { Tx($"p{page}", $"R{page}") }, HasMore: true, SoGiaoDichSaiDinhDang: 0));

            var data = await SyncOkAsync();

            Assert.Equal(SePaySyncService.MaxPages, _client.Calls.Count);
            Assert.Equal(SePaySyncService.MaxPages, _client.Calls[^1].Page);
            Assert.Equal(SePaySyncService.MaxPages, data.Rows.Count);
            Assert.False(data.HoanTat);
            Assert.Equal("Dừng ở trang 100: thu hẹp khoảng ngày rồi đồng bộ lại.", data.LoiDongBo);
        }

        [Fact]
        public async Task AnEmptyPage_EndsTheRunAsComplete_EvenIfItClaimsThereIsMore()
        {
            _client.Enqueue(Array.Empty<SePayApiTransaction>(), hasMore: true);

            var data = await SyncOkAsync();

            Assert.Single(_client.Calls);
            Assert.Empty(data.Rows);
            Assert.True(data.HoanTat);
            Assert.Null(data.LoiDongBo);
        }

        [Fact]
        public async Task TheSameApiId_OnTwoPages_IsHandledOnce_IgnoringCase()
        {
            _client.Enqueue(new[] { Tx("ABC-1", "R1") }, hasMore: true);
            _client.Enqueue(new[] { Tx("abc-1", "R1"), Tx("abc-2", "R2") }, hasMore: false);

            var data = await SyncOkAsync();

            Assert.Equal(2, data.Rows.Count);
            Assert.Equal(new[] { "R1", "R2" }, _ingestion.Transfers.Select(t => t.ReferenceCode));
        }

        [Fact]
        public async Task MalformedTransactions_AreCountedAcrossPages_IntoAWarning()
        {
            _client.Enqueue(new[] { Tx("a1", "R1") }, hasMore: true, malformed: 2);
            _client.Enqueue(Array.Empty<SePayApiTransaction>(), hasMore: false, malformed: 1);

            var data = await SyncOkAsync();

            Assert.Equal("3 giao dịch SePay trả về sai định dạng, không xử lý.", data.CanhBao);
            Assert.True(data.HoanTat);
        }

        [Fact]
        public async Task WithoutMalformedTransactions_ThereIsNoWarning()
        {
            _client.Enqueue(new[] { Tx() }, hasMore: false);

            Assert.Null((await SyncOkAsync()).CanhBao);
        }

        // ---------- Không bao giờ ghi sổ ----------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WithoutAReferenceNumber_IsNotIngested_AndIsLabelledMissingReference(string? reference)
        {
            _client.Enqueue(new[] { Tx(reference: reference) }, hasMore: false);

            var data = await SyncOkAsync();

            var row = Assert.Single(data.Rows);
            Assert.Equal(SePaySyncRowStatus.ThieuMaThamChieu, row.TrangThai);
            Assert.Equal("Thiếu mã tham chiếu", row.NhanTrangThai);
            Assert.Equal("Không chống trùng được với webhook. Nếu là tiền hóa đơn, xử lý bằng Thu thủ công. Kiểm lịch sử thanh toán của hóa đơn trước khi Thu thủ công.", row.GhiChu);
            Assert.Null(row.MaThamChieu);
            Assert.Empty(_ingestion.Transfers);
        }

        [Theory]
        [InlineData("out")]
        [InlineData("OUT")]
        [InlineData("IN")]
        [InlineData(" in")]
        [InlineData("")]
        [InlineData(null)]
        public async Task NotExactlyIncoming_IsNotIngested_AndIsSkipped(string? type)
        {
            _client.Enqueue(new[] { Tx(type: type) }, hasMore: false);

            var row = Assert.Single((await SyncOkAsync()).Rows);

            Assert.Equal(SePaySyncRowStatus.BoQua, row.TrangThai);
            Assert.Equal("Bỏ qua", row.NhanTrangThai);
            Assert.Equal("Không phải tiền vào", row.GhiChu);
            Assert.Empty(_ingestion.Transfers);
        }

        // ---------- Ánh xạ trường sang giao dịch ghi sổ ----------

        [Fact]
        public async Task EachTransaction_IsHandedToIngestion_WithTheSameKeyAsTheWebhook()
        {
            _client.Enqueue(new[]
            {
                Tx("tx-1", reference: " ft 26 abc ", amount: 1_500_000m, content: "Nguyen Van A TTPTHD7", code: "TTPTHD7",
                    account: "0123456789", bank: "MBBank", subAccount: "VA001")
            }, hasMore: false);

            await SyncOkAsync();

            var transfer = Assert.Single(_ingestion.Transfers);
            Assert.Equal(0, transfer.SePayId);
            Assert.Equal("MBBank", transfer.Gateway);
            Assert.Equal(TxTime, transfer.TransactionDateUtc);
            Assert.Equal(DateTimeKind.Utc, transfer.TransactionDateUtc.Kind);
            Assert.Equal("0123456789", transfer.AccountNumber);
            Assert.Equal("VA001", transfer.SubAccount);
            Assert.Equal("TTPTHD7", transfer.Code);
            Assert.Equal("Nguyen Van A TTPTHD7", transfer.Content);
            Assert.Equal("in", transfer.TransferType);
            Assert.Equal(1_500_000m, transfer.TransferAmount);
            Assert.Equal("ft 26 abc", transfer.ReferenceCode);
            Assert.Null(transfer.Description);
            Assert.True(transfer.LaDongBoBu);
            Assert.Equal("SEPAY-FT26ABC", transfer.DedupKey);
            Assert.DoesNotContain("SEPAY-ID-", transfer.DedupKey);
        }

        [Fact]
        public async Task TheTransferCode_IsTakenFromTheCodeFieldOrTheContent()
        {
            _client.Enqueue(new[]
            {
                Tx("a", "R1", code: "ttpt hd 9", content: "khong co ma"),
                Tx("b", "R2", code: null, content: "Chuyen TTPT HD12 tien phong"),
                Tx("c", "R3", code: null, content: "khong co ma gi ca")
            }, hasMore: false);

            var rows = (await SyncOkAsync()).Rows;

            Assert.Equal("TTPTHD9", rows[0].MaChuyenKhoan);
            Assert.Equal("TTPTHD12", rows[1].MaChuyenKhoan);
            Assert.Null(rows[2].MaChuyenKhoan);
        }

        [Fact]
        public async Task TheRow_CarriesTheTransactionFields_AndTheInvoiceFromTheOutcome()
        {
            _client.Enqueue(new[] { Tx("tx-9", "FT-9", amount: 750_000m, content: "TTPTHD7 tien phong") }, hasMore: false);
            _ingestion.Responder = t => new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded, Transfer = t, HoaDonId = 7, MaHoaDon = "HD-7", SoTienGhiNhan = 750_000m, ConLai = 1_250_000m
            };

            var row = Assert.Single((await SyncOkAsync()).Rows);

            Assert.Equal("tx-9", row.SePayApiId);
            Assert.Equal(TxTime, row.NgayGiaoDichUtc);
            Assert.Equal(750_000m, row.SoTien);
            Assert.Equal("TTPTHD7 tien phong", row.NoiDung);
            Assert.Equal("FT-9", row.MaThamChieu);
            Assert.Equal("TTPTHD7", row.MaChuyenKhoan);
            Assert.Equal(7, row.HoaDonId);
            Assert.Equal("HD-7", row.MaHoaDon);
        }

        // ---------- Ánh xạ kết quả (bảng X3) ----------

        [Fact]
        public async Task Recorded_IsNewlyRecorded_WithTheRecordedAmountAndWhatRemains()
        {
            _client.Enqueue(new[] { Tx() }, hasMore: false);
            _ingestion.Responder = t => new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded, Transfer = t, HoaDonId = 7, MaHoaDon = "HD-7",
                SoTienGhiNhan = 1_500_000m, ConLai = 500_000m
            };

            var row = Assert.Single((await SyncOkAsync()).Rows);

            Assert.Equal(SePaySyncRowStatus.MoiGhiNhan, row.TrangThai);
            Assert.Equal("Mới ghi nhận", row.NhanTrangThai);
            Assert.Equal("Ghi 1.500.000đ, còn lại 500.000đ", row.GhiChu);
        }

        [Fact]
        public async Task Recorded_WithASurplus_MentionsTheSurplus()
        {
            _client.Enqueue(new[] { Tx() }, hasMore: false);
            _ingestion.Responder = t => new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded, Transfer = t, HoaDonId = 7, SoTienGhiNhan = 2_000_000m, ConLai = 0m, TienThua = 100_000m
            };

            var row = Assert.Single((await SyncOkAsync()).Rows);

            Assert.Equal("Ghi 2.000.000đ, còn lại 0đ, tiền thừa 100.000đ", row.GhiChu);
        }

        [Theory]
        [InlineData(TransferOutcomeKind.Duplicate, SePaySyncRowStatus.DaCo, "Đã có", null)]
        [InlineData(TransferOutcomeKind.Unmatched, SePaySyncRowStatus.KhongKhopMa, "Không khớp mã", null)]
        [InlineData(TransferOutcomeKind.InvoiceNotPayable, SePaySyncRowStatus.HoaDonKhongNhanTien, "Hóa đơn không nhận tiền", "Hóa đơn đã hủy")]
        [InlineData(TransferOutcomeKind.AlreadyPaid, SePaySyncRowStatus.HoaDonKhongNhanTien, "Hóa đơn không nhận tiền", "Hóa đơn đã thu đủ")]
        [InlineData(TransferOutcomeKind.Ignored, SePaySyncRowStatus.BoQua, "Bỏ qua", "Số tiền không hợp lệ")]
        [InlineData(TransferOutcomeKind.WrongAccount, SePaySyncRowStatus.BoQua, "Bỏ qua", "Tiền vào tài khoản khác")]
        [InlineData(TransferOutcomeKind.Failed, SePaySyncRowStatus.LoiGhiSo, "Lỗi ghi sổ", "Lỗi hệ thống, bấm Đồng bộ lại")]
        public async Task EveryOutcomeKind_MapsToTheAgreedStatusLabelAndNote(
            TransferOutcomeKind kind, SePaySyncRowStatus status, string label, string? note)
        {
            _client.Enqueue(new[] { Tx() }, hasMore: false);
            _ingestion.Responder = t => new TransferOutcome
            {
                Kind = kind, Transfer = t, HoaDonId = 7, MaHoaDon = "HD-7",
                LyDo = kind == TransferOutcomeKind.InvoiceNotPayable ? "Hóa đơn đã hủy" : "chi tiết nội bộ không hiển thị"
            };

            var row = Assert.Single((await SyncOkAsync()).Rows);

            Assert.Equal(status, row.TrangThai);
            Assert.Equal(label, row.NhanTrangThai);
            Assert.Equal(note, row.GhiChu);
        }

        [Fact]
        public async Task EveryOutcomeKind_IsCovered()
        {
            var covered = new[]
            {
                TransferOutcomeKind.Recorded, TransferOutcomeKind.Duplicate, TransferOutcomeKind.Ignored, TransferOutcomeKind.WrongAccount,
                TransferOutcomeKind.Unmatched, TransferOutcomeKind.InvoiceNotPayable, TransferOutcomeKind.AlreadyPaid, TransferOutcomeKind.Failed
            };
            var all = Enum.GetValues<TransferOutcomeKind>();

            _client.Enqueue(all.Select((_, i) => Tx($"id-{i}", $"R{i}")).ToArray(), hasMore: false);
            var kinds = new Queue<TransferOutcomeKind>(all);
            _ingestion.Responder = t => new TransferOutcome { Kind = kinds.Dequeue(), Transfer = t };

            var data = await SyncOkAsync();

            Assert.Equal(all.Length, data.Rows.Count);
            Assert.All(all, kind => Assert.Contains(kind, covered));
        }

        [Fact]
        public async Task Totals_ListEveryStatus_IncludingTheEmptyOnes_WithTheirCounts()
        {
            _client.Enqueue(new[]
            {
                Tx("a", "R1"), Tx("b", "R2"), Tx("c", "R3"), Tx("d", null), Tx("e", "R5", type: "out")
            }, hasMore: false);
            var kinds = new Queue<TransferOutcomeKind>(new[] { TransferOutcomeKind.Recorded, TransferOutcomeKind.Recorded, TransferOutcomeKind.Duplicate });
            _ingestion.Responder = t => new TransferOutcome { Kind = kinds.Dequeue(), Transfer = t };

            var totals = (await SyncOkAsync()).TongTheoTrangThai;

            Assert.Equal(Enum.GetValues<SePaySyncRowStatus>(), totals.Select(t => t.TrangThai));
            int Count(SePaySyncRowStatus status) => totals.Single(t => t.TrangThai == status).SoLuong;
            Assert.Equal(2, Count(SePaySyncRowStatus.MoiGhiNhan));
            Assert.Equal(1, Count(SePaySyncRowStatus.DaCo));
            Assert.Equal(1, Count(SePaySyncRowStatus.ThieuMaThamChieu));
            Assert.Equal(1, Count(SePaySyncRowStatus.BoQua));
            Assert.Equal(0, Count(SePaySyncRowStatus.KhongKhopMa));
            Assert.Equal(0, Count(SePaySyncRowStatus.HoaDonKhongNhanTien));
            Assert.Equal(0, Count(SePaySyncRowStatus.LoiGhiSo));
            Assert.Equal(0, Count(SePaySyncRowStatus.ChuaGhiSo));
            Assert.Equal("Mới ghi nhận", totals.Single(t => t.TrangThai == SePaySyncRowStatus.MoiGhiNhan).Nhan);
            Assert.Equal("Chưa ghi sổ", totals.Single(t => t.TrangThai == SePaySyncRowStatus.ChuaGhiSo).Nhan);
        }

        [Fact]
        public async Task Totals_AreStillComplete_WhenNothingWasReturned()
        {
            _client.Enqueue(Array.Empty<SePayApiTransaction>(), hasMore: false);

            var totals = (await SyncOkAsync()).TongTheoTrangThai;

            Assert.Equal(Enum.GetValues<SePaySyncRowStatus>().Length, totals.Count);
            Assert.All(totals, t => Assert.Equal(0, t.SoLuong));
        }

        // ---------- Chế độ chỉ đối chiếu (SePay:SyncRecordEnabled = false) ----------

        [Fact]
        public async Task CheckOnlyMode_NeverIngests_AndMarksTransactionsMissingFromTheLedger()
        {
            _options.SyncRecordEnabled = false;
            _client.Enqueue(new[] { Tx("a", "FT-NEW") }, hasMore: false);

            var data = await SyncOkAsync();

            var row = Assert.Single(data.Rows);
            Assert.Equal(SePaySyncRowStatus.ChuaGhiSo, row.TrangThai);
            Assert.Equal("Chưa ghi sổ", row.NhanTrangThai);
            Assert.Equal("Chế độ chỉ đối chiếu (SePay:SyncRecordEnabled = false). Kiểm lịch sử thanh toán của hóa đơn trước khi Thu thủ công.", row.GhiChu);
            Assert.True(data.ChiDoiChieu);
            Assert.Empty(_ingestion.Transfers);
            Assert.Empty(_store.Ledger);
        }

        [Fact]
        public async Task CheckOnlyMode_MarksTransactionsAlreadyInTheLedger_IncludingVoidedRowsAndOtherCasing()
        {
            _options.SyncRecordEnabled = false;
            var h = new PaymentTestHarness();
            h.AddSentInvoice();
            h.AddSePayLedger(7, 1_000_000m, "FT-ACTIVE", PaymentTestHarness.Now.AddDays(-1));
            h.AddSePayLedger(7, 1_000_000m, "FT-VOIDED", PaymentTestHarness.Now.AddDays(-1), isDeleted: true);
            _client.Enqueue(new[]
            {
                Tx("a", "ft-active "), Tx("b", "FT-VOIDED"), Tx("c", "FT-OTHER")
            }, hasMore: false);
            var service = new SePaySyncService(_client, _ingestion, _access, _options, h.Store, _log);

            var rows = (await service.SyncAsync(AdminId, Range())).Data!.Rows;

            Assert.Equal(new[] { SePaySyncRowStatus.DaCo, SePaySyncRowStatus.DaCo, SePaySyncRowStatus.ChuaGhiSo }, rows.Select(r => r.TrangThai));
            Assert.Empty(_ingestion.Transfers);
            Assert.Equal(2, h.Store.Ledger.Count);
        }

        [Fact]
        public async Task CheckOnlyMode_StillSkipsOutgoingMoney_AndMissingReferences_WithoutTouchingTheLedger()
        {
            _options.SyncRecordEnabled = false;
            _client.Enqueue(new[] { Tx("a", "R1", type: "out"), Tx("b", reference: null) }, hasMore: false);

            var rows = (await SyncOkAsync()).Rows;

            Assert.Equal(SePaySyncRowStatus.BoQua, rows[0].TrangThai);
            Assert.Equal(SePaySyncRowStatus.ThieuMaThamChieu, rows[1].TrangThai);
            Assert.Empty(_ingestion.Transfers);
        }

        [Fact]
        public async Task RecordMode_IngestsInsteadOfOnlyComparing()
        {
            _options.SyncRecordEnabled = true;
            _client.Enqueue(new[] { Tx("a", "FT-NEW") }, hasMore: false);

            var data = await SyncOkAsync();

            Assert.Single(_ingestion.Transfers);
            Assert.False(data.ChiDoiChieu);
            Assert.Equal(SePaySyncRowStatus.MoiGhiNhan, Assert.Single(data.Rows).TrangThai);
        }

        // ---------- Hủy và nhật ký ----------

        [Fact]
        public async Task WhenTheCallerCancels_TheCancellationPropagates()
        {
            _client.Enqueue(new[] { Tx() }, hasMore: false);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().SyncAsync(AdminId, Range(), cts.Token));
        }

        [Fact]
        public async Task TheSummaryLog_HasCountsOnly_NeverTheTokenOrTheTransferText()
        {
            _client.Enqueue(new[] { Tx("a", "R1", content: "BI-MAT-NOI-DUNG"), Tx("b", reference: null, content: "BI-MAT-NOI-DUNG") }, hasMore: false);

            await SyncOkAsync();

            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains("MoiGhiNhan=1", entry.Message);
            Assert.Contains("ThieuMaThamChieu=1", entry.Message);
            Assert.DoesNotContain("api-token-secret", entry.Message);
            Assert.DoesNotContain("BI-MAT-NOI-DUNG", entry.Message);
            Assert.DoesNotContain("R1", entry.Message);
        }

        // ---------- Qua ghi sổ thật: chống trùng với webhook ----------

        private (SePaySyncService Service, PaymentTestHarness Harness, RecordingPaymentNotificationQueue Queue) RealIngestion()
        {
            var h = new PaymentTestHarness();
            h.AddSentInvoice();
            var queue = new RecordingPaymentNotificationQueue();
            var publisher = new PaymentOutcomePublisher(h.Notifier, queue, new FakeTransferAlertDeduplicator(), new CapturingLogger<PaymentOutcomePublisher>());
            var classifier = new IncomingTransferClassifier(
                new IIncomingTransferHandler[] { new InvoiceTransferHandler(h.Transaction) }, new VietQrSettings { AccountNumber = BankAccount });
            var ingestion = new BankTransferIngestionService(classifier, publisher, new CapturingLogger<BankTransferIngestionService>());
            var service = new SePaySyncService(_client, ingestion, h.Access, _options, h.Store, _log);
            return (service, h, queue);
        }

        [Fact]
        public async Task SyncingTheSameTransactionTwice_WritesOneLedgerRow_AndTheSecondRunReportsItExists()
        {
            var (service, h, _) = RealIngestion();
            _client.Fallback = _ => ServiceResult<SePayTransactionPage>.Ok(new SePayTransactionPage(new[] { Tx("a", "FT26ABC") }, false, 0));

            var first = (await service.SyncAsync(AdminId, Range())).Data!;
            var second = (await service.SyncAsync(AdminId, Range())).Data!;

            Assert.Equal(SePaySyncRowStatus.MoiGhiNhan, Assert.Single(first.Rows).TrangThai);
            Assert.Equal(SePaySyncRowStatus.DaCo, Assert.Single(second.Rows).TrangThai);
            var ledger = Assert.Single(h.Store.Ledger);
            Assert.Equal("SEPAY-FT26ABC", ledger.MaGiaoDich);
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task ATransactionTheWebhookAlreadyRecorded_IsReportedAsExisting_EvenWithDifferentCasingAndSpaces()
        {
            var (service, h, _) = RealIngestion();
            h.AddSePayLedger(7, 2_000_000m, "FT26ABC", PaymentTestHarness.Now.AddDays(-1));
            _client.Enqueue(new[] { Tx("a", "ft26abc ") }, hasMore: false);

            var row = Assert.Single((await service.SyncAsync(AdminId, Range())).Data!.Rows);

            Assert.Equal(SePaySyncRowStatus.DaCo, row.TrangThai);
            Assert.Single(h.Store.Ledger);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-500000)]
        public async Task ANonPositiveAmount_IsNeverRecorded_AndIsSkippedAsAnInvalidAmount(int amount)
        {
            var (service, h, queue) = RealIngestion();
            _client.Enqueue(new[] { Tx("a", "FT-ZERO", amount: amount) }, hasMore: false);

            var row = Assert.Single((await service.SyncAsync(AdminId, Range())).Data!.Rows);

            Assert.Equal(SePaySyncRowStatus.BoQua, row.TrangThai);
            Assert.Equal("Số tiền không hợp lệ", row.GhiChu);
            Assert.Empty(h.Store.Ledger);
            Assert.Empty(queue.Items);
        }

        [Fact]
        public async Task ATransactionTheWebhookRecordedAndAdminVoided_IsNotRecordedAgain()
        {
            var (service, h, _) = RealIngestion();
            h.AddSePayLedger(7, 2_000_000m, "FT26ABC", PaymentTestHarness.Now.AddDays(-1), isDeleted: true);
            _client.Enqueue(new[] { Tx("a", "FT26ABC") }, hasMore: false);

            var row = Assert.Single((await service.SyncAsync(AdminId, Range())).Data!.Rows);

            Assert.Equal(SePaySyncRowStatus.DaCo, row.TrangThai);
            Assert.Single(h.Store.Ledger);
            Assert.True(h.Store.Ledger.Single().IsDeleted);
        }

        [Fact]
        public async Task ResultsThatWriteNothing_SendNoTelegramAlert_ButARecordedRowStillQueuesItsNotifications()
        {
            var (service, _, queue) = RealIngestion();
            _client.Enqueue(new[]
            {
                // Không khớp mã, sai tài khoản, hóa đơn không tồn tại: ở webhook sẽ cảnh báo Telegram, ở đồng bộ bù thì không.
                Tx("a", "R-NOCODE", content: "chuyen tien tro"),
                Tx("b", "R-OTHER", account: "9999999999"),
                Tx("c", "R-GHOST", content: "TTPTHD999"),
                Tx("d", "R-OK", content: "TTPTHD7")
            }, hasMore: false);

            var data = (await service.SyncAsync(AdminId, Range())).Data!;

            Assert.Equal(
                new[] { SePaySyncRowStatus.KhongKhopMa, SePaySyncRowStatus.BoQua, SePaySyncRowStatus.HoaDonKhongNhanTien, SePaySyncRowStatus.MoiGhiNhan },
                data.Rows.Select(r => r.TrangThai));
            var queued = Assert.Single(queue.Items);
            Assert.Equal(TransferOutcomeKind.Recorded, queued.Outcome.Kind);
        }
    }
}
