using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class KhungGioXemPhongConfiguration : IEntityTypeConfiguration<KhungGioXemPhong>
    {
        public void Configure(EntityTypeBuilder<KhungGioXemPhong> builder)
        {
            builder.Property(k => k.IsActive)
                .HasDefaultValue(true);

            builder.HasOne(k => k.PhongTro)
                .WithMany()
                .HasForeignKey(k => k.PhongTroId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(k => k.NguoiPhuTrach)
                .WithMany()
                .HasForeignKey(k => k.NguoiPhuTrachId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(k => k.NguoiTao)
                .WithMany()
                .HasForeignKey(k => k.NguoiTaoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_KhungGioXemPhong_ThoiGian", "\"ThoiGianBatDau\" < \"ThoiGianKetThuc\"");
                table.HasCheckConstraint("CK_KhungGioXemPhong_SoLuongToiDa", "\"SoLuongToiDa\" > 0");
            });
        }
    }
}
