using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class GiaoDichGiuChoConfiguration : IEntityTypeConfiguration<GiaoDichGiuCho>
    {
        public void Configure(EntityTypeBuilder<GiaoDichGiuCho> builder)
        {
            builder.Property(g => g.MaGiaoDich)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(g => g.SoTienThucNhan)
                .HasPrecision(18, 2);

            builder.Property(g => g.GhiChu)
                .HasMaxLength(500);

            builder.HasOne(g => g.YeuCauThanhToanGiuCho)
                .WithMany(y => y.GiaoDichGiuChos)
                .HasForeignKey(g => g.YeuCauThanhToanGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(g => g.MinhChungThanhToanGiuCho)
                .WithMany()
                .HasForeignKey(g => g.MinhChungThanhToanGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(g => g.NguoiXacNhan)
                .WithMany()
                .HasForeignKey(g => g.NguoiXacNhanId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(g => g.MaGiaoDich)
                .IsUnique();

            builder.HasIndex(g => g.MinhChungThanhToanGiuChoId)
                .IsUnique()
                .HasFilter("\"MinhChungThanhToanGiuChoId\" IS NOT NULL");

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_GiaoDichGiuCho_SoTienThucNhan", "\"SoTienThucNhan\" > 0");
            });
        }
    }
}
