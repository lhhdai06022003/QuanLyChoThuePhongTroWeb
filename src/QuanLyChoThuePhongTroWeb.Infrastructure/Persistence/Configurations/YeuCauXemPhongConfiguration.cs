using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class YeuCauXemPhongConfiguration : IEntityTypeConfiguration<YeuCauXemPhong>
    {
        public void Configure(EntityTypeBuilder<YeuCauXemPhong> builder)
        {
            builder.Property(y => y.HoTen)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(y => y.SoDienThoai)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(y => y.Email)
                .HasMaxLength(150);

            builder.Property(y => y.GhiChu)
                .HasMaxLength(1000);

            builder.HasOne(y => y.PhongTro)
                .WithMany()
                .HasForeignKey(y => y.PhongTroId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(y => y.KhachVangLai)
                .WithMany()
                .HasForeignKey(y => y.KhachVangLaiId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(y => y.KhungGioXemPhong)
                .WithMany(k => k.YeuCauXemPhongs)
                .HasForeignKey(y => y.KhungGioXemPhongId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
