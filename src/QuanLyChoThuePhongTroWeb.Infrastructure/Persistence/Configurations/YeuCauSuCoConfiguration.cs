using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class YeuCauSuCoConfiguration : IEntityTypeConfiguration<YeuCauSuCo>
    {
        public void Configure(EntityTypeBuilder<YeuCauSuCo> builder)
        {
            builder.Property(y => y.ChiPhiSuaChua)
                .HasPrecision(18, 2);
        }
    }
}
