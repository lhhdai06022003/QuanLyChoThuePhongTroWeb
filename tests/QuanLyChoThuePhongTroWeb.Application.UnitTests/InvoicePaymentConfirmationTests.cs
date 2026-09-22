using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoicePaymentConfirmationTests
    {
        private class FakeTransaction : IApplicationTransaction
        {
            public bool IsCommitted { get; private set; }
            public bool IsRolledBack { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                IsCommitted = true;
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                IsRolledBack = true;
                return Task.CompletedTask;
            }

            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class FakeUnitOfWork : IUnitOfWork
        {
            public FakeTransaction LastTransaction { get; } = new FakeTransaction();
            public int SaveChangesCount { get; private set; }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCount++;
                return Task.FromResult(1);
            }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IApplicationTransaction>(LastTransaction);
            }
        }

        private class FakeInvoicePaymentStore : IInvoicePaymentStore
        {
            public Dictionary<int, HoaDon> HoaDons { get; } = new Dictionary<int, HoaDon>();
            public Dictionary<int, YeuCauThanhToanHoaDon> YeuCaus { get; } = new Dictionary<int, YeuCauThanhToanHoaDon>();
            public Dictionary<int, MinhChungThanhToanHoaDon> MinhChungs { get; } = new Dictionary<int, MinhChungThanhToanHoaDon>();
            public List<LichSuThanhToan> LichSuThanhToans { get; } = new List<LichSuThanhToan>();
            public List<LichSuTrangThaiYeuCauThanhToanHoaDon> LichSuYeuCaus { get; } = new List<LichSuTrangThaiYeuCauThanhToanHoaDon>();
            public List<LichSuTrangThaiHoaDon> LichSuHoaDons { get; } = new List<LichSuTrangThaiHoaDon>();

            public decimal CustomTongTienDaThanhToan { get; set; } = -1;

            public Task<HoaDon?> GetHoaDonByIdAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                HoaDons.TryGetValue(hoaDonId, out var hd);
                return Task.FromResult(hd);
            }

            public Task<YeuCauThanhToanHoaDon?> GetYeuCauThanhToanByIdAsync(int yeuCauId, CancellationToken cancellationToken = default)
            {
                YeuCaus.TryGetValue(yeuCauId, out var yc);
                return Task.FromResult(yc);
            }

            public Task<MinhChungThanhToanHoaDon?> GetMinhChungByIdWithDetailsAsync(int minhChungId, CancellationToken cancellationToken = default)
            {
                MinhChungs.TryGetValue(minhChungId, out var mc);
                return Task.FromResult(mc);
            }

            public Task<decimal> GetTongTienDaThanhToanHoaDonAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                if (CustomTongTienDaThanhToan >= 0)
                {
                    return Task.FromResult(CustomTongTienDaThanhToan);
                }

                var total = LichSuThanhToans
                    .Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted)
                    .Sum(l => l.SoTienThanhToan);
                return Task.FromResult(total);
            }

            public Task AddYeuCauThanhToanAsync(YeuCauThanhToanHoaDon yeuCau, CancellationToken cancellationToken = default)
            {
                if (yeuCau.YeuCauThanhToanHoaDonId == 0)
                {
                    yeuCau.YeuCauThanhToanHoaDonId = YeuCaus.Count + 1;
                }
                YeuCaus[yeuCau.YeuCauThanhToanHoaDonId] = yeuCau;
                return Task.CompletedTask;
            }

            public Task AddMinhChungThanhToanAsync(MinhChungThanhToanHoaDon minhChung, CancellationToken cancellationToken = default)
            {
                if (minhChung.MinhChungThanhToanHoaDonId == 0)
                {
                    minhChung.MinhChungThanhToanHoaDonId = MinhChungs.Count + 1;
                }
                MinhChungs[minhChung.MinhChungThanhToanHoaDonId] = minhChung;
                return Task.CompletedTask;
            }

            public Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default)
            {
                if (lichSu.LichSuThanhToanId == 0)
                {
                    lichSu.LichSuThanhToanId = LichSuThanhToans.Count + 1;
                }
                LichSuThanhToans.Add(lichSu);
                return Task.CompletedTask;
            }

            public Task AddLichSuTrangThaiYeuCauAsync(LichSuTrangThaiYeuCauThanhToanHoaDon lichSu, CancellationToken cancellationToken = default)
            {
                LichSuYeuCaus.Add(lichSu);
                return Task.CompletedTask;
            }

            public Task AddLichSuTrangThaiHoaDonAsync(LichSuTrangThaiHoaDon lichSu, CancellationToken cancellationToken = default)
            {
                LichSuHoaDons.Add(lichSu);
                return Task.CompletedTask;
            }

            public void UpdateYeuCauThanhToan(YeuCauThanhToanHoaDon yeuCau) => YeuCaus[yeuCau.YeuCauThanhToanHoaDonId] = yeuCau;
            public void UpdateMinhChungThanhToan(MinhChungThanhToanHoaDon minhChung) => MinhChungs[minhChung.MinhChungThanhToanHoaDonId] = minhChung;
            public void UpdateHoaDon(HoaDon hoaDon) => HoaDons[hoaDon.HoaDonId] = hoaDon;
        }

        private readonly FakeInvoicePaymentStore _store;
        private readonly FakeUnitOfWork _unitOfWork;
        private readonly InvoicePaymentConfirmationService _service;

        public InvoicePaymentConfirmationTests()
        {
            _store = new FakeInvoicePaymentStore();
            _unitOfWork = new FakeUnitOfWork();
            _service = new InvoicePaymentConfirmationService(
                _store,
                _unitOfWork,
                NullLogger<InvoicePaymentConfirmationService>.Instance);
        }

        [Fact]
        public async Task TaoYeuCauThanhToan_BelowMinimum_ThrowsInvalidOperationException()
        {
            var hoaDon = new HoaDon
            {
                HoaDonId = 1,
                TongTien = 3_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                ChoPhepThanhToanMotPhan = true,
                SoTienThanhToanToiThieu = 1_000_000m
            };
            _store.HoaDons[1] = hoaDon;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.TaoYeuCauThanhToanAsync(1, 500_000m, "Thanh toan tien phong"));

            Assert.Contains("tối thiểu", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task XacNhanMinhChung_DuplicateConfirmation_ThrowsInvalidOperationException()
        {
            var minhChung = new MinhChungThanhToanHoaDon
            {
                MinhChungThanhToanHoaDonId = 10,
                TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.DaXacNhan
            };
            _store.MinhChungs[10] = minhChung;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.XacNhanMinhChungAsync(10, nguoiXacNhanId: 1));

            Assert.Contains("đã được xác nhận trước đó", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(_unitOfWork.LastTransaction.IsRolledBack);
        }

        [Fact]
        public async Task XacNhanMinhChung_ExceedsTotalInvoice_ThrowsInvalidOperationException()
        {
            var hoaDon = new HoaDon
            {
                HoaDonId = 1,
                TongTien = 2_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan
            };

            var yeuCau = new YeuCauThanhToanHoaDon
            {
                YeuCauThanhToanHoaDonId = 5,
                HoaDonId = 1,
                HoaDon = hoaDon,
                SoTien = 1_500_000m
            };

            var minhChung = new MinhChungThanhToanHoaDon
            {
                MinhChungThanhToanHoaDonId = 20,
                YeuCauThanhToanHoaDonId = 5,
                YeuCauThanhToanHoaDon = yeuCau,
                SoTienKhaiBao = 1_500_000m,
                NgayChuyenKhaiBao = DateTime.UtcNow,
                TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.ChoXacNhan
            };

            _store.HoaDons[1] = hoaDon;
            _store.YeuCaus[5] = yeuCau;
            _store.MinhChungs[20] = minhChung;
            // Đã trả 1.000.000 trước đó, trả thêm 1.500.000 -> 2.500.000 > 2.000.000
            _store.CustomTongTienDaThanhToan = 1_000_000m;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.XacNhanMinhChungAsync(20, nguoiXacNhanId: 1));

            Assert.Contains("vượt quá tổng tiền hóa đơn", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(_unitOfWork.LastTransaction.IsRolledBack);
        }

        [Fact]
        public async Task RejectedEvidence_CanResubmitAndBeConfirmed()
        {
            var hoaDon = new HoaDon
            {
                HoaDonId = 1,
                TongTien = 2_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan
            };

            var yeuCau = new YeuCauThanhToanHoaDon
            {
                YeuCauThanhToanHoaDonId = 5,
                HoaDonId = 1,
                HoaDon = hoaDon,
                SoTien = 2_000_000m,
                TrangThai = TrangThaiYeuCauThanhToan.TuChoi // Đã bị từ chối lần 1
            };

            _store.HoaDons[1] = hoaDon;
            _store.YeuCaus[5] = yeuCau;

            // Nộp lại minh chứng mới cho yêu cầu này
            var res = await _service.NopMinhChungAsync(
                yeuCauId: 5,
                hinhAnhUrl: "https://example.com/receipt2.jpg",
                soTienKhaiBao: 2_000_000m,
                ngayChuyen: DateTime.UtcNow,
                maGiaoDich: "TX99999");

            Assert.NotNull(res);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, res.TrangThaiDoiChieu);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, yeuCau.TrangThai);

            // Gắn navigation để giả lập EF include
            var entity = _store.MinhChungs[res.MinhChungThanhToanHoaDonId];
            entity.YeuCauThanhToanHoaDon = yeuCau;

            var paymentRecord = await _service.XacNhanMinhChungAsync(res.MinhChungThanhToanHoaDonId, nguoiXacNhanId: 2);

            Assert.NotNull(paymentRecord);
            Assert.Equal(2_000_000m, paymentRecord.SoTienThanhToan);
            Assert.Equal(res.MinhChungThanhToanHoaDonId, paymentRecord.MinhChungThanhToanHoaDonId);
            Assert.Equal(TrangThaiMinhChungThanhToan.DaXacNhan, entity.TrangThaiDoiChieu);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, yeuCau.TrangThai);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.True(_unitOfWork.LastTransaction.IsCommitted);
        }
    }
}
