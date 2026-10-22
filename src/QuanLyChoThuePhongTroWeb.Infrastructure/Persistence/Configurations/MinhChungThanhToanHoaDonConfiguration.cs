using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class MinhChungThanhToanHoaDonConfiguration : IEntityTypeConfiguration<MinhChungThanhToanHoaDon>
    {
        public void Configure(EntityTypeBuilder<MinhChungThanhToanHoaDon> builder)
        {
            builder.HasOne(m => m.YeuCauThanhToanHoaDon)
                .WithMany(y => y.MinhChungThanhToanHoaDons)
                .HasForeignKey(m => m.YeuCauThanhToanHoaDonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(m => m.SoTienKhaiBao)
                .HasPrecision(18, 2);

            builder.Property(m => m.HinhAnhUrl)
                .IsRequired();

            builder.Property(m => m.PublicId)
                .HasMaxLength(255);

            builder.Property(m => m.MaGiaoDichNganHang)
                .HasMaxLength(100);
        }
    }
}
