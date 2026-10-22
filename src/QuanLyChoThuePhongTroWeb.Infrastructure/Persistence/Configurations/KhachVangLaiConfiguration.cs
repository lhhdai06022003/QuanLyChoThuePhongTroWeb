using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class KhachVangLaiConfiguration : IEntityTypeConfiguration<KhachVangLai>
    {
        public void Configure(EntityTypeBuilder<KhachVangLai> builder)
        {
            builder.Property(k => k.HoTen)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(k => k.Email)
                .HasMaxLength(150);

            builder.Property(k => k.EmailNormalized)
                .HasMaxLength(150);

            builder.Property(k => k.SoDienThoai)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(k => k.DaXacMinhEmail)
                .HasDefaultValue(false);

            builder.HasOne(k => k.NguoiDung)
                .WithOne(u => u.KhachVangLai)
                .HasForeignKey<KhachVangLai>(k => k.NguoiDungId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(k => k.NguoiDungId)
                .IsUnique();
        }
    }
}
