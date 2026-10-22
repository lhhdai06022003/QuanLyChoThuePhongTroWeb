using System;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    public class InvoicePaymentDomainTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc);

        private static HoaDon SentInvoice(decimal tongTien = 2_000_000m) => new HoaDon
        {
            HoaDonId = 7,
            TongTien = tongTien,
            TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui
        };

        private static YeuCauThanhToanHoaDon NewRequest(decimal soTien = 2_000_000m)
        {
            var yeuCau = YeuCauThanhToanHoaDon.Tao(7, "TT7ABCDEF", soTien, Now.AddHours(24), 99, Now);
            yeuCau.YeuCauThanhToanHoaDonId = 11;
            return yeuCau;
        }

        // ---------- HoaDon.CapNhatTrangThaiThanhToan ----------

        [Theory]
        [InlineData(0, TrangThaiHoaDon.ChuaThanhToan)]
        [InlineData(500_000, TrangThaiHoaDon.ThanhToanMotPhan)]
        [InlineData(2_000_000, TrangThaiHoaDon.DaThanhToan)]
        public void CapNhatTrangThaiThanhToan_MapsTotalToStatus(decimal tong, TrangThaiHoaDon expected)
        {
            var hoaDon = SentInvoice();

            hoaDon.CapNhatTrangThaiThanhToan(tong, 5, "lý do", Now);

            Assert.Equal(expected, hoaDon.TrangThaiHoaDon);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2_000_001)]
        public void CapNhatTrangThaiThanhToan_RejectsNegativeOrOverTotal(decimal tong)
        {
            var hoaDon = SentInvoice();

            Assert.Throws<InvalidOperationException>(() => hoaDon.CapNhatTrangThaiThanhToan(tong, 5, null, Now));
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.Empty(hoaDon.LichSuTrangThaiHoaDons);
        }

        [Fact]
        public void CapNhatTrangThaiThanhToan_WritesHistoryOnlyWhenChanged_UnlessForced()
        {
            var hoaDon = SentInvoice();

            Assert.False(hoaDon.CapNhatTrangThaiThanhToan(0, 5, "không đổi", Now));
            Assert.Empty(hoaDon.LichSuTrangThaiHoaDons);

            Assert.True(hoaDon.CapNhatTrangThaiThanhToan(500_000, 5, "trả một phần", Now));
            var row = Assert.Single(hoaDon.LichSuTrangThaiHoaDons);
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, row.TrangThaiThanhToanCu);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, row.TrangThaiThanhToanMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, row.TrangThaiPhatHanhCu);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, row.TrangThaiPhatHanhMoi);
            Assert.Equal(5, row.NguoiThucHienId);
            Assert.Equal(Now, row.NgayThucHien);

            Assert.False(hoaDon.CapNhatTrangThaiThanhToan(500_000, 1, "hủy ghi nhận", Now, luonGhiLichSu: true));
            Assert.Equal(2, hoaDon.LichSuTrangThaiHoaDons.Count);
            Assert.Equal("hủy ghi nhận", hoaDon.LichSuTrangThaiHoaDons.Last().LyDo);
        }

        // ---------- HoaDon.CauHinhThanhToanMotPhan ----------

        [Fact]
        public void CauHinhThanhToanMotPhan_RejectsFractionalMinimum()
        {
            var hoaDon = SentInvoice();

            Assert.Throws<ArgumentOutOfRangeException>(() => hoaDon.CauHinhThanhToanMotPhan(true, 500_000.5m, 1, Now));
            Assert.False(hoaDon.ChoPhepThanhToanMotPhan);
        }

        [Fact]
        public void CauHinhThanhToanMotPhan_WithActor_WritesHistoryKeepingStatus()
        {
            var hoaDon = SentInvoice();

            hoaDon.CauHinhThanhToanMotPhan(true, 500_000m, 1, Now);
            hoaDon.CauHinhThanhToanMotPhan(false, null, 1, Now);

            Assert.False(hoaDon.ChoPhepThanhToanMotPhan);
            Assert.Null(hoaDon.SoTienThanhToanToiThieu);
            Assert.Equal(2, hoaDon.LichSuTrangThaiHoaDons.Count);
            Assert.StartsWith("Bật thanh toán một phần, tối thiểu", hoaDon.LichSuTrangThaiHoaDons.First().LyDo);
            Assert.Equal("Tắt thanh toán một phần", hoaDon.LichSuTrangThaiHoaDons.Last().LyDo);
            Assert.All(hoaDon.LichSuTrangThaiHoaDons, r => Assert.Equal(r.TrangThaiThanhToanCu, r.TrangThaiThanhToanMoi));
        }

        [Fact]
        public void TongTienLaSoNguyen_DetectsFraction()
        {
            Assert.True(SentInvoice(1_233_333m).TongTienLaSoNguyen());
            Assert.False(SentInvoice(1_233_333.33m).TongTienLaSoNguyen());
        }

        // ---------- YeuCauThanhToanHoaDon ----------

        [Fact]
        public void Tao_SetsTransferContentToCode_AndWritesFirstHistory()
        {
            var yeuCau = NewRequest();

            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, yeuCau.TrangThai);
            Assert.Equal("TT7ABCDEF", yeuCau.NoiDungChuyenKhoan);
            Assert.Equal(Now.AddHours(24), yeuCau.HanThanhToan);
            var row = Assert.Single(yeuCau.LichSuTrangThaiYeuCauThanhToanHoaDons);
            Assert.Null(row.TrangThaiCu);
            Assert.Equal(99, row.NguoiThucHienId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(100.5)]
        public void Tao_RejectsNonWholeOrNonPositiveAmount(decimal soTien)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                YeuCauThanhToanHoaDon.Tao(7, "TT7ABCDEF", soTien, Now.AddHours(1), 1, Now));
        }

        [Fact]
        public void DangHoatDong_MatchesSpecDefinition_AtBoundary()
        {
            var predicate = YeuCauThanhToanHoaDon.DangHoatDong(Now).Compile();

            Assert.True(predicate(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan, HanThanhToan = Now }));
            Assert.False(predicate(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan, HanThanhToan = Now.AddTicks(-1) }));
            Assert.True(predicate(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.DangDoiChieu, HanThanhToan = Now.AddDays(-30) }));
            Assert.False(predicate(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.DangDoiChieu, IsDeleted = true }));
            Assert.False(predicate(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan, HanThanhToan = null }));
            Assert.False(predicate(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.HetHan, HanThanhToan = Now.AddDays(1) }));

            var expired = YeuCauThanhToanHoaDon.QuaHanChuaGhiNhan(Now).Compile();
            Assert.True(expired(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan, HanThanhToan = Now.AddTicks(-1) }));
            Assert.False(expired(new YeuCauThanhToanHoaDon { TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan, HanThanhToan = Now }));
        }

        [Fact]
        public void TrangThaiHieuLuc_ShowsExpiredWithoutWriting()
        {
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan,
                YeuCauThanhToanHoaDon.TrangThaiHieuLuc(TrangThaiYeuCauThanhToan.ChoThanhToan, Now.AddMinutes(-1), Now));
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu,
                YeuCauThanhToanHoaDon.TrangThaiHieuLuc(TrangThaiYeuCauThanhToan.DangDoiChieu, Now.AddMinutes(-1), Now));
        }

        [Fact]
        public void NopMinhChung_DeclaresRequestAmount_AndMovesToReview()
        {
            var yeuCau = NewRequest(1_500_000m);

            var minhChung = yeuCau.NopMinhChung("https://img", "payment-proofs/TT7ABCDEF_x", "FT123", Now.AddMinutes(-10), 99, Now);

            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, yeuCau.TrangThai);
            Assert.Equal(1_500_000m, minhChung.SoTienKhaiBao);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, minhChung.TrangThaiDoiChieu);
            Assert.Equal(Now, minhChung.NgayTao);
            Assert.Contains(minhChung, yeuCau.MinhChungThanhToanHoaDons);
        }

        [Fact]
        public void NopMinhChung_RejectsExpiredRequest()
        {
            var yeuCau = NewRequest();

            Assert.Throws<InvalidOperationException>(() =>
                yeuCau.NopMinhChung("https://img", "pid", null, Now, 99, Now.AddHours(25)));
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, yeuCau.TrangThai);
        }

        [Fact]
        public void TuChoiMinhChung_ReturnsRequestToWaiting_WithNewDeadline()
        {
            var yeuCau = NewRequest();
            var minhChung = yeuCau.NopMinhChung("https://img", "pid", null, Now, 99, Now);
            var later = Now.AddHours(30);

            yeuCau.TuChoiMinhChung(minhChung, 5, "Không thấy giao dịch", later, later.AddHours(24));

            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, yeuCau.TrangThai);
            Assert.Equal(later.AddHours(24), yeuCau.HanThanhToan);
            Assert.Equal(TrangThaiMinhChungThanhToan.TuChoi, minhChung.TrangThaiDoiChieu);
            Assert.Equal("Không thấy giao dịch", minhChung.LyDoTuChoi);
            Assert.Equal(5, minhChung.NguoiDoiChieuId);
        }

        [Fact]
        public void HoanTat_ConfirmsProof_AndClosesRequest()
        {
            var yeuCau = NewRequest();
            var minhChung = yeuCau.NopMinhChung("https://img", "pid", null, Now, 99, Now);

            yeuCau.HoanTat(minhChung, 5, Now, "Đối chiếu khớp");

            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, yeuCau.TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.DaXacNhan, minhChung.TrangThaiDoiChieu);
            Assert.Throws<InvalidOperationException>(() => minhChung.XacNhan(5, Now));
        }

        [Fact]
        public void GhiChuyenAdmin_KeepsStatus_AndAddsSelfTransitionRow()
        {
            var yeuCau = NewRequest();
            yeuCau.NopMinhChung("https://img", "pid", null, Now, 99, Now);

            yeuCau.GhiChuyenAdmin(5, Now, "[CHO-ADMIN] Thực nhận 2.500.000");

            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, yeuCau.TrangThai);
            var row = yeuCau.LichSuTrangThaiYeuCauThanhToanHoaDons.Last();
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, row.TrangThaiCu);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, row.TrangThaiMoi);
        }

        [Fact]
        public void TerminalStates_RejectFurtherTransitions()
        {
            var huy = NewRequest();
            huy.Huy(99, Now, "Khách hủy");
            Assert.Throws<InvalidOperationException>(() => huy.DanhDauHetHan(Now));

            var hetHan = NewRequest();
            hetHan.DanhDauHetHan(Now.AddHours(25));
            Assert.Null(hetHan.LichSuTrangThaiYeuCauThanhToanHoaDons.Last().NguoiThucHienId);
            Assert.Throws<InvalidOperationException>(() => hetHan.Huy(99, Now, "x"));

            var dangDoiChieu = NewRequest();
            dangDoiChieu.NopMinhChung("https://img", "pid", null, Now, 99, Now);
            Assert.Throws<InvalidOperationException>(() => dangDoiChieu.Huy(99, Now, "x"));
            Assert.Throws<InvalidOperationException>(() => dangDoiChieu.DanhDauHetHan(Now.AddDays(5)));
        }

        // ---------- LichSuThanhToan ----------

        [Fact]
        public void HuyGhiNhan_SoftDeletesOnce()
        {
            var ledger = new LichSuThanhToan { SoTienThanhToan = 100_000m, GhiChu = "cũ" };

            ledger.HuyGhiNhan("cũ | [DA-HUY] lý do");

            Assert.True(ledger.IsDeleted);
            Assert.Equal(100_000m, ledger.SoTienThanhToan);
            Assert.Throws<InvalidOperationException>(() => ledger.HuyGhiNhan("lần hai"));
        }
    }
}
