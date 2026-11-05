using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    // Hóa đơn của cả chuỗi gia hạn (cùng mã gốc) với hợp đồng đang thanh lý, vì cọc đã chuyển sang hợp đồng mới khi gia hạn.
    public sealed record ThanhLyHoaDonInput(
        int HoaDonId, string MaHoaDon, int HopDongId, string MaHopDong, int Thang, int Nam,
        decimal TongTien, decimal DaThu, TrangThaiPhatHanhHoaDon TrangThaiPhatHanh,
        bool CoLuotDangDoiChieu, bool TongTienLaSoNguyen);

    public sealed record ThanhLyPhanBo(
        int HoaDonId, string MaHoaDon, int HopDongId, string MaHopDong, int Thang, int Nam,
        decimal TongTien, decimal DaThu, decimal ConLai, decimal CanTru);

    public sealed record ThanhLyKetQuaTinh(
        IReadOnlyList<ThanhLyPhanBo> HoaDonNo, decimal TongNo, decimal CanTru, decimal CocHoanTrucTiep, decimal ConNoSauCanTru);

    public static class ThanhLyCalculator
    {
        // Chỉ hóa đơn DaGui có ConLai > 0 là nợ. Sắp Nam, Thang, HoaDonId tăng dần; kỳ cũ được cấn trừ trước.
        public static ThanhLyKetQuaTinh Tinh(decimal tienCoc, IEnumerable<ThanhLyHoaDonInput> hoaDons)
        {
            var no = hoaDons
                .Where(h => h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui && h.TongTien - h.DaThu > 0)
                .OrderBy(h => h.Nam).ThenBy(h => h.Thang).ThenBy(h => h.HoaDonId)
                .ToList();

            var coc = Math.Max(tienCoc, 0m);
            var tongNo = no.Sum(h => h.TongTien - h.DaThu);
            var canTru = Math.Min(coc, tongNo);
            var conLaiCanTru = canTru;
            var phanBos = new List<ThanhLyPhanBo>();
            foreach (var h in no)
            {
                var conLai = h.TongTien - h.DaThu;
                var nhan = Math.Min(conLaiCanTru, conLai);
                conLaiCanTru -= nhan;
                phanBos.Add(new ThanhLyPhanBo(h.HoaDonId, h.MaHoaDon, h.HopDongId, h.MaHopDong, h.Thang, h.Nam, h.TongTien, h.DaThu, conLai, nhan));
            }

            return new ThanhLyKetQuaTinh(phanBos, tongNo, canTru, coc - canTru, tongNo - canTru);
        }

        public static bool ThieuHoaDonThangTraPhong(int hopDongId, DateTime thoiDiemKetThucUtc, IReadOnlyList<ThanhLyHoaDonInput> hoaDons)
        {
            var ketThucVn = thoiDiemKetThucUtc.AddHours(7);
            var thang = ketThucVn.Month;
            var nam = ketThucVn.Year;
            return !hoaDons.Any(h => h.HopDongId == hopDongId && h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui && h.Thang == thang && h.Nam == nam);
        }

        // Trả danh sách lý do chặn (rỗng = cho thanh lý).
        // Quy tắc "tháng trả phòng phải có hóa đơn DaGui" chỉ xét hợp đồng đang thanh lý; các quy tắc còn lại áp cho cả chuỗi.
        public static IReadOnlyList<string> TimLyDoChan(
            TrangThaiHopDong trangThaiHopDong,
            DateTime? thoiDiemKetThucUtc,
            decimal tienCoc,
            int hopDongId,
            IReadOnlyList<ThanhLyHoaDonInput> hoaDons)
        {
            var lyDo = new List<string>();
            if (trangThaiHopDong != TrangThaiHopDong.DangHoatDong)
            {
                lyDo.Add("Hợp đồng không còn hoạt động.");
                return lyDo;
            }

            if (thoiDiemKetThucUtc == null)
            {
                lyDo.Add("Hợp đồng chưa có ngày trả phòng. Cập nhật Ngày kết thúc trước khi thanh lý.");
                return lyDo;
            }

            var ketThucVn = thoiDiemKetThucUtc.Value.AddHours(7);
            var thang = ketThucVn.Month;
            var nam = ketThucVn.Year;
            if (ThieuHoaDonThangTraPhong(hopDongId, thoiDiemKetThucUtc.Value, hoaDons))
            {
                lyDo.Add($"Tháng trả phòng T{thang}/{nam} chưa có hóa đơn đã công bố. Lập hóa đơn, gửi duyệt, chốt và công bố trước.");
            }

            var sapXep = hoaDons.OrderBy(x => x.Nam).ThenBy(x => x.Thang).ThenBy(x => x.HoaDonId).ToList();

            foreach (var h in sapXep)
            {
                if (h.TrangThaiPhatHanh is TrangThaiPhatHanhHoaDon.Nhap or TrangThaiPhatHanhHoaDon.ChoDuyet or TrangThaiPhatHanhHoaDon.DaChot)
                {
                    var ten = h.TrangThaiPhatHanh switch
                    {
                        TrangThaiPhatHanhHoaDon.Nhap => "Nháp",
                        TrangThaiPhatHanhHoaDon.ChoDuyet => "Chờ duyệt",
                        _ => "Đã chốt"
                    };
                    lyDo.Add($"Hóa đơn {h.MaHoaDon} (T{h.Thang}/{h.Nam}) đang ở trạng thái {ten}, cần công bố hoặc hủy trước.");
                }
            }

            foreach (var h in sapXep.Where(x => x.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui && x.TongTien - x.DaThu > 0))
            {
                if (h.CoLuotDangDoiChieu)
                {
                    lyDo.Add($"Hóa đơn {h.MaHoaDon}: khách đã nộp minh chứng, hãy đối chiếu trước.");
                }

                if (!h.TongTienLaSoNguyen)
                {
                    lyDo.Add($"Hóa đơn {h.MaHoaDon} có số tiền lẻ, cần hủy và lập lại.");
                }
            }

            if (tienCoc != decimal.Truncate(tienCoc))
            {
                lyDo.Add("Tiền cọc có số lẻ, không cấn trừ được.");
            }

            return lyDo;
        }
    }
}
