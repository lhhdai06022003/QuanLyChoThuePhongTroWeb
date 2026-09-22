using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class ChiTietHoaDonConfiguration : IEntityTypeConfiguration<ChiTietHoaDon>
    {
        public void Configure(EntityTypeBuilder<ChiTietHoaDon> builder)
        {
            builder.Property(c => c.DonGia)
                .HasPrecision(18, 2);

            builder.Property(c => c.TongTien)
                .HasPrecision(18, 2);

            builder.Property(c => c.SoLuong)
                .HasPrecision(18, 3);
        }
    }
}
