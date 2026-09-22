using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class DichVuDienNuocCuaPhongConfiguration : IEntityTypeConfiguration<DichVuDienNuocCuaPhong>
    {
        public void Configure(EntityTypeBuilder<DichVuDienNuocCuaPhong> builder)
        {
            builder.Property(d => d.DonGiaDien)
                .HasPrecision(18, 2);

            builder.Property(d => d.DonGiaNuoc)
                .HasPrecision(18, 2);

            builder.Property(d => d.ChiSoDienCu)
                .HasPrecision(18, 3);

            builder.Property(d => d.ChiSoDienMoi)
                .HasPrecision(18, 3);

            builder.Property(d => d.ChiSoNuocCu)
                .HasPrecision(18, 3);

            builder.Property(d => d.ChiSoNuocMoi)
                .HasPrecision(18, 3);

            builder.HasIndex(d => new { d.PhongTroId, d.Thang, d.Nam })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
        }
    }
}
