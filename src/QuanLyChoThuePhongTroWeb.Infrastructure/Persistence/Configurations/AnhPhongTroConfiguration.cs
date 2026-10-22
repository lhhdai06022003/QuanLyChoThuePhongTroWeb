using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class AnhPhongTroConfiguration : IEntityTypeConfiguration<AnhPhongTro>
    {
        public void Configure(EntityTypeBuilder<AnhPhongTro> builder)
        {
            builder.Property(a => a.Url)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(a => a.PublicId)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(a => a.ChuThich)
                .HasMaxLength(255);

            builder.Property(a => a.LaAnhDaiDien)
                .HasDefaultValue(false);

            builder.Property(a => a.IsActive)
                .HasDefaultValue(true);

            builder.HasOne(a => a.PhongTro)
                .WithMany(p => p.AnhPhongTros)
                .HasForeignKey(a => a.PhongTroId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.NguoiTaiLen)
                .WithMany()
                .HasForeignKey(a => a.NguoiTaiLenId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => a.PhongTroId)
                .IsUnique()
                .HasFilter("\"LaAnhDaiDien\" = true AND \"IsActive\" = true");
        }
    }
}
