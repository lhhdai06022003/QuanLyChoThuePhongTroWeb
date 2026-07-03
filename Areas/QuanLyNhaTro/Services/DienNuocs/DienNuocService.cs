using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using Microsoft.Extensions.Logging;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.DienNuocs
{
    public class DienNuocService : IDienNuocService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DienNuocService> _logger;

        public DienNuocService(ApplicationDbContext context, ILogger<DienNuocService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<DienNuocPhongRes>> GetDanhSachDienNuocAsync(int chiNhanhId, int thang, int nam)
        {
            // 1. Lấy danh sách các phòng có hợp đồng hiệu lực tại chi nhánh trong tháng
            var startOfMonth = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

            var hopDongsActive = await _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endOfMonth &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= startOfMonth))
                .ToListAsync();

            if (!hopDongsActive.Any()) return new List<DienNuocPhongRes>();

            // Nhóm theo PhongTroId để đảm bảo mỗi phòng trọ chỉ hiển thị 1 dòng duy nhất trên giao diện chốt số
            var phongĐangThue = hopDongsActive
                .GroupBy(h => h.PhongTroId)
                .Select(g => g.OrderByDescending(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                              .ThenByDescending(h => h.ThoiDiemBatDau)
                              .First())
                .ToList();

            var phongTroIds = phongĐangThue.Select(h => h.PhongTroId).ToList();

            // 2. Lấy TOÀN BỘ bản ghi chỉ số tháng hiện tại của các phòng này (Query 2)
            var currentRecords = await _context.DichVuDienNuocCuaPhongs
                .Where(x => phongTroIds.Contains(x.PhongTroId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                .ToDictionaryAsync(x => x.PhongTroId);

            // Tính tháng/năm trước đó để lấy chỉ số cũ nếu cần
            int prevThang = thang == 1 ? 12 : thang - 1;
            int prevNam = thang == 1 ? nam - 1 : nam;

            // 3. Lấy TOÀN BỘ bản ghi chỉ số tháng trước của các phòng này (Query 3)
            var prevRecords = await _context.DichVuDienNuocCuaPhongs
                .Where(x => phongTroIds.Contains(x.PhongTroId) && x.Thang == prevThang && x.Nam == prevNam && !x.IsDeleted)
                .ToDictionaryAsync(x => x.PhongTroId);

            // 4. Tìm các phòng đã có bản ghi chốt ở các tháng tiếp theo (dùng để khóa)
            var lockedPhongIds = new HashSet<int>(
                await _context.DichVuDienNuocCuaPhongs
                    .Where(x => phongTroIds.Contains(x.PhongTroId) && !x.IsDeleted &&
                                (x.Nam > nam || (x.Nam == nam && x.Thang > thang)))
                    .Select(x => x.PhongTroId)
                    .Distinct()
                    .ToListAsync()
            );

            var result = new List<DienNuocPhongRes>();

            foreach (var hd in phongĐangThue)
            {
                var phongTroId = hd.PhongTroId;
                bool isLocked = lockedPhongIds.Contains(phongTroId);

                if (currentRecords.TryGetValue(phongTroId, out var currentRecord))
                {
                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = currentRecord.DichVuDienNuocCuaPhongId,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = currentRecord.ChiSoDienCu,
                        ChiSoDienMoi = currentRecord.ChiSoDienMoi,
                        ChiSoNuocCu = currentRecord.ChiSoNuocCu,
                        ChiSoNuocMoi = currentRecord.ChiSoNuocMoi,
                        IsDaChot = true,
                        IsLocked = isLocked
                    });
                }
                else
                {
                    double dienCu = 0;
                    double nuocCu = 0;
                    if (prevRecords.TryGetValue(phongTroId, out var prevRecord))
                    {
                        dienCu = prevRecord.ChiSoDienMoi;
                        nuocCu = prevRecord.ChiSoNuocMoi;
                    }
                    else
                    {
                        var ganNhat = await GetChiSoDienNuocGanNhatAsync(phongTroId, thang, nam);
                        dienCu = ganNhat.ChiSoDienMoi;
                        nuocCu = ganNhat.ChiSoNuocMoi;
                    }

                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = 0,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = dienCu,
                        ChiSoDienMoi = 0,
                        ChiSoNuocCu = nuocCu,
                        ChiSoNuocMoi = 0,
                        IsDaChot = false,
                        IsLocked = isLocked
                    });
                }
            }

            return result.OrderBy(x => x.TenPhong).ToList();
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> SaveChotDienNuocAsync(ChotDienNuocReq input)
        {
            if (input == null || input.DanhSachPhong == null || !input.DanhSachPhong.Any())
            {
                return (false, "Không có dữ liệu để lưu.");
            }

            // 1. Kiểm tra tính hợp lệ của tham số tháng, năm, chi nhánh
            if (input.Thang < 1 || input.Thang > 12 || input.Nam < 2000)
            {
                return (false, "Thời gian chốt chỉ số điện nước không hợp lệ.");
            }

            if (input.ChiNhanhId <= 0)
            {
                return (false, "Chi nhánh không hợp lệ.");
            }

            // 2. Lấy đơn giá Điện / Nước cấu hình ở Chi nhánh
            var dichVuDien = await _context.DichVus.FirstOrDefaultAsync(x => x.TenDichVu.ToLower().Contains("điện") && !x.IsDeleted);
            var dichVuNuoc = await _context.DichVus.FirstOrDefaultAsync(x => x.TenDichVu.ToLower().Contains("nước") && !x.IsDeleted);

            double donGiaDien = 0;
            double donGiaNuoc = 0;

            if (dichVuDien != null)
            {
                var bgDien = await _context.Set<DichVuChiNhanh>().FirstOrDefaultAsync(x => x.DichVuId == dichVuDien.DichVuId && x.ChiNhanhId == input.ChiNhanhId && !x.IsDeleted);
                if (bgDien != null) donGiaDien = bgDien.GiaDichVu;
            }

            if (dichVuNuoc != null)
            {
                var bgNuoc = await _context.Set<DichVuChiNhanh>().FirstOrDefaultAsync(x => x.DichVuId == dichVuNuoc.DichVuId && x.ChiNhanhId == input.ChiNhanhId && !x.IsDeleted);
                if (bgNuoc != null) donGiaNuoc = bgNuoc.GiaDichVu;
            }

            var phongTroIds = input.DanhSachPhong.Select(x => x.PhongTroId).ToList();

            // 3. Tải toàn bộ bản ghi cần đối chiếu để tránh N+1 Query
            var currentRecords = await _context.DichVuDienNuocCuaPhongs
                .Where(x => phongTroIds.Contains(x.PhongTroId) && x.Thang == input.Thang && x.Nam == input.Nam && !x.IsDeleted)
                .ToDictionaryAsync(x => x.PhongTroId);

            int prevThang = input.Thang == 1 ? 12 : input.Thang - 1;
            int prevNam = input.Thang == 1 ? input.Nam - 1 : input.Nam;

            var prevRecords = await _context.DichVuDienNuocCuaPhongs
                .Where(x => phongTroIds.Contains(x.PhongTroId) && x.Thang == prevThang && x.Nam == prevNam && !x.IsDeleted)
                .ToDictionaryAsync(x => x.PhongTroId);

            var lockedPhongIds = new HashSet<int>(
                await _context.DichVuDienNuocCuaPhongs
                    .Where(x => phongTroIds.Contains(x.PhongTroId) && !x.IsDeleted &&
                                (x.Nam > input.Nam || (x.Nam == input.Nam && x.Thang > input.Thang)))
                    .Select(x => x.PhongTroId)
                    .Distinct()
                    .ToListAsync()
            );

            var phongNames = await _context.PhongTros
                .Where(x => phongTroIds.Contains(x.PhongTroId))
                .ToDictionaryAsync(x => x.PhongTroId, x => x.SoPhong);

            // Bắt đầu Transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var req in input.DanhSachPhong)
                {
                    string soPhong = phongNames.TryGetValue(req.PhongTroId, out var name) ? name : $"ID {req.PhongTroId}";

                    // a. Kiểm tra ràng buộc khóa thời gian (không được sửa tháng cũ nếu tháng sau đã chốt)
                    if (lockedPhongIds.Contains(req.PhongTroId))
                    {
                        return (false, $"Phòng {soPhong}: Không thể lưu chỉ số tháng {input.Thang}/{input.Nam} vì tháng tiếp theo đã được chốt.");
                    }

                    // b. Kiểm tra chỉ số âm
                    if (req.ChiSoDienCu < 0 || req.ChiSoDienMoi < 0 || req.ChiSoNuocCu < 0 || req.ChiSoNuocMoi < 0)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số điện/nước không được phép âm.");
                    }

                    // c. Kiểm tra chỉ số mới nhỏ hơn cũ
                    if (req.ChiSoDienMoi < req.ChiSoDienCu)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                    }
                    if (req.ChiSoNuocMoi < req.ChiSoNuocCu)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
                    }

                    // d. Kiểm tra tính chính xác của chỉ số đầu kỳ gửi lên từ Client
                    double actualPrevDienMoi = 0;
                    double actualPrevNuocMoi = 0;
                    if (prevRecords.TryGetValue(req.PhongTroId, out var prevRecord))
                    {
                        actualPrevDienMoi = prevRecord.ChiSoDienMoi;
                        actualPrevNuocMoi = prevRecord.ChiSoNuocMoi;
                    }
                    else
                    {
                        var ganNhat = await GetChiSoDienNuocGanNhatAsync(req.PhongTroId, input.Thang, input.Nam);
                        actualPrevDienMoi = ganNhat.ChiSoDienMoi;
                        actualPrevNuocMoi = ganNhat.ChiSoNuocMoi;
                    }

                    if (req.ChiSoDienCu != actualPrevDienMoi)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số điện đầu kỳ ({req.ChiSoDienCu}) không khớp với chỉ số cuối kỳ trước ({actualPrevDienMoi}).");
                    }
                    if (req.ChiSoNuocCu != actualPrevNuocMoi)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số nước đầu kỳ ({req.ChiSoNuocCu}) không khớp với chỉ số cuối kỳ trước ({actualPrevNuocMoi}).");
                    }

                    // e. Lưu/Cập nhật dữ liệu
                    if (currentRecords.TryGetValue(req.PhongTroId, out var record))
                    {
                        // Update
                        record.ChiSoDienCu = req.ChiSoDienCu;
                        record.ChiSoDienMoi = req.ChiSoDienMoi;
                        record.ChiSoNuocCu = req.ChiSoNuocCu;
                        record.ChiSoNuocMoi = req.ChiSoNuocMoi;
                        record.DonGiaDien = donGiaDien;
                        record.DonGiaNuoc = donGiaNuoc;
                        record.NgayCapNhat = DateTime.UtcNow;
                        _context.DichVuDienNuocCuaPhongs.Update(record);
                    }
                    else
                    {
                        // Create
                        var newRecord = new DichVuDienNuocCuaPhong
                        {
                            PhongTroId = req.PhongTroId,
                            Thang = input.Thang,
                            Nam = input.Nam,
                            ChiSoDienCu = req.ChiSoDienCu,
                            ChiSoDienMoi = req.ChiSoDienMoi,
                            DonGiaDien = donGiaDien,
                            ChiSoNuocCu = req.ChiSoNuocCu,
                            ChiSoNuocMoi = req.ChiSoNuocMoi,
                            DonGiaNuoc = donGiaNuoc,
                            NgayTao = DateTime.UtcNow
                        };
                        _context.DichVuDienNuocCuaPhongs.Add(newRecord);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi hệ thống khi lưu chốt điện nước.");
                return (false, "Lỗi hệ thống khi lưu chốt điện nước.");
            }
        }

        private async Task<(double ChiSoDienMoi, double ChiSoNuocMoi)> GetChiSoDienNuocGanNhatAsync(int phongTroId, int thang, int nam)
        {
            var record = await _context.DichVuDienNuocCuaPhongs
                .Where(x => x.PhongTroId == phongTroId && !x.IsDeleted && 
                            (x.Nam < nam || (x.Nam == nam && x.Thang < thang)))
                .OrderByDescending(x => x.Nam)
                .ThenByDescending(x => x.Thang)
                .FirstOrDefaultAsync();

            return record != null ? (record.ChiSoDienMoi, record.ChiSoNuocMoi) : (0.0, 0.0);
        }
    }
}
