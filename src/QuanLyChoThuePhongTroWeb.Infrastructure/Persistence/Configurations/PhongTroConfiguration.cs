using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class PhongTroConfiguration : IEntityTypeConfiguration<PhongTro>
    {
        public void Configure(EntityTypeBuilder<PhongTro> builder)
        {
            builder.Property(p => p.GiaThue)
                .HasPrecision(18, 2);

            builder.Property(p => p.DuocDangTin)
                .HasDefaultValue(false);

            builder.Property(p => p.TieuDeDangTin)
                .HasMaxLength(255);

            builder.Property(p => p.MaCongKhai)
                .HasMaxLength(50);

            builder.HasIndex(p => p.MaCongKhai)
                .IsUnique()
                .HasFilter("\"MaCongKhai\" IS NOT NULL");

            builder.HasOne(p => p.NguoiDangTin)
                .WithMany()
                .HasForeignKey(p => p.NguoiDangTinId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(p => p.AnhPhongTros)
                .WithOne(a => a.PhongTro)
                .HasForeignKey(a => a.PhongTroId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
