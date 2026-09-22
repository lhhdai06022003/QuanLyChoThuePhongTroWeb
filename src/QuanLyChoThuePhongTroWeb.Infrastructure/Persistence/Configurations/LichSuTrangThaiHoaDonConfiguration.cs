using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class LichSuTrangThaiHoaDonConfiguration : IEntityTypeConfiguration<LichSuTrangThaiHoaDon>
    {
        public void Configure(EntityTypeBuilder<LichSuTrangThaiHoaDon> builder)
        {
            builder.HasOne(l => l.HoaDon)
                .WithMany(h => h.LichSuTrangThaiHoaDons)
                .HasForeignKey(l => l.HoaDonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
