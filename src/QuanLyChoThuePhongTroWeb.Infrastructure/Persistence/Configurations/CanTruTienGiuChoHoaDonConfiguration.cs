using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations;

public sealed class CanTruTienGiuChoHoaDonConfiguration : IEntityTypeConfiguration<CanTruTienGiuChoHoaDon>
{
    public void Configure(EntityTypeBuilder<CanTruTienGiuChoHoaDon> builder)
    {
        builder.HasKey(item => item.CanTruTienGiuChoHoaDonId);
        builder.Property(item => item.SoTienCanTru).HasPrecision(18, 2);
        builder.HasOne(item => item.ApDungTienGiuChoVaoTienCoc).WithMany()
            .HasForeignKey(item => item.ApDungTienGiuChoVaoTienCocId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.HoaDon).WithOne(item => item.CanTruGiuCho)
            .HasForeignKey<CanTruTienGiuChoHoaDon>(item => item.HoaDonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.HoaDonId).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("CK_CanTruGiuCho_SoTien", "\"SoTienCanTru\" > 0"));
    }
}
