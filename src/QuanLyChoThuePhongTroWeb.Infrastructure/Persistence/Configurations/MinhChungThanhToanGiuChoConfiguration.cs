using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class MinhChungThanhToanGiuChoConfiguration : IEntityTypeConfiguration<MinhChungThanhToanGiuCho>
    {
        public void Configure(EntityTypeBuilder<MinhChungThanhToanGiuCho> builder)
        {
            builder.Property(m => m.UrlHinhAnh)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(m => m.PublicIdHinhAnh)
                .HasMaxLength(200);

            builder.Property(m => m.MaGiaoDichNganHang)
                .HasMaxLength(100);

            builder.Property(m => m.SoTienKhaiBao)
                .HasPrecision(18, 2);

            builder.Property(m => m.GhiChuKhachHang)
                .HasMaxLength(500);

            builder.Property(m => m.LyDoTuChoi)
                .HasMaxLength(500);

            builder.HasOne(m => m.YeuCauThanhToanGiuCho)
                .WithMany(y => y.MinhChungThanhToanGiuChos)
                .HasForeignKey(m => m.YeuCauThanhToanGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.NguoiDoiChieu)
                .WithMany()
                .HasForeignKey(m => m.NguoiDoiChieuId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_MinhChungThanhToanGiuCho_SoTienKhaiBao", "\"SoTienKhaiBao\" > 0");
            });
        }
    }
}
