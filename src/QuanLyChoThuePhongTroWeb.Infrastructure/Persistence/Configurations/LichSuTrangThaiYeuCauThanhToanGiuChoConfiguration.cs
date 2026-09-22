using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class LichSuTrangThaiYeuCauThanhToanGiuChoConfiguration : IEntityTypeConfiguration<LichSuTrangThaiYeuCauThanhToanGiuCho>
    {
        public void Configure(EntityTypeBuilder<LichSuTrangThaiYeuCauThanhToanGiuCho> builder)
        {
            builder.Property(l => l.LyDo)
                .HasMaxLength(500);

            builder.HasOne(l => l.YeuCauThanhToanGiuCho)
                .WithMany(y => y.LichSuTrangThais)
                .HasForeignKey(l => l.YeuCauThanhToanGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(l => l.NguoiThucHien)
                .WithMany()
                .HasForeignKey(l => l.NguoiThucHienId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
