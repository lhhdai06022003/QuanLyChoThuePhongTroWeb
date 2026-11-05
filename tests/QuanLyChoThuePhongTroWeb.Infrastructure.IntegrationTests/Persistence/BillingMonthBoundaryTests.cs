using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Cặp hợp đồng gia hạn (cũ DaKetThuc đến 31/10 23:59:59 VN, mới từ 01/11 00:00 VN) không được tính trùng hay hụt tháng khi lập hóa đơn.
    [Collection("PostgreSqlCollection")]
    public class BillingMonthBoundaryTests
    {
        private readonly PostgreSqlFixture _fixture;

        public BillingMonthBoundaryTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ValidContractsForBilling_RenewalPair_SplitsAtVietnamMonthBoundary()
        {
            await using var db = _fixture.CreateDbContext();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("B");
            var tenant = await seed.TenantAsync("B");
            var room = await seed.RoomAsync(branch, "B");
            var old = await seed.ContractAsync(room, tenant,
                start: new DateTime(2025, 10, 31, 17, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc),
                status: TrangThaiHopDong.DaKetThuc);
            var renewed = await seed.ContractAsync(room, tenant,
                start: new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2027, 10, 31, 16, 59, 59, DateTimeKind.Utc));

            // Giống InvoiceIssuanceService: đầu tháng = ngày 1 00:00 VN, cuối tháng = ngày cuối 23:59:59 VN.
            static (DateTime Start, DateTime End) Month(int year, int month)
            {
                var start = DateTime.SpecifyKind(new DateTime(year, month, 1), DateTimeKind.Utc).AddHours(-7);
                var last = DateTime.DaysInMonth(year, month);
                var end = DateTime.SpecifyKind(new DateTime(year, month, last, 23, 59, 59), DateTimeKind.Utc).AddHours(-7);
                return (start, end);
            }

            var store = new HoaDonStore(db);
            var (octStart, octEnd) = Month(2026, 10);
            var (novStart, novEnd) = Month(2026, 11);

            var october = await store.GetValidContractsForBillingAsync(branch.ChiNhanhId, new[] { room.PhongTroId }, octStart, octEnd);
            var november = await store.GetValidContractsForBillingAsync(branch.ChiNhanhId, new[] { room.PhongTroId }, novStart, novEnd);

            Assert.Equal(new[] { old.HopDongId }, october.Select(c => c.HopDongId).ToArray());
            Assert.Equal(new[] { renewed.HopDongId }, november.Select(c => c.HopDongId).ToArray());
        }
    }
}
