using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
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
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
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
        public DbSet<AnhChiSoDongHo> AnhChiSoDongHos { get; set; }
        public DbSet<LichSuTrangThaiHoaDon> LichSuTrangThaiHoaDons { get; set; }
        public DbSet<YeuCauThanhToanHoaDon> YeuCauThanhToanHoaDons { get; set; }
        public DbSet<MinhChungThanhToanHoaDon> MinhChungThanhToanHoaDons { get; set; }
        public DbSet<LichSuTrangThaiYeuCauThanhToanHoaDon> LichSuTrangThaiYeuCauThanhToanHoaDons { get; set; }

        public DbSet<AnhPhongTro> AnhPhongTros { get; set; }
        public DbSet<NhanVienChiNhanh> NhanVienChiNhanhs { get; set; }
        public DbSet<KhachVangLai> KhachVangLais { get; set; }
        public DbSet<KhungGioXemPhong> KhungGioXemPhongs { get; set; }
        public DbSet<YeuCauXemPhong> YeuCauXemPhongs { get; set; }
        public DbSet<LichSuTrangThaiYeuCauXemPhong> LichSuTrangThaiYeuCauXemPhongs { get; set; }
        public DbSet<YeuCauGiuCho> YeuCauGiuChos { get; set; }
        public DbSet<LichSuTrangThaiYeuCauGiuCho> LichSuTrangThaiYeuCauGiuChos { get; set; }
        public DbSet<YeuCauThanhToanGiuCho> YeuCauThanhToanGiuChos { get; set; }
        public DbSet<MinhChungThanhToanGiuCho> MinhChungThanhToanGiuChos { get; set; }
        public DbSet<GiaoDichGiuCho> GiaoDichGiuChos { get; set; }
        public DbSet<LichSuTrangThaiYeuCauThanhToanGiuCho> LichSuTrangThaiYeuCauThanhToanGiuChos { get; set; }
        public DbSet<ApDungTienGiuChoVaoTienCoc> ApDungTienGiuChoVaoTienCocs { get; set; }
        public DbSet<QuyetDinhHoanTienGiuCho> QuyetDinhHoanTienGiuChos { get; set; }
        public DbSet<GiaoDichHoanTienGiuCho> GiaoDichHoanTienGiuChos { get; set; }
    }
}
