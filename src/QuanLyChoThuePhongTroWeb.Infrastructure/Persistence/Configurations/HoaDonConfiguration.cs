using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class HoaDonConfiguration : IEntityTypeConfiguration<HoaDon>
    {
        public void Configure(EntityTypeBuilder<HoaDon> builder)
        {
            builder.Property(h => h.TongTien)
                .HasPrecision(18, 2);

            builder.Property(h => h.SoTienThanhToanToiThieu)
                .HasPrecision(18, 2);

            builder.HasIndex(h => new { h.HopDongId, h.Thang, h.Nam })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            builder.HasIndex(h => new { h.TrangThaiPhatHanh, h.HanPhanHoi })
                .HasDatabaseName("IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi")
                .HasFilter("\"HanPhanHoi\" IS NOT NULL AND \"IsDeleted\" = false");

            builder.HasOne(h => h.DichVuDienNuocCuaPhong)
                .WithMany(d => d.HoaDons)
                .HasForeignKey(h => h.DichVuDienNuocCuaPhongId)
                .IsRequired(false);
        }
    }
}
