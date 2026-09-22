using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class LichSuTrangThaiYeuCauGiuChoConfiguration : IEntityTypeConfiguration<LichSuTrangThaiYeuCauGiuCho>
    {
        public void Configure(EntityTypeBuilder<LichSuTrangThaiYeuCauGiuCho> builder)
        {
            builder.Property(l => l.LyDo)
                .HasMaxLength(500);

            builder.HasOne(l => l.YeuCauGiuCho)
                .WithMany(y => y.LichSuTrangThais)
                .HasForeignKey(l => l.YeuCauGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(l => l.NguoiThucHien)
                .WithMany()
                .HasForeignKey(l => l.NguoiThucHienId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
