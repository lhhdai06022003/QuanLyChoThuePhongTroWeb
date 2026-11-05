using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ThanhLyCalculatorTests
    {
        private static readonly DateTime EndOct = new(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc);

        private static ThanhLyHoaDonInput Inv(int id, int thang, decimal tong, decimal daThu = 0,
            TrangThaiPhatHanhHoaDon st = TrangThaiPhatHanhHoaDon.DaGui, int hopDongId = 10, bool doiChieu = false, bool nguyen = true, int nam = 2026)
            => new(id, "HDN-" + id, hopDongId, "HD-" + hopDongId, thang, nam, tong, daThu, st, doiChieu, nguyen);

        [Fact]
        public void DepositLargerThanDebt_RefundsRest()
        {
            var r = ThanhLyCalculator.Tinh(5_000_000m, new[] { Inv(1, 10, 3_000_000m) });

            Assert.Equal(3_000_000m, r.TongNo);
            Assert.Equal(3_000_000m, r.CanTru);
            Assert.Equal(2_000_000m, r.CocHoanTrucTiep);
            Assert.Equal(0m, r.ConNoSauCanTru);
        }

        [Fact]
        public void DepositSmallerThanDebt_LeavesDebt()
        {
            var r = ThanhLyCalculator.Tinh(1_000_000m, new[] { Inv(1, 10, 3_000_000m, daThu: 500_000m) });

            Assert.Equal(2_500_000m, r.TongNo);
            Assert.Equal(1_000_000m, r.CanTru);
            Assert.Equal(0m, r.CocHoanTrucTiep);
            Assert.Equal(1_500_000m, r.ConNoSauCanTru);
        }

        [Fact]
        public void NoDebt_AndZeroDeposit()
        {
            var noDebt = ThanhLyCalculator.Tinh(2_000_000m, new[] { Inv(1, 10, 1_000_000m, daThu: 1_000_000m) });
            Assert.Equal(0m, noDebt.TongNo);
            Assert.Equal(0m, noDebt.CanTru);
            Assert.Equal(2_000_000m, noDebt.CocHoanTrucTiep);
            Assert.Empty(noDebt.HoaDonNo);

            var zeroDeposit = ThanhLyCalculator.Tinh(0m, new[] { Inv(1, 10, 1_000_000m) });
            Assert.Equal(0m, zeroDeposit.CanTru);
            Assert.Equal(1_000_000m, zeroDeposit.ConNoSauCanTru);
        }

        [Fact]
        public void OldestPeriodFirst_RegardlessOfInputOrder()
        {
            var r = ThanhLyCalculator.Tinh(1_200_000m, new[]
            {
                Inv(3, 10, 1_000_000m),
                Inv(1, 8, 1_000_000m),
                Inv(2, 9, 1_000_000m),
                Inv(4, 12, 1_000_000m, nam: 2025)
            });

            Assert.Equal(new[] { 4, 1, 2, 3 }, r.HoaDonNo.Select(p => p.HoaDonId).ToArray());
            Assert.Equal(new[] { 1_000_000m, 200_000m, 0m, 0m }, r.HoaDonNo.Select(p => p.CanTru).ToArray());
        }

        [Fact]
        public void OnlyPublishedInvoicesCountAsDebt()
        {
            var r = ThanhLyCalculator.Tinh(10_000_000m, new[]
            {
                Inv(1, 9, 1_000_000m, st: TrangThaiPhatHanhHoaDon.Nhap),
                Inv(2, 10, 1_000_000m, st: TrangThaiPhatHanhHoaDon.DaChot),
                Inv(3, 10, 1_000_000m)
            });

            Assert.Equal(1_000_000m, r.TongNo);
            Assert.Equal(3, Assert.Single(r.HoaDonNo).HoaDonId);
        }

        [Fact]
        public void Block1_ContractNotActive()
        {
            var lyDo = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DaKetThuc, EndOct, 0, 10, new[] { Inv(1, 10, 1m) });
            Assert.Equal(new[] { "Hợp đồng không còn hoạt động." }, lyDo.ToArray());
        }

        [Fact]
        public void Block2_NoReturnDate()
        {
            var lyDo = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, null, 0, 10, new[] { Inv(1, 10, 1m) });
            Assert.Single(lyDo);
            Assert.Contains("chưa có ngày trả phòng", lyDo[0]);
        }

        [Fact]
        public void Block3_ReturnMonthNeedsPublishedInvoice_ByVietnamMonth()
        {
            // 31/10 23:59:59 VN là tháng 10; 01/11 00:00:00 VN là tháng 11.
            var invOct = new[] { Inv(1, 10, 1m, daThu: 1m) };
            Assert.Empty(ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc), 0, 10, invOct));

            var nov = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), 0, 10, invOct);
            Assert.Single(nov);
            Assert.Contains("T11/2026", nov[0]);
        }

        [Fact]
        public void Block3_IgnoresInvoicesOfOtherContractsInChain()
        {
            var inChain = new[] { Inv(1, 10, 1m, daThu: 1m, hopDongId: 9) };

            var lyDo = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, EndOct, 0, 10, inChain);

            Assert.Single(lyDo);
            Assert.Contains("T10/2026", lyDo[0]);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap, "Nháp")]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet, "Chờ duyệt")]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot, "Đã chốt")]
        public void Block4_UnpublishedInvoices_InAnyContractOfChain(TrangThaiPhatHanhHoaDon st, string ten)
        {
            var inputs = new[] { Inv(1, 10, 1m, daThu: 1m), Inv(2, 6, 5m, st: st, hopDongId: 9) };

            var lyDo = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, EndOct, 0, 10, inputs);

            var line = Assert.Single(lyDo);
            Assert.Contains("HDN-2", line);
            Assert.Contains(ten, line);
        }

        [Fact]
        public void Block5_ReviewingProof_OnlyForDebtInvoices()
        {
            var inputs = new[]
            {
                Inv(1, 10, 2_000_000m, doiChieu: true),
                Inv(2, 9, 1_000_000m, daThu: 1_000_000m, doiChieu: true)
            };

            var lyDo = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, EndOct, 0, 10, inputs);

            var line = Assert.Single(lyDo);
            Assert.Contains("HDN-1", line);
            Assert.Contains("đối chiếu", line);
        }

        [Fact]
        public void Block6_OddInvoiceAmount_AndOddDeposit()
        {
            var odd = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, EndOct, 0, 10, new[] { Inv(1, 10, 1.5m, nguyen: false) });
            Assert.Contains(odd, l => l.Contains("số tiền lẻ"));

            var oddCoc = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, EndOct, 100.5m, 10, new[] { Inv(1, 10, 1m, daThu: 1m) });
            Assert.Equal(new[] { "Tiền cọc có số lẻ, không cấn trừ được." }, oddCoc.ToArray());
        }

        [Fact]
        public void AllClear_ReturnsEmpty()
        {
            var lyDo = ThanhLyCalculator.TimLyDoChan(TrangThaiHopDong.DangHoatDong, EndOct, 3_000_000m, 10, new List<ThanhLyHoaDonInput> { Inv(1, 10, 2_000_000m) });
            Assert.Empty(lyDo);
        }
    }
}
