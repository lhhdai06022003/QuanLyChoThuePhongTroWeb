using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tx = await Database.BeginTransactionAsync(cancellationToken);
            return new ApplicationTransaction(tx);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình quan hệ 1-1 giữa NguoiDung và NguoiThue
            modelBuilder.Entity<NguoiDung>()
                .HasOne(u => u.NguoiThue)
                .WithOne(t => t.NguoiDung)
                .HasForeignKey<NguoiDung>(u => u.NguoiThueId);

            // Đảm bảo một Chi nhánh không thể kích hoạt một Dịch vụ tổng quá 1 lần
            modelBuilder.Entity<DichVuChiNhanh>()
                .HasIndex(dcn => new { dcn.ChiNhanhId, dcn.DichVuId })
                .IsUnique();

            // Đảm bảo Mã chi nhánh là duy nhất
            modelBuilder.Entity<ChiNhanh>()
                .HasIndex(c => c.MaChiNhanh)
                .IsUnique();
                
            // Đảm bảo 1 hợp đồng chỉ có tối đa 1 hóa đơn active trong 1 tháng/năm (bỏ qua soft-deleted)
            modelBuilder.Entity<HoaDon>()
                .HasIndex(h => new { h.HopDongId, h.Thang, h.Nam })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
                
            // Cấu hình quan hệ 1-N giữa Điện Nước và Hóa Đơn
            modelBuilder.Entity<HoaDon>()
                .HasOne(h => h.DichVuDienNuocCuaPhong)
                .WithMany(d => d.HoaDons)
                .HasForeignKey(h => h.DichVuDienNuocCuaPhongId)
                .IsRequired(false);
        }

        public DbSet<ChiNhanh> ChiNhanhs { get; set; }
        public DbSet<PhongTro> PhongTros { get; set; }
        public DbSet<DichVu> DichVus { get; set; }
        public DbSet<ChiTietHoaDon> ChiTietHoaDons { get; set; }
        public DbSet<ChiTietThanhVienHopDong> ChiTietThanhVienHopDongs { get; set; }
        public DbSet<DangKyDichVu> DangKyDichVus { get; set; }
        public DbSet<DichVuDienNuocCuaPhong> DichVuDienNuocCuaPhongs { get; set; }
        public DbSet<HoaDon> HoaDons { get; set; }
        public DbSet<HopDong> HopDongs { get; set; }
        public DbSet<NguoiThue> NguoiThues { get; set; }
        public DbSet<LichSuThanhToan> LichSuThanhToans { get; set; }
        public DbSet<NguoiDung> NguoiDungs { get; set; }
        public DbSet<DieuKhoanMau> DieuKhoanMaus { get; set; }
        public DbSet<HopDongDieuKhoan> HopDongDieuKhoans { get; set; }
        public DbSet<YeuCauSuCo> YeuCauSuCos { get; set; }
        public DbSet<ThongBao> ThongBaos { get; set; }
        public DbSet<DichVuChiNhanh> DichVuChiNhanhs { get; set; }
    }
}
