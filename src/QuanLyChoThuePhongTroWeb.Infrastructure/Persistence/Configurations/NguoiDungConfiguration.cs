using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Configurations
{
    public class NguoiDungConfiguration : IEntityTypeConfiguration<NguoiDung>
    {
        public void Configure(EntityTypeBuilder<NguoiDung> builder)
        {
            builder.HasOne(u => u.NguoiThue)
                .WithOne(t => t.NguoiDung)
                .HasForeignKey<NguoiDung>(u => u.NguoiThueId);

            builder.HasOne(u => u.KhachVangLai)
                .WithOne(k => k.NguoiDung)
                .HasForeignKey<KhachVangLai>(k => k.NguoiDungId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
