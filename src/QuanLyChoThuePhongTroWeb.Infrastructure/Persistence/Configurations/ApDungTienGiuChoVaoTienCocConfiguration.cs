using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class ApDungTienGiuChoVaoTienCocConfiguration : IEntityTypeConfiguration<ApDungTienGiuChoVaoTienCoc>
    {
        public void Configure(EntityTypeBuilder<ApDungTienGiuChoVaoTienCoc> builder)
        {
            builder.Property(a => a.SoTienApDung)
                .HasPrecision(18, 2);

            builder.Property(a => a.GhiChu)
                .HasMaxLength(500);

            builder.HasOne(a => a.YeuCauGiuCho)
                .WithOne(y => y.ApDungTienGiuChoVaoTienCoc)
                .HasForeignKey<ApDungTienGiuChoVaoTienCoc>(a => a.YeuCauGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.HopDong)
                .WithMany()
                .HasForeignKey(a => a.HopDongId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.NguoiThucHien)
                .WithMany()
                .HasForeignKey(a => a.NguoiThucHienId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => a.YeuCauGiuChoId)
                .IsUnique();

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ApDungTienGiuChoVaoTienCoc_SoTienApDung", "\"SoTienApDung\" > 0");
            });
        }
    }
}
