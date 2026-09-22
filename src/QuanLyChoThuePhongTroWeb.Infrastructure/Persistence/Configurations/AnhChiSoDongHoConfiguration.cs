using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class AnhChiSoDongHoConfiguration : IEntityTypeConfiguration<AnhChiSoDongHo>
    {
        public void Configure(EntityTypeBuilder<AnhChiSoDongHo> builder)
        {
            builder.HasOne(a => a.DichVuDienNuocCuaPhong)
                .WithMany(d => d.AnhChiSoDongHos)
                .HasForeignKey(a => a.DichVuDienNuocCuaPhongId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.NguoiGui)
                .WithMany()
                .HasForeignKey(a => a.NguoiGuiId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(a => a.NguoiXacNhan)
                .WithMany()
                .HasForeignKey(a => a.NguoiXacNhanId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(a => a.Url)
                .IsRequired()
                .HasMaxLength(2048);

            builder.Property(a => a.PublicId)
                .HasMaxLength(255);

            builder.Property(a => a.GiaTriAIGoiY)
                .HasPrecision(18, 3);

            builder.Property(a => a.GiaTriXacNhan)
                .HasPrecision(18, 3);

            builder.Property(a => a.ThongBaoLoi)
                .HasMaxLength(1000);

            builder.Property(a => a.GhiChuXacNhan)
                .HasMaxLength(1000);

            builder.HasIndex(a => new
                {
                    a.DichVuDienNuocCuaPhongId,
                    a.LoaiDongHo
                })
                .IsUnique()
                .HasFilter("\"DuocChonLamChiSoChinhThuc\" = true AND \"IsDeleted\" = false");

            builder.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_AnhChiSoDongHo_DoTinCay",
                    "\"DoTinCay\" IS NULL OR (\"DoTinCay\" >= 0 AND \"DoTinCay\" <= 1)");
                table.HasCheckConstraint(
                    "CK_AnhChiSoDongHo_GiaTri",
                    "(\"GiaTriAIGoiY\" IS NULL OR \"GiaTriAIGoiY\" >= 0) AND (\"GiaTriXacNhan\" IS NULL OR \"GiaTriXacNhan\" >= 0)");
                table.HasCheckConstraint(
                    "CK_AnhChiSoDongHo_ChiSoChinhThuc",
                    "NOT \"DuocChonLamChiSoChinhThuc\" OR (\"GiaTriXacNhan\" IS NOT NULL AND \"NguoiXacNhanId\" IS NOT NULL AND \"NgayXacNhan\" IS NOT NULL)");
            });
        }
    }
}
