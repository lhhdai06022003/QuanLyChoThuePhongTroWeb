using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class GiaoDichHoanTienGiuChoConfiguration : IEntityTypeConfiguration<GiaoDichHoanTienGiuCho>
    {
        public void Configure(EntityTypeBuilder<GiaoDichHoanTienGiuCho> builder)
        {
            builder.Property(g => g.SoTienHoan)
                .HasPrecision(18, 2);

            builder.Property(g => g.MaGiaoDichHoan)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(g => g.UrlHinhAnhMinhChung)
                .HasMaxLength(500);

            builder.Property(g => g.GhiChu)
                .HasMaxLength(500);

            builder.HasOne(g => g.QuyetDinhHoanTienGiuCho)
                .WithMany(q => q.GiaoDichHoanTiens)
                .HasForeignKey(g => g.QuyetDinhHoanTienGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(g => g.NguoiXacNhan)
                .WithMany()
                .HasForeignKey(g => g.NguoiXacNhanId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(g => g.MaGiaoDichHoan)
                .IsUnique();

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_GiaoDichHoanTienGiuCho_SoTienHoan", "\"SoTienHoan\" > 0");
            });
        }
    }
}
