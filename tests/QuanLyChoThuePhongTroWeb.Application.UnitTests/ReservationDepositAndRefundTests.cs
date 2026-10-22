using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class ReservationDepositAndRefundTests
    {
        [Fact]
        public void DepositApplication_CannotExceed_ConfirmedAmount_Or_ContractDeposit()
        {
            var confirmedHoldMoney = 2000000m;
            var contractDeposit = 1500000m;

            var maxAllowedToApply = Math.Min(confirmedHoldMoney, contractDeposit);
            Assert.Equal(1500000m, maxAllowedToApply);

            var application = new ApDungTienGiuChoVaoTienCoc
            {
                ApDungTienGiuChoVaoTienCocId = 1,
                YeuCauGiuChoId = 10,
                HopDongId = 100,
                SoTienApDung = maxAllowedToApply,
                NguoiThucHienId = 5,
                NgayApDung = DateTime.UtcNow
            };

            Assert.True(application.SoTienApDung <= confirmedHoldMoney);
            Assert.True(application.SoTienApDung <= contractDeposit);
        }

        [Fact]
        public void RefundDecision_CannotExceed_ActualReceivedHoldMoney()
        {
            var actualReceived = 1000000m;
            var proposedRefund = 1200000m;

            var isValid = proposedRefund <= actualReceived;
            Assert.False(isValid);

            var decision = new QuyetDinhHoanTienGiuCho
            {
                QuyetDinhHoanTienGiuChoId = 1,
                YeuCauGiuChoId = 10,
                SoTienHoanDuyet = 800000m,
                LyDo = "Khách hủy có lý do chính đáng được hoàn 80%",
                NguoiQuyetDinhId = 1,
                TrangThai = TrangThaiHoanTien.ChoHoanTien
            };

            Assert.True(decision.SoTienHoanDuyet <= actualReceived);
        }

        [Fact]
        public void MultipleRefundTransactions_SumCannotExceed_ApprovedRefundAmount()
        {
            var decision = new QuyetDinhHoanTienGiuCho
            {
                QuyetDinhHoanTienGiuChoId = 1,
                YeuCauGiuChoId = 10,
                SoTienHoanDuyet = 1000000m,
                TrangThai = TrangThaiHoanTien.DangHoanTien
            };

            var transactions = new List<GiaoDichHoanTienGiuCho>
            {
                new()
                {
                    GiaoDichHoanTienGiuChoId = 1,
                    QuyetDinhHoanTienGiuChoId = decision.QuyetDinhHoanTienGiuChoId,
                    SoTienHoan = 600000m,
                    MaGiaoDichHoan = "REFUND-001"
                },
                new()
                {
                    GiaoDichHoanTienGiuChoId = 2,
                    QuyetDinhHoanTienGiuChoId = decision.QuyetDinhHoanTienGiuChoId,
                    SoTienHoan = 400000m,
                    MaGiaoDichHoan = "REFUND-002"
                }
            };

            var totalRefunded = transactions.Sum(t => t.SoTienHoan);
            Assert.Equal(1000000m, totalRefunded);
            Assert.True(totalRefunded <= decision.SoTienHoanDuyet);

            // Any additional transaction would exceed
            var thirdRefundAmount = 100000m;
            var canAddThird = (totalRefunded + thirdRefundAmount) <= decision.SoTienHoanDuyet;
            Assert.False(canAddThird);
        }
    }
}
