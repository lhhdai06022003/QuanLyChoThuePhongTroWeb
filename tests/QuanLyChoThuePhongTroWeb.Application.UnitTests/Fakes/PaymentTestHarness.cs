using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Dựng sẵn các service thanh toán trên fake store để test theo kịch bản spec §32.
    public class PaymentTestHarness
    {
        public const int Branch = 1;
        public const int OtherBranch = 2;
        public const int TenantId = 50;
        public const int TenantUserId = 500;
        public const int StaffId = 10;
        public const int OtherStaffId = 11;
        public const int AdminId = 1;

        public static readonly DateTime Now = new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc);

        public FakeInvoicePaymentStore Store { get; } = new();
        public RecordingUnitOfWork UnitOfWork { get; } = new();
        public RecordingNotifier Notifier { get; } = new();
        public PaymentAccessFake Access { get; } = new();
        public FakeProofStorage Storage { get; } = new();
        public RecordingVietQr VietQr { get; } = new();
        public FixedTimeProvider Time { get; } = new(new DateTimeOffset(Now));
        public QueueCodeGenerator Codes { get; } = new();
        public InvoicePaymentOptions Options { get; } = new();
        public VietQrSettings VietQrSettings { get; } = new() { BankId = "MB", AccountNumber = "0123456789", AccountName = "NHA TRO" };

        public InvoicePaymentTransaction Transaction { get; }
        public InvoicePaymentRequestService Requests { get; }
        public InvoicePaymentConfirmationService Confirmation { get; }
        public InvoiceLedgerService Ledger { get; }

        public PaymentTestHarness()
        {
            UnitOfWork.OnSave = Store.AssignIds;
            Store.InTransaction = () => UnitOfWork.InTransaction;
            Store.AdminUserIds.Add(AdminId);
            Store.StaffUserIdsByBranch[Branch] = new List<int> { StaffId };
            Store.StaffUserIdsByBranch[OtherBranch] = new List<int> { OtherStaffId };
            Access.Admins.Add(AdminId);
            Access.StaffBranches[StaffId] = new HashSet<int> { Branch };
            Access.StaffBranches[OtherStaffId] = new HashSet<int> { OtherBranch };

            Transaction = new InvoicePaymentTransaction(Store, UnitOfWork, Notifier, NullLogger<InvoicePaymentTransaction>.Instance, Time);
            Requests = new InvoicePaymentRequestService(Store, Transaction, Codes, Storage, VietQr, VietQrSettings, Options, NullLogger<InvoicePaymentRequestService>.Instance);
            Confirmation = new InvoicePaymentConfirmationService(Store, Transaction, Access, Storage, Options, NullLogger<InvoicePaymentConfirmationService>.Instance);
            Ledger = new InvoiceLedgerService(Store, Transaction, Access, Codes, Options);
        }

        public HoaDon AddSentInvoice(int hoaDonId = 7, decimal tongTien = 2_000_000m, int chiNhanhId = Branch, bool choPhepMotPhan = false, decimal? toiThieu = null)
        {
            var hopDongId = hoaDonId + 100;
            var hoaDon = new HoaDon
            {
                HoaDonId = hoaDonId,
                MaHoaDon = $"HD-{hoaDonId}",
                HopDongId = hopDongId,
                TongTien = tongTien,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                NgayGui = Now.AddDays(-2),
                HanThanhToan = Now.AddDays(5),
                ChoPhepThanhToanMotPhan = choPhepMotPhan,
                SoTienThanhToanToiThieu = toiThieu
            };
            Store.Invoices[hoaDonId] = hoaDon;
            Store.Meta[hoaDonId] = new FakeInvoicePaymentStore.InvoiceMeta(chiNhanhId, TenantId);
            Store.TenantUserIdByHopDong[hopDongId] = TenantUserId;
            return hoaDon;
        }

        public YeuCauThanhToanHoaDon AddRequest(int hoaDonId = 7, decimal soTien = 2_000_000m, DateTime? createdAt = null, string? code = null)
        {
            var created = createdAt ?? Now.AddHours(-1);
            var request = YeuCauThanhToanHoaDon.Tao(hoaDonId, code ?? $"TT{hoaDonId}AAA{Store.Requests.Count:D3}", soTien, created.AddHours(24), TenantUserId, created);
            Store.Requests.Add(request);
            Store.AssignIds();
            return request;
        }

        public MinhChungThanhToanHoaDon AddProof(YeuCauThanhToanHoaDon request, string? maGiaoDich = null)
        {
            var proof = request.NopMinhChung("https://res.cloudinary.com/demo/proof.jpg", "payment-proofs/x", maGiaoDich, Now.AddMinutes(-30), TenantUserId, Now.AddMinutes(-20));
            Store.AssignIds();
            return proof;
        }

        public LichSuThanhToan AddLedger(int hoaDonId, decimal soTien, string maGiaoDich = "GD-OLD", bool isDeleted = false)
        {
            var ledger = new LichSuThanhToan
            {
                HoaDonId = hoaDonId,
                MaGiaoDich = maGiaoDich,
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                NgayThanhToan = Now.AddDays(-1),
                IsDeleted = isDeleted
            };
            Store.Ledger.Add(ledger);
            Store.AssignIds();
            var hoaDon = Store.Invoices[hoaDonId];
            var tong = Store.Ledger.Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted).Sum(l => l.SoTienThanhToan);
            hoaDon.TrangThaiHoaDon = tong == 0 ? TrangThaiHoaDon.ChuaThanhToan : tong == hoaDon.TongTien ? TrangThaiHoaDon.DaThanhToan : TrangThaiHoaDon.ThanhToanMotPhan;
            return ledger;
        }

        public static UploadFile JpegFile(string fileName = "bienlai.jpg")
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 2, 3 };
            return new UploadFile(new MemoryStream(bytes), fileName, "image/jpeg", bytes.Length);
        }

        public RecordingUnitOfWork.Tx LastTx => UnitOfWork.Transactions.Last();
    }

    public class RecordingUnitOfWork : IUnitOfWork
    {
        public sealed class Tx : IApplicationTransaction
        {
            private readonly RecordingUnitOfWork _owner;
            public Tx(RecordingUnitOfWork owner) => _owner = owner;
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                Committed = true;
                _owner.InTransaction = false;
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                RolledBack = true;
                _owner.InTransaction = false;
                return Task.CompletedTask;
            }

            public void Dispose() => _owner.InTransaction = false;
            public ValueTask DisposeAsync()
            {
                _owner.InTransaction = false;
                return ValueTask.CompletedTask;
            }
        }

        public List<Tx> Transactions { get; } = new();
        public bool InTransaction { get; private set; }
        public int SaveChangesCalls { get; private set; }
        public Action? OnSave { get; set; }
        // Trả exception để ném ở lần SaveChanges thứ n (bắt đầu từ 1), null để lưu bình thường.
        public Func<int, Exception?>? FailOnSave { get; set; }

        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tx = new Tx(this);
            Transactions.Add(tx);
            InTransaction = true;
            return Task.FromResult<IApplicationTransaction>(tx);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            var error = FailOnSave?.Invoke(SaveChangesCalls);
            if (error != null)
            {
                throw error;
            }

            OnSave?.Invoke();
            return Task.FromResult(1);
        }
    }

    public class RecordingNotifier : IThongBaoNotifier
    {
        public List<(int UserId, object Payload)> Sent { get; } = new();
        public bool Throw { get; set; }

        public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                throw new InvalidOperationException("SignalR down");
            }

            Sent.Add((nguoiDungId, payload));
            return Task.CompletedTask;
        }

        public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // Mô phỏng quy tắc của EmployeeAccessService: Admin làm mọi hành động hợp lệ; nhân viên chỉ ở chi nhánh
    // được phân công và không làm hành động Admin-only.
    public class PaymentAccessFake : IEmployeeAccessService
    {
        public HashSet<int> Admins { get; } = new();
        public Dictionary<int, HashSet<int>> StaffBranches { get; } = new();

        public Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default)
        {
            if (!EmployeeActionCodes.IsValid(action))
            {
                return Task.FromResult(false);
            }

            if (Admins.Contains(actorId))
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(!EmployeeActionCodes.IsAdminOnly(action) &&
                StaffBranches.TryGetValue(actorId, out var branches) && branches.Contains(chiNhanhId));
        }

        public Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default)
        {
            if (Admins.Contains(actorId))
            {
                return Task.FromResult<EmployeeAccessScope?>(new EmployeeAccessScope(actorId, true));
            }

            return Task.FromResult(StaffBranches.TryGetValue(actorId, out var branches)
                ? new EmployeeAccessScope(actorId, false, branches)
                : null);
        }
    }

    public class FakeProofStorage : IMeterImageStorageService
    {
        public List<(string FileName, string Folder)> Uploads { get; } = new();
        public List<string> Deleted { get; } = new();
        public bool ThrowOnUpload { get; set; }
        public bool ThrowOnDelete { get; set; }
        // Chạy sau khi tải ảnh, trước khi service ghi minh chứng (mô phỏng thay đổi đồng thời).
        public Action? AfterUpload { get; set; }

        public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
        {
            if (ThrowOnUpload)
            {
                throw new InvalidOperationException("Cloudinary down");
            }

            Uploads.Add((file.FileName, folder));
            AfterUpload?.Invoke();
            var stem = Path.GetFileNameWithoutExtension(file.FileName);
            return Task.FromResult(new MeterImageUploadResult($"https://res.cloudinary.com/demo/{folder}/{stem}_abc.jpg", $"{folder}/{stem}_abc"));
        }

        public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MeterImageReadResult(new byte[] { 1, 2, 3 }, "image/jpeg"));
        }

        public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnDelete)
            {
                throw new InvalidOperationException("delete failed");
            }

            Deleted.Add(publicId);
            return Task.CompletedTask;
        }
    }

    public class RecordingVietQr : IVietQRService
    {
        public decimal? LastAmount { get; private set; }
        public string? LastMemo { get; private set; }

        public string GenerateVietQRString(string bankIdOrBin, string accountNumber, decimal amount, string memo)
        {
            LastAmount = amount;
            LastMemo = memo;
            return $"QR|{amount}|{memo}";
        }

        public byte[] GenerateQRCodePNGBytes(string payload) => new byte[] { 0x89, 0x50 };
    }

    // Trả lần lượt các mã đã xếp hàng, hết hàng thì sinh mã thật.
    public class QueueCodeGenerator : IPaymentCodeGenerator
    {
        private readonly PaymentCodeGenerator _real = new();
        public Queue<string> RequestCodes { get; } = new();

        public string NewRequestCode(int hoaDonId) => RequestCodes.Count > 0 ? RequestCodes.Dequeue() : _real.NewRequestCode(hoaDonId);
        public string NewLedgerCode(string prefix, DateTime nowUtc) => _real.NewLedgerCode(prefix, nowUtc);
    }
}
