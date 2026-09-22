using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class DichVuChiNhanhConfiguration : IEntityTypeConfiguration<DichVuChiNhanh>
    {
        public void Configure(EntityTypeBuilder<DichVuChiNhanh> builder)
        {
            builder.Property(d => d.GiaDichVu)
                .HasPrecision(18, 2);

            builder.HasIndex(dcn => new { dcn.ChiNhanhId, dcn.DichVuId })
                .IsUnique();
        }
    }
}
