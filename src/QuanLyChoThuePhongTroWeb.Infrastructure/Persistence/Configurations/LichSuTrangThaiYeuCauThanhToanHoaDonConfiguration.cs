using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class LichSuTrangThaiYeuCauThanhToanHoaDonConfiguration : IEntityTypeConfiguration<LichSuTrangThaiYeuCauThanhToanHoaDon>
    {
        public void Configure(EntityTypeBuilder<LichSuTrangThaiYeuCauThanhToanHoaDon> builder)
        {
            builder.HasOne(l => l.YeuCauThanhToanHoaDon)
                .WithMany(y => y.LichSuTrangThaiYeuCauThanhToanHoaDons)
                .HasForeignKey(l => l.YeuCauThanhToanHoaDonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
