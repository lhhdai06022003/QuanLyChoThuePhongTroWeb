using Microsoft.EntityFrameworkCore;
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
                .HasForeignKey<NguoiThue>(t => t.NguoiDungId) // Khóa ngoại nằm ở bảng NguoiThue
                .OnDelete(DeleteBehavior.Cascade); // Nếu xóa NguoiDung thì xóa luôn NguoiThue (tùy bạn chọn)

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



    }
}
