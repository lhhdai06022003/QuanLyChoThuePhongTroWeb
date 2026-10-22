using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class YeuCauGiuChoConfiguration : IEntityTypeConfiguration<YeuCauGiuCho>
    {
        public void Configure(EntityTypeBuilder<YeuCauGiuCho> builder)
        {
            builder.Property(y => y.SoTienGiuCho)
                .HasPrecision(18, 2);

            builder.Property(y => y.LyDoTuChoiHoacHuy)
                .HasMaxLength(500);

            builder.HasOne(y => y.KhachVangLai)
                .WithMany()
                .HasForeignKey(y => y.KhachVangLaiId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(y => y.YeuCauXemPhong)
                .WithMany()
                .HasForeignKey(y => y.YeuCauXemPhongId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(y => y.PhongTro)
                .WithMany()
                .HasForeignKey(y => y.PhongTroId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(y => y.NguoiDuyet)
                .WithMany()
                .HasForeignKey(y => y.NguoiDuyetId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(y => y.PhongTroId)
                .IsUnique()
                .HasFilter("\"TrangThai\" IN (1, 2, 3)");

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_YeuCauGiuCho_SoTienGiuCho", "\"SoTienGiuCho\" IS NULL OR \"SoTienGiuCho\" > 0");
                table.HasCheckConstraint("CK_YeuCauGiuCho_SoTienTheoTrangThai", "(\"TrangThai\" NOT IN (1, 2, 3, 4)) OR (\"SoTienGiuCho\" IS NOT NULL AND \"SoTienGiuCho\" > 0)");
            });
        }
    }
}
