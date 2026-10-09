using System;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Dựng dữ liệu chi nhánh / phòng / hợp đồng / hóa đơn cho các test truy vấn việc cần làm và Dashboard.
    // Mọi tên có hậu tố Guid; test phải chạy trong transaction rồi RollbackAsync.
    internal sealed class DebtTestSeeder
    {
        private readonly ApplicationDbContext _db;
        private int _counter;

        public string Suffix { get; } = Guid.NewGuid().ToString("N")[..8];

        public DebtTestSeeder(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ChiNhanh> BranchAsync(string tag)
        {
            var b = new ChiNhanh
            {
                TenChiNhanh = $"Vc{tag} {Suffix}",
                MaChiNhanh = $"V{tag}{Suffix[..5]}",
                DiaChi = tag,
                SoDienThoai = "0900000001",
                MoTa = tag
            };
            _db.ChiNhanhs.Add(b);
            await _db.SaveChangesAsync();
            return b;
        }

        public async Task<NguoiThue> TenantAsync(string tag)
        {
            var t = new NguoiThue
            {
                HoVaTen = $"Vc Khach {tag} {Suffix}",
                SoDienThoai = "0911111111",
                CCCD = $"V{tag}{Suffix}",
                Email = $"v{tag.ToLowerInvariant()}{Suffix}@t.vn"
            };
            _db.NguoiThues.Add(t);
            await _db.SaveChangesAsync();
            return t;
        }

        public async Task<PhongTro> RoomAsync(ChiNhanh b, string tag)
        {
            var r = new PhongTro
            {
                ChiNhanhId = b.ChiNhanhId,
                SoPhong = $"{tag}{Suffix[..6]}{++_counter}",
                GiaThue = 1_000_000m,
                DienTich = 10,
                MoTa = "x",
                TrangThai = TrangThaiPhong.DaThue
            };
            _db.PhongTros.Add(r);
            await _db.SaveChangesAsync();
            return r;
        }

        public async Task<HopDong> ContractAsync(
            PhongTro room, NguoiThue tenant, DateTime? start = null, DateTime? end = null,
            TrangThaiHopDong status = TrangThaiHopDong.DangHoatDong, bool deleted = false)
        {
            var c = new HopDong
            {
                MaHopDong = $"VC{Suffix}{++_counter}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = start ?? new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ThoiDiemKetThuc = end,
                TienCocPhong = 1m,
                TienThuePhong = 1m,
                TrangThaiHopDong = status,
                IsDeleted = deleted
            };
            _db.HopDongs.Add(c);
            await _db.SaveChangesAsync();
            return c;
        }

        // Mỗi hóa đơn mặc định một (tháng, năm) riêng trên cùng hợp đồng để không vướng chỉ mục duy nhất.
        public async Task<HoaDon> InvoiceAsync(
            HopDong contract, TrangThaiPhatHanhHoaDon issue, TrangThaiHoaDon pay, decimal total, DateTime? due,
            bool deleted = false, decimal paid = 0m, decimal paidDeleted = 0m, int? month = null, int? year = null)
        {
            var n = ++_counter;
            var inv = new HoaDon
            {
                MaHoaDon = $"VH{Suffix}{n}",
                HopDongId = contract.HopDongId,
                Thang = month ?? (n % 12) + 1,
                Nam = year ?? 2010 + (n / 12),
                TongTien = total,
                TrangThaiPhatHanh = issue,
                TrangThaiHoaDon = pay,
                HanThanhToan = due,
                IsDeleted = deleted
            };
            _db.HoaDons.Add(inv);
            await _db.SaveChangesAsync();

            if (paid > 0)
            {
                _db.LichSuThanhToans.Add(Ledger(inv, paid, false, n, 1));
            }

            if (paidDeleted > 0)
            {
                _db.LichSuThanhToans.Add(Ledger(inv, paidDeleted, true, n, 2));
            }

            await _db.SaveChangesAsync();
            return inv;
        }

        private LichSuThanhToan Ledger(HoaDon inv, decimal amount, bool deleted, int n, int k) => new()
        {
            HoaDonId = inv.HoaDonId,
            MaGiaoDich = $"VL{Suffix}{n}-{k}",
            SoTienThanhToan = amount,
            PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
            NgayThanhToan = new DateTime(2003, 4, 15, 3, 0, 0, DateTimeKind.Utc),
            IsDeleted = deleted
        };

        public int Next() => ++_counter;
    }
}
