using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class YeuCauThanhToanHoaDonConfiguration : IEntityTypeConfiguration<YeuCauThanhToanHoaDon>
    {
        public void Configure(EntityTypeBuilder<YeuCauThanhToanHoaDon> builder)
        {
            builder.HasOne(y => y.HoaDon)
                .WithMany(h => h.YeuCauThanhToanHoaDons)
                .HasForeignKey(y => y.HoaDonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(y => y.SoTien)
                .HasPrecision(18, 2);

            builder.Property(y => y.MaYeuCau)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(y => y.MaYeuCau)
                .IsUnique();
        }
    }
}
