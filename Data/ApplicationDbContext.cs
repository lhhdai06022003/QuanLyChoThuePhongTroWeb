using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình quan hệ 1-1 giữa NguoiDung và NguoiThue
            modelBuilder.Entity<NguoiDung>()
                .HasOne(u => u.NguoiThue)          // Một Người dùng có một Người thuê
                .WithOne(t => t.NguoiDung)         // Một Người thuê có một Người dùng
                .HasForeignKey<NguoiDung>(u => u.NguoiThueId);

            // Đảm bảo một Chi nhánh không thể kích hoạt một Dịch vụ tổng quá 1 lần
            modelBuilder.Entity<DichVuChiNhanh>()
                .HasIndex(dcn => new { dcn.ChiNhanhId, dcn.DichVuId })
                .IsUnique();

            // Đảm bảo Mã chi nhánh là duy nhất
            modelBuilder.Entity<ChiNhanh>()
                .HasIndex(c => c.MaChiNhanh)
                .IsUnique();
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



    }
}
