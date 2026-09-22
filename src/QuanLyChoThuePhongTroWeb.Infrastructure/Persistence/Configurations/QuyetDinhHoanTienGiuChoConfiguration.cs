using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class QuyetDinhHoanTienGiuChoConfiguration : IEntityTypeConfiguration<QuyetDinhHoanTienGiuCho>
    {
        public void Configure(EntityTypeBuilder<QuyetDinhHoanTienGiuCho> builder)
        {
            builder.Property(q => q.SoTienHoanDuyet)
                .HasPrecision(18, 2);

            builder.Property(q => q.LyDo)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(q => q.GhiChu)
                .HasMaxLength(500);

            builder.HasOne(q => q.YeuCauGiuCho)
                .WithOne(y => y.QuyetDinhHoanTienGiuCho)
                .HasForeignKey<QuyetDinhHoanTienGiuCho>(q => q.YeuCauGiuChoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(q => q.NguoiQuyetDinh)
                .WithMany()
                .HasForeignKey(q => q.NguoiQuyetDinhId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(q => q.YeuCauGiuChoId)
                .IsUnique();

            builder.ToTable(table =>
            {
                table.HasCheckConstraint("CK_QuyetDinhHoanTienGiuCho_SoTienHoanDuyet", "\"SoTienHoanDuyet\" >= 0");
            });
        }
    }
}
