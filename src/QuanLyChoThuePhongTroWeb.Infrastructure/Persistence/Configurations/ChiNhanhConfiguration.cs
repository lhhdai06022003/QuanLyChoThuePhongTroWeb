using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class ChiNhanhConfiguration : IEntityTypeConfiguration<ChiNhanh>
    {
        public void Configure(EntityTypeBuilder<ChiNhanh> builder)
        {
            builder.HasIndex(c => c.MaChiNhanh)
                .IsUnique();
        }
    }
}
