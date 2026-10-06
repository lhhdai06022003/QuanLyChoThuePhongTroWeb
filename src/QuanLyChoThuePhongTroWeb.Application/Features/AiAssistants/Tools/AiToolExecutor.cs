using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools
{
    // Chạy công cụ AI theo vai trò người hỏi: kiểm quyền, kiểm tham số, truy vấn trong phạm vi chi nhánh, dựng JSON trả về model.
    public class AiToolExecutor
    {
        private const string ThangError = "Tháng phải là số nguyên từ 1 đến 12.";
        private const string NamError = "Năm phải là số nguyên từ 2000 đến 2100.";
        private const string SoNgayError = "Số ngày phải là số nguyên từ 1 đến 365.";
        private const string DateFormatError = "Ngày phải có dạng YYYY-MM-DD.";
        private const string DateOrderError = "Từ ngày phải trước hoặc bằng đến ngày.";
        private const string GiaError = "Mức giá tối đa phải là số lớn hơn 0.";
        private const string TuKhoaError = "Cần nhập tên khách thuê hoặc số phòng để tra cứu.";
        private const string SoPhongError = "Cần cho biết số phòng.";

        // Số dòng tối đa gửi cho model ở các công cụ có thể trả rất nhiều dòng; tổng số và tổng tiền vẫn tính trên toàn bộ.
        private const int MaxListRows = 50;
        private const int MaxLoggedToolNameLength = 64;

        private const string ErrThamSo = "THAM_SO_KHONG_HOP_LE";
        private const string ErrKhongKhaDung = "CONG_CU_KHONG_KHA_DUNG";
        private const string ErrTruyVan = "LOI_TRUY_VAN";

        private readonly IAiAssistantStore _store;
        private readonly ILogger<AiToolExecutor> _logger;
        private readonly TimeProvider _timeProvider;

        public AiToolExecutor(IAiAssistantStore store, ILogger<AiToolExecutor> logger, TimeProvider? timeProvider = null)
        {
            _store = store;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<AiToolResult> ExecuteAsync(AiCallerContext caller, AiFunctionCall call, CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            AiToolResult result;
            try
            {
                result = await ExecuteCoreAsync(caller, call, ct);
            }
            catch (AiToolArgException ex)
            {
                result = AiToolResult.Error(ErrThamSo, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Công cụ AI {ToolName} gặp lỗi khi truy vấn dữ liệu", SafeToolName(call.Name));
                result = AiToolResult.Error(ErrTruyVan, "Không tra cứu được dữ liệu lúc này.");
            }
            stopwatch.Stop();

            _logger.LogInformation("AI công cụ {ToolName}: người dùng {NguoiDungId} ({Role}), {Outcome}, {ResultCount} kết quả, {ElapsedMs} ms",
                SafeToolName(call.Name), caller.NguoiDungId, caller.Role, result.Outcome, result.ResultCount, stopwatch.ElapsedMilliseconds);
            return result;
        }

        private async Task<AiToolResult> ExecuteCoreAsync(AiCallerContext caller, AiFunctionCall call, CancellationToken ct)
        {
            if (!AiToolCatalog.IsAllowed(caller.Role, call.Name))
            {
                return Unavailable();
            }

            var isTenant = caller.Role == AiCallerRole.KhachThue;
            if (isTenant && !(caller.NguoiThueId > 0))
            {
                return Unavailable();
            }

            // Admin không giới hạn; nhân viên fail closed khi thiếu danh sách chi nhánh.
            IReadOnlyCollection<int>? allowed = caller.Role == AiCallerRole.Admin
                ? null
                : caller.AllowedBranchIds ?? Array.Empty<int>();

            var args = AiToolArgs.Parse(call.ArgumentsJson);
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var nowVn = nowUtc.AddHours(7);

            switch (call.Name)
            {
                case AiToolNames.PhongTroChuaChotDienNuoc:
                    {
                        var (thang, nam) = ReadMonthYearWithDefaults(args, nowUtc);
                        var rows = await _store.GetRentedRoomsWithoutMeterReadingAsync(thang, nam, allowed, ct);
                        return AiToolResult.Ok(new { thang, nam, phongs = rows.Select(RoomPayload).ToList() }, rows.Count);
                    }
                case AiToolNames.HoaDonChuaThanhToan:
                    {
                        var (thang, nam) = ReadPeriodFilter(args, nowUtc);
                        var rows = await _store.GetUnpaidInvoicesAsync(thang, nam, null, allowed, ct);
                        return AiToolResult.Ok(new
                        {
                            kyLoc = DescribePeriod(thang, nam),
                            soHoaDon = rows.Count,
                            tongConNo = rows.Sum(ConLai),
                            donVi = "VND",
                            soHoaDonHienThi = Math.Min(rows.Count, MaxListRows),
                            ghiChu = TruncationNote(rows.Count, "hóa đơn", "màn Công nợ"),
                            hoaDons = rows.Take(MaxListRows).Select(r => new
                            {
                                maHoaDon = r.MaHoaDon,
                                soPhong = r.SoPhong,
                                chiNhanh = r.TenChiNhanh,
                                khachThue = r.KhachThue,
                                thang = r.Thang,
                                nam = r.Nam,
                                tongTien = r.TongTien,
                                daThu = r.DaThu,
                                conLai = ConLai(r)
                            }).ToList()
                        }, rows.Count);
                    }
                case AiToolNames.DoanhThuThucThu:
                    {
                        var tuNgay = args.GetDate("tuNgay", DateFormatError) ?? new DateOnly(nowVn.Year, nowVn.Month, 1);
                        var denNgay = args.GetDate("denNgay", DateFormatError) ?? DateOnly.FromDateTime(nowVn);
                        if (tuNgay > denNgay) throw new AiToolArgException(DateOrderError);

                        var fromUtc = DateTime.SpecifyKind(tuNgay.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
                        var toExclusiveUtc = DateTime.SpecifyKind(denNgay.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
                        var total = await _store.GetRevenueAsync(fromUtc, toExclusiveUtc, allowed, ct);
                        return AiToolResult.Ok(new
                        {
                            tuNgay = FormatDate(tuNgay),
                            denNgay = FormatDate(denNgay),
                            soGiaoDich = total.SoGiaoDich,
                            tongDoanhThu = total.TongTien,
                            donVi = "VND"
                        }, total.SoGiaoDich);
                    }
                case AiToolNames.PhongTrong:
                    {
                        var gia = args.GetDecimal("mucGiaToiDa", GiaError);
                        if (gia.HasValue && gia.Value <= 0) throw new AiToolArgException(GiaError);
                        var rows = await _store.GetVacantRoomsAsync(gia, allowed, ct);
                        return AiToolResult.Ok(new { mucGiaToiDa = gia, phongs = rows.Select(RoomPayload).ToList() }, rows.Count);
                    }
                case AiToolNames.HopDongSapHetHan:
                    {
                        var soNgay = args.GetInt("soNgay", SoNgayError) ?? 30;
                        if (soNgay < 1 || soNgay > 365) throw new AiToolArgException(SoNgayError);
                        var rows = await _store.GetExpiringContractsAsync(nowUtc, nowUtc.AddDays(soNgay), allowed, ct);
                        return AiToolResult.Ok(new
                        {
                            soNgay,
                            hopDongs = rows.Select(r => new
                            {
                                soPhong = r.SoPhong,
                                chiNhanh = r.TenChiNhanh,
                                khachThue = r.KhachThue,
                                ngayKetThuc = FormatDate(r.ThoiDiemKetThucUtc),
                                soNgayConLai = (r.ThoiDiemKetThucUtc.AddHours(7).Date - nowVn.Date).Days
                            }).ToList()
                        }, rows.Count);
                    }
                case AiToolNames.ThongTinKhachThue:
                    {
                        var tuKhoa = args.GetString("tuKhoa")?.Trim();
                        if (string.IsNullOrEmpty(tuKhoa)) throw new AiToolArgException(TuKhoaError);
                        var rows = await _store.SearchTenantsAsync(tuKhoa, allowed, ct);
                        return AiToolResult.Ok(new
                        {
                            soKetQua = rows.Count,
                            soKetQuaHienThi = Math.Min(rows.Count, MaxListRows),
                            ghiChu = TruncationNote(rows.Count, "kết quả", "màn Người thuê hoặc dùng từ khóa cụ thể hơn"),
                            ketQua = rows.Take(MaxListRows).Select(r => new
                            {
                                soPhong = r.SoPhong,
                                chiNhanh = r.TenChiNhanh,
                                hoVaTen = r.HoVaTen,
                                soDienThoai = r.SoDienThoai,
                                trangThaiHopDong = ContractStatusLabel(r.TrangThaiHopDong)
                            }).ToList()
                        }, rows.Count);
                    }
                case AiToolNames.CongNoPhong:
                    {
                        var (soPhong, tenChiNhanh) = ReadRoomLookup(args);
                        var rooms = await _store.FindRoomsByNumberAsync(soPhong, tenChiNhanh, allowed, ct);
                        var early = ResolveRoomOutcome(rooms, out var room);
                        if (early != null) return early;

                        var rows = await _store.GetUnpaidInvoicesAsync(null, null, room!.PhongTroId, allowed, ct);
                        return AiToolResult.Ok(new
                        {
                            timThayPhong = true,
                            soPhong = room.SoPhong,
                            chiNhanh = room.TenChiNhanh,
                            soHoaDon = rows.Count,
                            tongConNo = rows.Sum(ConLai),
                            donVi = "VND",
                            hoaDons = rows.Select(r => new
                            {
                                maHoaDon = r.MaHoaDon,
                                thang = r.Thang,
                                nam = r.Nam,
                                tongTien = r.TongTien,
                                daThu = r.DaThu,
                                conLai = ConLai(r)
                            }).ToList()
                        }, rows.Count);
                    }
                case AiToolNames.DoanhThuChiNhanh:
                    {
                        var (thang, nam) = ReadMonthYearWithDefaults(args, nowUtc);
                        var rows = await _store.GetRevenueByBranchAsync(thang, nam, allowed, ct);
                        return AiToolResult.Ok(new
                        {
                            thang,
                            nam,
                            tongDoanhThu = rows.Sum(r => r.TongTien),
                            donVi = "VND",
                            chiNhanhs = rows.Select(r => new { chiNhanh = r.TenChiNhanh, tongDoanhThu = r.TongTien, soGiaoDich = r.SoGiaoDich }).ToList()
                        }, rows.Count);
                    }
                case AiToolNames.ChiSoDienNuoc:
                    {
                        var (soPhong, tenChiNhanh) = ReadRoomLookup(args);
                        var (thang, nam) = ReadMonthYearWithDefaults(args, nowUtc);
                        var rooms = await _store.FindRoomsByNumberAsync(soPhong, tenChiNhanh, allowed, ct);
                        var early = ResolveRoomOutcome(rooms, out var room);
                        if (early != null) return early;

                        var readings = await _store.GetMeterReadingsAsync(new[] { room!.PhongTroId }, thang, nam, ct);
                        var reading = readings.FirstOrDefault();
                        return AiToolResult.Ok(new
                        {
                            timThayPhong = true,
                            soPhong = room.SoPhong,
                            chiNhanh = room.TenChiNhanh,
                            thang,
                            nam,
                            chiSo = reading == null ? null : MeterPayload(reading)
                        }, reading == null ? 0 : 1);
                    }
                case AiToolNames.MyHopDongInfo:
                    {
                        var rows = await _store.GetActiveContractsOfTenantAsync(caller.NguoiThueId!.Value, ct);
                        return AiToolResult.Ok(new
                        {
                            donVi = "VND",
                            hopDongs = rows.Select(r => new
                            {
                                maHopDong = r.MaHopDong,
                                soPhong = r.SoPhong,
                                chiNhanh = r.TenChiNhanh,
                                ngayBatDau = FormatDate(r.ThoiDiemBatDauUtc),
                                ngayKetThuc = r.ThoiDiemKetThucUtc.HasValue ? FormatDate(r.ThoiDiemKetThucUtc.Value) : "Không thời hạn",
                                giaThue = r.TienThuePhong,
                                tienCoc = r.TienCocPhong
                            }).ToList()
                        }, rows.Count);
                    }
                case AiToolNames.MyChiSoDienNuoc:
                    {
                        var (thang, nam) = ReadMonthYearWithDefaults(args, nowUtc);
                        var contracts = await _store.GetActiveContractsOfTenantAsync(caller.NguoiThueId!.Value, ct);
                        if (contracts.Count == 0)
                        {
                            return AiToolResult.Ok(new { thang, nam, chiSos = new List<object>(), thongBao = "Bạn không có hợp đồng đang hoạt động." }, 0);
                        }

                        // Chỉ các phòng mà khách đã vào ở từ kỳ được hỏi trở về trước, để không lộ chỉ số của người thuê trước.
                        var roomIds = contracts
                            .Where(c => StartPeriodVn(c.ThoiDiemBatDauUtc) <= nam * 12 + thang)
                            .Select(c => c.PhongTroId)
                            .Distinct()
                            .ToList();
                        if (roomIds.Count == 0)
                        {
                            return AiToolResult.Ok(new { thang, nam, chiSos = new List<object>(), thongBao = "Kỳ này trước thời điểm bạn bắt đầu thuê phòng." }, 0);
                        }

                        var readings = await _store.GetMeterReadingsAsync(roomIds, thang, nam, ct);
                        return AiToolResult.Ok(new { thang, nam, chiSos = readings.Select(MeterPayload).ToList() }, readings.Count);
                    }
                case AiToolNames.MyHoaDonChuaThanhToan:
                    {
                        var (thang, nam) = ReadPeriodFilter(args, nowUtc);
                        var rows = await _store.GetTenantUnpaidInvoicesAsync(caller.NguoiThueId!.Value, thang, nam, ct);
                        return AiToolResult.Ok(new
                        {
                            kyLoc = DescribePeriod(thang, nam),
                            soHoaDon = rows.Count,
                            tongConNo = rows.Sum(ConLai),
                            donVi = "VND",
                            hoaDons = rows.Select(r => new
                            {
                                maHoaDon = r.MaHoaDon,
                                soPhong = r.SoPhong,
                                thang = r.Thang,
                                nam = r.Nam,
                                tongTien = r.TongTien,
                                daThu = r.DaThu,
                                conLai = ConLai(r)
                            }).ToList()
                        }, rows.Count);
                    }
                default:
                    return Unavailable();
            }
        }

        private static AiToolResult Unavailable()
            => AiToolResult.Error(ErrKhongKhaDung, "Công cụ không khả dụng.");

        private static decimal ConLai(AiUnpaidInvoiceRow r) => r.TongTien - r.DaThu;

        // Kỳ (năm * 12 + tháng) theo giờ Việt Nam của thời điểm bắt đầu hợp đồng.
        private static int StartPeriodVn(DateTime startUtc)
        {
            var vn = startUtc.AddHours(7);
            return vn.Year * 12 + vn.Month;
        }

        private static string? TruncationNote(int total, string donVi, string noiXemDu)
            => total > MaxListRows
                ? $"Chỉ hiện {MaxListRows}/{total} {donVi}; còn {total - MaxListRows} {donVi} chưa hiện, xem đủ ở {noiXemDu}."
                : null;

        // Tên công cụ do model đặt: chỉ ghi log khi là định danh ngắn, tránh chèn nội dung tùy ý vào log.
        private static string SafeToolName(string? name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxLoggedToolNameLength) return "(không hợp lệ)";
            foreach (var ch in name)
            {
                if (!(ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
                {
                    return "(không hợp lệ)";
                }
            }
            return name;
        }

        private static string FormatDate(DateTime utc)
            => utc.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static string FormatDate(DateOnly date)
            => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static object RoomPayload(AiRoomRow r)
            => new { soPhong = r.SoPhong, tangLau = r.TangLau, chiNhanh = r.TenChiNhanh, giaThue = r.GiaThue };

        private static object MeterPayload(AiMeterReadingRow r)
            => new
            {
                soPhong = r.SoPhong,
                chiNhanh = r.TenChiNhanh,
                dienCu = r.ChiSoDienCu,
                dienMoi = r.ChiSoDienMoi,
                dienTieuThu = r.ChiSoDienMoi - r.ChiSoDienCu,
                nuocCu = r.ChiSoNuocCu,
                nuocMoi = r.ChiSoNuocMoi,
                nuocTieuThu = r.ChiSoNuocMoi - r.ChiSoNuocCu
            };

        private static string ContractStatusLabel(TrangThaiHopDong status) => status switch
        {
            TrangThaiHopDong.DangHoatDong => "Đang hoạt động",
            TrangThaiHopDong.DaKetThuc => "Đã kết thúc",
            TrangThaiHopDong.DaHuy => "Đã hủy",
            _ => status.ToString()
        };

        private static string DescribePeriod(int? thang, int? nam)
        {
            if (thang.HasValue && nam.HasValue) return $"tháng {thang.Value}/{nam.Value}";
            if (nam.HasValue) return $"năm {nam.Value}";
            return "tất cả các kỳ";
        }

        private static int? ReadMonth(AiToolArgs args)
        {
            var thang = args.GetInt("thang", ThangError, zeroMeansMissing: true);
            if (thang.HasValue && (thang.Value < 1 || thang.Value > 12)) throw new AiToolArgException(ThangError);
            return thang;
        }

        private static int? ReadYear(AiToolArgs args)
        {
            var nam = args.GetInt("nam", NamError, zeroMeansMissing: true);
            if (nam.HasValue && (nam.Value < 2000 || nam.Value > 2100)) throw new AiToolArgException(NamError);
            return nam;
        }

        // Thiếu tháng/năm thì dùng tháng/năm hiện tại theo giờ Việt Nam.
        private static (int Thang, int Nam) ReadMonthYearWithDefaults(AiToolArgs args, DateTime nowUtc)
        {
            var thang = ReadMonth(args);
            var nam = ReadYear(args);
            var current = MeterPeriodPolicy.CurrentVn(nowUtc);
            return (thang ?? current.Thang, nam ?? current.Nam);
        }

        // Lọc kỳ tùy chọn: thiếu cả hai = mọi kỳ; có tháng thiếu năm = năm hiện tại; có năm thiếu tháng = cả năm.
        private static (int? Thang, int? Nam) ReadPeriodFilter(AiToolArgs args, DateTime nowUtc)
        {
            var thang = ReadMonth(args);
            var nam = ReadYear(args);
            if (thang.HasValue && !nam.HasValue)
            {
                nam = MeterPeriodPolicy.CurrentVn(nowUtc).Nam;
            }
            return (thang, nam);
        }

        private static (string SoPhong, string? TenChiNhanh) ReadRoomLookup(AiToolArgs args)
        {
            var soPhong = args.GetString("soPhong")?.Trim();
            if (string.IsNullOrEmpty(soPhong)) throw new AiToolArgException(SoPhongError);
            var tenChiNhanh = args.GetString("tenChiNhanh")?.Trim();
            return (soPhong, string.IsNullOrEmpty(tenChiNhanh) ? null : tenChiNhanh);
        }

        // Trả kết quả sớm khi không có phòng hoặc có nhiều phòng cùng số; còn đúng một phòng thì trả null và gán room.
        private static AiToolResult? ResolveRoomOutcome(IReadOnlyList<AiRoomRow> rooms, out AiRoomRow? room)
        {
            room = null;
            if (rooms.Count == 0)
            {
                return AiToolResult.Ok(new { timThayPhong = false, thongBao = "Không tìm thấy phòng." }, 0);
            }
            if (rooms.Count > 1)
            {
                return AiToolResult.Ok(new
                {
                    timThayPhong = true,
                    canChonChiNhanh = true,
                    cacChiNhanh = rooms.Select(r => r.TenChiNhanh).ToList(),
                    huongDan = "Có nhiều phòng cùng số, hãy hỏi người dùng muốn xem chi nhánh nào."
                }, rooms.Count);
            }
            room = rooms[0];
            return null;
        }
    }
}
