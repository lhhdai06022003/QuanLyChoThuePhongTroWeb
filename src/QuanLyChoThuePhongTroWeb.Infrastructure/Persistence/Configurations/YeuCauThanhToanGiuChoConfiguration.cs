using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class YeuCauThanhToanGiuChoConfiguration : IEntityTypeConfiguration<YeuCauThanhToanGiuCho>
    {
        public void Configure(EntityTypeBuilder<YeuCauThanhToanGiuCho> builder)
        {
            builder.Property(y => y.MaYeuCau)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(y => y.SoTien)
                .HasPrecision(18, 2);

            builder.Property(y => y.NoiDungChuyenKhoan)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasOne(y => y.YeuCauGiuCho)
                .WithMany(y => y.YeuCauThanhToanGiuChos)
                .HasForeignKey(y => y.YeuCauGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(y => y.NguoiTao)
                .WithMany()
                .HasForeignKey(y => y.NguoiTaoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(y => y.MaYeuCau)
                .IsUnique();

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_YeuCauThanhToanGiuCho_SoTien", "\"SoTien\" > 0");
            });
        }
    }
}
