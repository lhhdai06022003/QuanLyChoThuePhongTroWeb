using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class LichSuThanhToanConfiguration : IEntityTypeConfiguration<LichSuThanhToan>
    {
        public void Configure(EntityTypeBuilder<LichSuThanhToan> builder)
        {
            builder.Property(l => l.SoTienThanhToan)
                .HasPrecision(18, 2);

            builder.HasOne(l => l.MinhChungThanhToanHoaDon)
                .WithOne(m => m.LichSuThanhToan)
                .HasForeignKey<LichSuThanhToan>(l => l.MinhChungThanhToanHoaDonId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(l => l.MinhChungThanhToanHoaDonId)
                .IsUnique()
                .HasFilter("\"MinhChungThanhToanHoaDonId\" IS NOT NULL");

            builder.HasIndex(l => l.MaGiaoDich)
                .IsUnique()
                .HasFilter("\"MaGiaoDich\" IS NOT NULL AND \"MaGiaoDich\" <> '' AND \"IsDeleted\" = false");
        }
    }
}
