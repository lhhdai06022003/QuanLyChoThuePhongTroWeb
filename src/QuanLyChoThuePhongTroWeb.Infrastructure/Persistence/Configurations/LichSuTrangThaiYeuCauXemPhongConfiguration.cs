using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class LichSuTrangThaiYeuCauXemPhongConfiguration : IEntityTypeConfiguration<LichSuTrangThaiYeuCauXemPhong>
    {
        public void Configure(EntityTypeBuilder<LichSuTrangThaiYeuCauXemPhong> builder)
        {
            builder.Property(l => l.LyDo)
                .HasMaxLength(500);

            builder.HasOne(l => l.YeuCauXemPhong)
                .WithMany(y => y.LichSuTrangThais)
                .HasForeignKey(l => l.YeuCauXemPhongId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(l => l.NguoiThucHien)
                .WithMany()
                .HasForeignKey(l => l.NguoiThucHienId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
