using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Domain.Rules;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests;

public sealed class ReservationInvoiceCreditTests
{
    [Fact]
    public void CreditReducesAmountDueAndCreatesVisibleInvoiceLine()
    {
        var invoice = new HoaDon { TongTien = 2_000_000 };
        var applied = GiuChoRules.CanTruVaoHoaDon(invoice, 3, 1_000_000);
        Assert.Equal(1_000_000, applied);
        Assert.Equal(1_000_000, invoice.TongTien);
        Assert.Equal(-1_000_000, Assert.Single(invoice.ChiTietHoaDonDichVus).TongTien);
        Assert.Equal(1_000_000, invoice.CanTruGiuCho!.SoTienCanTru);
    }

    [Fact]
    public void InvoiceSmallerThanCreditConsumesOnlyItsAmountAndLeavesRemainder()
    {
        var invoice = new HoaDon { TongTien = 300_000 };
        var applied = GiuChoRules.CanTruVaoHoaDon(invoice, 3, 1_000_000);
        Assert.Equal(300_000, applied);
        Assert.Equal(0, invoice.TongTien);
        Assert.Equal(TrangThaiHoaDon.DaThanhToan, invoice.TrangThaiHoaDon);
        Assert.Equal(700_000, 1_000_000 - applied);
    }

    [Fact]
    public void SameInvoiceCannotReceiveCreditTwice()
    {
        var invoice = new HoaDon { TongTien = 2_000_000 };
        GiuChoRules.CanTruVaoHoaDon(invoice, 3, 1_000_000);
        Assert.Throws<InvalidOperationException>(() =>
            GiuChoRules.CanTruVaoHoaDon(invoice, 3, 1_000_000));
        Assert.Equal(1_000_000, invoice.TongTien);
    }
}
