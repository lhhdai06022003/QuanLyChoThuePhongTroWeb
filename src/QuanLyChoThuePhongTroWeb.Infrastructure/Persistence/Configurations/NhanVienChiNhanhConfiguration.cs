using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class NhanVienChiNhanhConfiguration : IEntityTypeConfiguration<NhanVienChiNhanh>
    {
        public void Configure(EntityTypeBuilder<NhanVienChiNhanh> builder)
        {
            builder.Property(n => n.IsActive)
                .HasDefaultValue(true);

            builder.Property(n => n.LyDo)
                .HasMaxLength(500);

            builder.HasOne(n => n.NguoiDung)
                .WithMany(u => u.NhanVienChiNhanhs)
                .HasForeignKey(n => n.NguoiDungId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(n => n.ChiNhanh)
                .WithMany()
                .HasForeignKey(n => n.ChiNhanhId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(n => n.NguoiPhanCong)
                .WithMany()
                .HasForeignKey(n => n.NguoiPhanCongId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(n => n.NguoiThuHoi)
                .WithMany()
                .HasForeignKey(n => n.NguoiThuHoiId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(n => new { n.NguoiDungId, n.ChiNhanhId })
                .IsUnique();
        }
    }
}
