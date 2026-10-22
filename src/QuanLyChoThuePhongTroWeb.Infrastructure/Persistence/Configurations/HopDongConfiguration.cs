using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class HopDongConfiguration : IEntityTypeConfiguration<HopDong>
    {
        public void Configure(EntityTypeBuilder<HopDong> builder)
        {
            builder.Property(h => h.TienCocPhong)
                .HasPrecision(18, 2);

            builder.Property(h => h.TienThuePhong)
                .HasPrecision(18, 2);
        }
    }
}
