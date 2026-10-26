using System;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // vnp_TxnRef của tiền giữ chỗ: GC{YeuCauGiuChoId}_{Ticks}, tách biệt với HD{HoaDonId}_{Ticks}.
    public class VnPayProtocolReservationTxnRefTests
    {
        [Fact]
        public void BuildAndParse_RoundTrip()
        {
            var now = new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc);

            var txnRef = VnPayProtocol.BuildReservationTxnRef(57, now);

            Assert.Equal($"GC57_{now.Ticks}", txnRef);
            Assert.True(VnPayProtocol.TryParseReservationTxnRef(txnRef, out var id));
            Assert.Equal(57, id);
            Assert.True(VnPayProtocol.IsReservationTxnRef(txnRef));
        }

        [Theory]
        [InlineData("HD7_1")]
        [InlineData("gc7_1")]
        [InlineData(" GC7_1")]
        [InlineData("GC7_1\n")]
        [InlineData("GC0_1")]
        [InlineData("GC1234567890_1")]
        [InlineData("GC7")]
        [InlineData("GC7_")]
        [InlineData("")]
        [InlineData(null)]
        public void TryParseReservationTxnRef_InvalidReference_ReturnsFalse(string? txnRef)
        {
            Assert.False(VnPayProtocol.TryParseReservationTxnRef(txnRef, out var id));
            Assert.Equal(0, id);
            Assert.False(VnPayProtocol.IsReservationTxnRef(txnRef));
        }

        [Fact]
        public void InvoiceParser_RejectsAReservationReference_AndTheOtherWayAround()
        {
            Assert.False(VnPayProtocol.TryParseTxnRef("GC7_1", out _));
            Assert.False(VnPayProtocol.TryParseReservationTxnRef("HD7_1", out _));
        }
    }
}
