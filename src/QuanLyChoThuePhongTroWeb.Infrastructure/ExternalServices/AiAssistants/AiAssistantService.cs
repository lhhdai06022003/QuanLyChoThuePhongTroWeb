using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants
{
    public class AiAssistantService : IAiAssistantService
    {
        private readonly ApplicationDbContext _db;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public AiAssistantService(ApplicationDbContext db, HttpClient httpClient, IConfiguration configuration)
        {
            _db = db;
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
            _model = configuration["Gemini:Model"] ?? "gemini-1.5-flash";
        }

        public async Task<List<PhongTroChuaChotRes>> GetPhongTroChuaChotDienNuocAsync(int thang = 0, int nam = 0)
        {
            if (thang <= 0) thang = DateTime.Now.Month;
            if (nam <= 0) nam = DateTime.Now.Year;

            return await _db.PhongTros
                .AsNoTracking()
                .Include(p => p.ChiNhanh)
                .Where(p => !p.IsDeleted && p.TrangThai == TrangThaiPhong.DaThue)
                .Where(p => !_db.DichVuDienNuocCuaPhongs.Any(dv => 
                    dv.PhongTroId == p.PhongTroId && 
                    dv.Thang == thang && 
                    dv.Nam == nam && 
                    !dv.IsDeleted))
                .Select(p => new PhongTroChuaChotRes
                {
                    PhongTroId = p.PhongTroId,
                    SoPhong = p.SoPhong,
                    TangLau = p.TangLau,
                    ChiNhanhTen = p.ChiNhanh.TenChiNhanh,
                    GiaThue = p.GiaThue
                })
                .ToListAsync();
        }

        public async Task<List<HoaDonChuaThanhToanRes>> GetHoaDonChuaThanhToanAsync(int thang = 0, int nam = 0)
        {
            var query = _db.HoaDons
                .AsNoTracking()
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted && 
                            h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan && 
                            h.HopDong.PhongTro.TrangThai == TrangThaiPhong.DaThue);

            if (thang > 0)
            {
                query = query.Where(h => h.Thang == thang);
            }
            if (nam > 0)
            {
                query = query.Where(h => h.Nam == nam);
            }

            return await query
                .Select(h => new HoaDonChuaThanhToanRes
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    SoPhong = h.HopDong.PhongTro.SoPhong + " (" + h.HopDong.PhongTro.ChiNhanh.TenChiNhanh + ")",
                    KhachThueTen = h.HopDong.NguoiThue.HoVaTen,
                    TongTien = h.TongTien,
                    Thang = h.Thang,
                    Nam = h.Nam
                })
                .ToListAsync();
        }

        public async Task<double> GetDoanhThuThucThuAsync(DateTime tuNgay, DateTime denNgay)
        {
            var tuNgayUtc = tuNgay.ToUniversalTime();
            var denNgayUtc = denNgay.ToUniversalTime();

            return await _db.LichSuThanhToans
                .AsNoTracking()
                .Where(lst => !lst.IsDeleted && 
                              lst.NgayThanhToan >= tuNgayUtc && 
                              lst.NgayThanhToan <= denNgayUtc)
                .SumAsync(lst => lst.SoTienThanhToan);
        }

        public async Task<List<PhongTrongRes>> GetPhongTrongAsync(double? mucGiaToiDa)
        {
            var query = _db.PhongTros
                .AsNoTracking()
                .Include(p => p.ChiNhanh)
                .Where(p => !p.IsDeleted && p.TrangThai == TrangThaiPhong.Trong);
            
            if (mucGiaToiDa.HasValue && mucGiaToiDa.Value > 0)
            {
                query = query.Where(p => p.GiaThue <= mucGiaToiDa.Value);
            }

            return await query.Select(p => new PhongTrongRes
            {
                SoPhong = p.SoPhong,
                TangLau = p.TangLau,
                ChiNhanhTen = p.ChiNhanh.TenChiNhanh,
                GiaThue = p.GiaThue
            }).ToListAsync();
        }

        public async Task<List<HopDongSapHetHanRes>> GetHopDongSapHetHanAsync(int soNgay)
        {
            var targetDate = DateTime.UtcNow.AddDays(soNgay);
            var now = DateTime.UtcNow;

            return await _db.HopDongs
                .AsNoTracking()
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && h.ThoiDiemKetThuc.HasValue && h.ThoiDiemKetThuc.Value >= now && h.ThoiDiemKetThuc.Value <= targetDate)
                .Select(h => new HopDongSapHetHanRes
                {
                    SoPhong = h.PhongTro.SoPhong,
                    KhachThueTen = h.NguoiThue.HoVaTen,
                    NgayKetThuc = h.ThoiDiemKetThuc.Value.ToString("dd/MM/yyyy"),
                    SoNgayConLai = (int)(h.ThoiDiemKetThuc.Value - now).TotalDays
                })
                .ToListAsync();
        }

        public async Task<List<ThongTinKhachThueRes>> GetThongTinKhachThueAsync(string tuKhoa)
        {
            tuKhoa = tuKhoa.ToLower();
            return await _db.HopDongs
                .AsNoTracking()
                .Include(h => h.NguoiThue)
                .Include(h => h.PhongTro)
                .Where(h => !h.IsDeleted && !h.NguoiThue.IsDeleted && (h.NguoiThue.HoVaTen.ToLower().Contains(tuKhoa) || h.PhongTro.SoPhong.ToLower().Contains(tuKhoa)))
                .Select(h => new ThongTinKhachThueRes
                {
                    SoPhong = h.PhongTro.SoPhong,
                    HoVaTen = h.NguoiThue.HoVaTen,
                    SoDienThoai = h.NguoiThue.SoDienThoai,
                    TrangThai = h.TrangThaiHopDong.ToString()
                })
                .ToListAsync();
        }

        public async Task<double> GetCongNoPhongAsync(string soPhong)
        {
            soPhong = soPhong.ToLower();
            var hoaDons = await _db.HoaDons
                .AsNoTracking()
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro)
                .Include(h => h.LichSuThanhToans)
                .Where(h => !h.IsDeleted && h.HopDong.PhongTro.SoPhong.ToLower() == soPhong && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan)
                .ToListAsync();

            double totalDebt = 0;
            foreach (var hd in hoaDons)
            {
                var paid = hd.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan);
                totalDebt += (hd.TongTien - paid);
            }

            return totalDebt;
        }

        public async Task<List<DoanhThuChiNhanhRes>> GetDoanhThuChiNhanhAsync(int thang = 0, int nam = 0)
        {
            var query = _db.LichSuThanhToans
                .AsNoTracking()
                .Include(l => l.HoaDon).ThenInclude(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Where(l => !l.IsDeleted && !l.HoaDon.IsDeleted);

            if (thang > 0) query = query.Where(l => l.HoaDon.Thang == thang);
            if (nam > 0) query = query.Where(l => l.HoaDon.Nam == nam);

            var lss = await query.ToListAsync();

            return lss.GroupBy(l => l.HoaDon.HopDong.PhongTro.ChiNhanh.TenChiNhanh)
                .Select(g => new DoanhThuChiNhanhRes
                {
                    ChiNhanhTen = g.Key,
                    TongDoanhThu = g.Sum(x => x.SoTienThanhToan)
                })
                .ToList();
        }

        public async Task<ChiSoDienNuocRes> GetChiSoDienNuocAsync(string soPhong, int thang, int nam)
        {
            soPhong = soPhong.ToLower();
            var dv = await _db.DichVuDienNuocCuaPhongs
                .AsNoTracking()
                .Include(d => d.PhongTro)
                .Where(d => !d.IsDeleted && d.PhongTro.SoPhong.ToLower() == soPhong && d.Thang == thang && d.Nam == nam)
                .FirstOrDefaultAsync();

            if (dv == null) return null!;

            return new ChiSoDienNuocRes
            {
                SoPhong = dv.PhongTro.SoPhong,
                Thang = dv.Thang,
                Nam = dv.Nam,
                TieuThuDien = dv.ChiSoDienMoi - dv.ChiSoDienCu,
                TieuThuNuoc = dv.ChiSoNuocMoi - dv.ChiSoNuocCu
            };
        }

        public async Task<ServiceResult> ChatWithAssistantAsync(string userMessage, List<ChatMessageDto> history, string userRole, int? nguoiThueId = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return ServiceResult.Fail("Lỗi cấu hình: Chưa thiết lập Gemini API Key trong hệ thống.");
            }

            var formattedContents = new List<object>();

            if (history != null)
            {
                foreach (var msg in history)
                {
                    formattedContents.Add(new
                    {
                        role = msg.Role,
                        parts = new[] { new { text = msg.Message } }
                    });
                }
            }

            formattedContents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            object[] toolsConfig = userRole == "Admin" ? GetAdminToolsConfig() : GetTenantToolsConfig();
            
            int currentYear = DateTime.Now.Year;
            string timeInstruction = $"Lưu ý: Năm hiện tại đang là {currentYear}. Nếu người dùng hỏi chung về hóa đơn chưa thanh toán (ví dụ: 'có hóa đơn nào chưa thanh toán không', 'lấy tất cả hóa đơn chưa thanh toán'), bạn KHÔNG ĐƯỢC hỏi lại người dùng tháng/năm mà phải gọi ngay công cụ GetHoaDonChuaThanhToanAsync với thang=0 và nam=0 để lấy TẤT CẢ các hóa đơn chưa thanh toán trong hệ thống. Nếu người dùng chỉ định tháng/năm thì mới truyền tháng/năm.";
            
            string systemInstructionText = userRole == "Admin" 
                ? $"Bạn là Trợ lý AI của hệ thống Quản lý nhà trọ dành cho Quản trị viên. Hãy dùng các công cụ (tools) được cung cấp để trả lời câu hỏi về: phòng trống, hợp đồng, khách thuê, doanh thu, công nợ, điện nước, hóa đơn. {timeInstruction} Chỉ dùng thông tin từ công cụ. Trả lời lịch sự bằng Markdown."
                : $"Bạn là Trợ lý AI chăm sóc khách hàng của khu trọ. Hãy giúp Khách Thuê tra cứu hợp đồng, điện nước và hóa đơn của họ. {timeInstruction} Tuyệt đối không tiết lộ thông tin người khác. Chỉ dùng thông tin từ công cụ. Trả lời thân thiện bằng Markdown.";

            var requestPayload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemInstructionText } }
                },
                contents = formattedContents,
                tools = toolsConfig
            };

            var serializeOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var httpContent = new StringContent(JsonSerializer.Serialize(requestPayload, serializeOptions), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync();
                return ServiceResult.Fail($"Lỗi kết nối Gemini API (HTTP {response.StatusCode}): {errContent}");
            }

            var responseJsonStr = await response.Content.ReadAsStringAsync();
            var rootNode = JsonNode.Parse(responseJsonStr);
            var part = rootNode?["candidates"]?[0]?["content"]?["parts"]?[0];

            if (part == null)
            {
                return ServiceResult.Fail("Trợ lý AI tạm thời không phản hồi. Vui lòng thử lại sau.");
            }

            if (part["functionCall"] != null)
            {
                var functionCall = part["functionCall"];
                var functionName = functionCall["name"]?.ToString() ?? string.Empty;
                var args = functionCall["args"];

                object functionResult = await HandleFunctionCallAsync(functionName, args, userRole, nguoiThueId);

                var modelCallPart = new
                {
                    role = "model",
                    parts = new[]
                    {
                        new
                        {
                            functionCall = new
                            {
                                name = functionName,
                                args = args
                            }
                        }
                    }
                };

                var functionResponsePart = new
                {
                    role = "function",
                    parts = new[]
                    {
                        new
                        {
                            functionResponse = new
                            {
                                name = functionName,
                                response = new
                                {
                                    output = functionResult
                                }
                            }
                        }
                    }
                };

                formattedContents.Add(modelCallPart);
                formattedContents.Add(functionResponsePart);

                var secondRequestPayload = new
                {
                    systemInstruction = new
                    {
                        parts = new[] { new { text = systemInstructionText } }
                    },
                    contents = formattedContents,
                    tools = toolsConfig
                };

                var secondHttpContent = new StringContent(JsonSerializer.Serialize(secondRequestPayload, serializeOptions), Encoding.UTF8, "application/json");
                var secondResponse = await _httpClient.PostAsync(url, secondHttpContent);

                if (!secondResponse.IsSuccessStatusCode)
                {
                    var errContent = await secondResponse.Content.ReadAsStringAsync();
                    return ServiceResult.Fail($"Lỗi kết nối Gemini API lượt 2 (HTTP {secondResponse.StatusCode}): {errContent}");
                }

                var secondResponseJsonStr = await secondResponse.Content.ReadAsStringAsync();
                var secondRootNode = JsonNode.Parse(secondResponseJsonStr);
                var finalAnswer = secondRootNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                return ServiceResult.Ok(finalAnswer ?? "Trợ lý AI không phản hồi sau khi lấy dữ liệu.");
            }

            return ServiceResult.Ok(part["text"]?.ToString() ?? "Trợ lý AI không phản hồi.");
        }

        private async Task<object> HandleFunctionCallAsync(string functionName, JsonNode? args, string userRole, int? nguoiThueId)
        {
            try
            {
                if (userRole == "KhachThue" && nguoiThueId.HasValue)
                {
                    switch (functionName)
                    {
                        case "GetMyHopDongInfoAsync":
                            return await GetMyHopDongInfoAsync(nguoiThueId.Value);
                        case "GetMyChiSoDienNuocAsync":
                            return await GetMyChiSoDienNuocAsync(nguoiThueId.Value, int.Parse(args?["thang"]?.ToString() ?? "0"), int.Parse(args?["nam"]?.ToString() ?? "0"));
                        case "GetMyHoaDonChuaThanhToanAsync":
                            return await GetMyHoaDonChuaThanhToanAsync(nguoiThueId.Value);
                    }
                }

                return functionName switch
                {
                    "GetPhongTroChuaChotDienNuocAsync" => await GetPhongTroChuaChotDienNuocAsync(int.Parse(args?["thang"]?.ToString() ?? "0"), int.Parse(args?["nam"]?.ToString() ?? "0")),
                    "GetHoaDonChuaThanhToanAsync" => await GetHoaDonChuaThanhToanAsync(int.Parse(args?["thang"]?.ToString() ?? "0"), int.Parse(args?["nam"]?.ToString() ?? "0")),
                    "GetDoanhThuThucThuAsync" => new
                    {
                        totalRevenue = await GetDoanhThuThucThuAsync(
                            DateTime.Parse(args?["tuNgay"]?.ToString() ?? DateTime.MinValue.ToString()),
                            DateTime.Parse(args?["denNgay"]?.ToString() ?? DateTime.MaxValue.ToString()).Date.AddDays(1).AddTicks(-1)),
                        currency = "VND"
                    },
                    "GetPhongTrongAsync" => await GetPhongTrongAsync(args?["mucGiaToiDa"] != null && double.TryParse(args["mucGiaToiDa"]!.ToString(), out double price) ? price : null),
                    "GetHopDongSapHetHanAsync" => await GetHopDongSapHetHanAsync(int.Parse(args?["soNgay"]?.ToString() ?? "30")),
                    "GetThongTinKhachThueAsync" => await GetThongTinKhachThueAsync(args?["tuKhoa"]?.ToString() ?? ""),
                    "GetCongNoPhongAsync" => new { totalDebt = await GetCongNoPhongAsync(args?["soPhong"]?.ToString() ?? ""), currency = "VND" },
                    "GetDoanhThuChiNhanhAsync" => await GetDoanhThuChiNhanhAsync(int.Parse(args?["thang"]?.ToString() ?? "0"), int.Parse(args?["nam"]?.ToString() ?? "0")),
                    "GetChiSoDienNuocAsync" => await GetChiSoDienNuocAsync(args?["soPhong"]?.ToString() ?? "", int.Parse(args?["thang"]?.ToString() ?? "0"), int.Parse(args?["nam"]?.ToString() ?? "0")),
                    _ => new { error = $"Hàm {functionName} không được hỗ trợ." }
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Lỗi thực thi hàm C#: {ex.Message}" };
            }
        }

        private object[] GetAdminToolsConfig()
        {
            return new object[]
            {
                new
                {
                    function_declarations = new object[]
                    {
                        new { name = "GetPhongTroChuaChotDienNuocAsync", description = "Lấy danh sách các phòng đang thuê nhưng chưa được chốt số điện nước trong tháng và năm chỉ định (nếu không truyền sẽ mặc định tháng năm hiện tại).", parameters = new { type = "OBJECT", properties = new { thang = new { type = "INTEGER", description = "Tháng cần kiểm tra (1-12)" }, nam = new { type = "INTEGER", description = "Năm cần kiểm tra (ví dụ 2026)" } } } },
                        new { name = "GetHoaDonChuaThanhToanAsync", description = "Lấy danh sách các hóa đơn chưa được thanh toán. Để thang=0 và nam=0 (hoặc không truyền) để lấy TẤT CẢ hóa đơn chưa thanh toán trong toàn bộ hệ thống.", parameters = new { type = "OBJECT", properties = new { thang = new { type = "INTEGER", description = "Tháng cần kiểm tra (1-12). Truyền 0 nếu muốn lấy tất cả các tháng." }, nam = new { type = "INTEGER", description = "Năm cần kiểm tra (ví dụ 2026). Truyền 0 nếu muốn lấy tất cả các năm." } } } },
                        new { name = "GetDoanhThuThucThuAsync", description = "Tính tổng doanh thu thực tế đã thu được từ lịch sử thanh toán trong khoảng thời gian từ ngày bắt đầu đến ngày kết thúc.", parameters = new { type = "OBJECT", properties = new { tuNgay = new { type = "STRING", description = "Ngày bắt đầu (định dạng YYYY-MM-DD)" }, denNgay = new { type = "STRING", description = "Ngày kết thúc (định dạng YYYY-MM-DD)" } }, required = new string[] { "tuNgay", "denNgay" } } },
                        new { name = "GetPhongTrongAsync", description = "Lấy danh sách các phòng đang trống. Có thể lọc theo mức giá tối đa.", parameters = new { type = "OBJECT", properties = new { mucGiaToiDa = new { type = "NUMBER", description = "Mức giá thuê tối đa (nếu có, ví dụ 3000000)" } } } },
                        new { name = "GetHopDongSapHetHanAsync", description = "Lấy danh sách các hợp đồng sắp hết hạn trong X ngày tới.", parameters = new { type = "OBJECT", properties = new { soNgay = new { type = "INTEGER", description = "Số ngày tới (ví dụ 30)" } }, required = new string[] { "soNgay" } } },
                        new { name = "GetThongTinKhachThueAsync", description = "Tra cứu thông tin khách thuê dựa vào tên hoặc số phòng.", parameters = new { type = "OBJECT", properties = new { tuKhoa = new { type = "STRING", description = "Tên khách thuê hoặc số phòng (ví dụ 'Nguyễn Văn A' hoặc '101')" } }, required = new string[] { "tuKhoa" } } },
                        new { name = "GetCongNoPhongAsync", description = "Tra cứu tổng công nợ (số tiền chưa thanh toán) của một phòng cụ thể.", parameters = new { type = "OBJECT", properties = new { soPhong = new { type = "STRING", description = "Số phòng cần tra cứu (ví dụ '101')" } }, required = new string[] { "soPhong" } } },
                        new { name = "GetDoanhThuChiNhanhAsync", description = "Tính doanh thu thu được theo từng chi nhánh trong tháng và năm chỉ định.", parameters = new { type = "OBJECT", properties = new { thang = new { type = "INTEGER", description = "Tháng (1-12)" }, nam = new { type = "INTEGER", description = "Năm (ví dụ 2026)" } }, required = new string[] { "thang", "nam" } } },
                        new { name = "GetChiSoDienNuocAsync", description = "Lấy thông tin chỉ số điện nước (cũ, mới, tiêu thụ) của một phòng trong tháng và năm chỉ định.", parameters = new { type = "OBJECT", properties = new { soPhong = new { type = "STRING", description = "Số phòng (ví dụ '101')" }, thang = new { type = "INTEGER", description = "Tháng (1-12)" }, nam = new { type = "INTEGER", description = "Năm (ví dụ 2026)" } }, required = new string[] { "soPhong", "thang", "nam" } } }
                    }
                }
            };
        }

        private object[] GetTenantToolsConfig()
        {
            return new object[]
            {
                new
                {
                    function_declarations = new object[]
                    {
                        new { name = "GetMyHopDongInfoAsync", description = "Tra cứu thông tin chi tiết về hợp đồng thuê phòng hiện tại của tôi (phòng nào, giá thuê, ngày hết hạn...)" },
                        new { name = "GetMyChiSoDienNuocAsync", description = "Xem chỉ số điện nước của phòng tôi đang thuê trong một tháng cụ thể.", parameters = new { type = "OBJECT", properties = new { thang = new { type = "INTEGER", description = "Tháng (1-12)" }, nam = new { type = "INTEGER", description = "Năm (ví dụ 2026)" } }, required = new string[] { "thang", "nam" } } },
                        new { name = "GetMyHoaDonChuaThanhToanAsync", description = "Tra cứu các hóa đơn hoặc công nợ mà tôi chưa thanh toán." }
                    }
                }
            };
        }

        private async Task<object> GetMyHopDongInfoAsync(int nguoiThueId)
        {
            var hd = await _db.HopDongs
                .AsNoTracking()
                .Include(h => h.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Where(h => !h.IsDeleted && h.NguoiThueId == nguoiThueId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                .FirstOrDefaultAsync();

            if (hd == null) return new { message = "Hiện tại bạn không có hợp đồng nào đang hoạt động." };

            return new
            {
                SoPhong = hd.PhongTro.SoPhong,
                ChiNhanh = hd.PhongTro.ChiNhanh.TenChiNhanh,
                NgayBatDau = hd.ThoiDiemBatDau.ToString("dd/MM/yyyy"),
                NgayKetThuc = hd.ThoiDiemKetThuc?.ToString("dd/MM/yyyy") ?? "Không thời hạn",
                GiaThue = hd.TienThuePhong,
                TienCoc = hd.TienCocPhong
            };
        }

        private async Task<object> GetMyChiSoDienNuocAsync(int nguoiThueId, int thang, int nam)
        {
            var hd = await _db.HopDongs.AsNoTracking()
                .Where(h => !h.IsDeleted && h.NguoiThueId == nguoiThueId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                .FirstOrDefaultAsync();
            if (hd == null) return new { message = "Không tìm thấy hợp đồng." };

            var dv = await _db.DichVuDienNuocCuaPhongs.AsNoTracking()
                .Where(d => !d.IsDeleted && d.PhongTroId == hd.PhongTroId && d.Thang == thang && d.Nam == nam)
                .FirstOrDefaultAsync();

            if (dv == null) return new { message = $"Chưa có số điện nước cho tháng {thang}/{nam}." };

            return new
            {
                Thang = thang,
                Nam = nam,
                ChiSoDienCu = dv.ChiSoDienCu,
                ChiSoDienMoi = dv.ChiSoDienMoi,
                SoDienTieuThu = dv.ChiSoDienMoi - dv.ChiSoDienCu,
                ChiSoNuocCu = dv.ChiSoNuocCu,
                ChiSoNuocMoi = dv.ChiSoNuocMoi,
                SoNuocTieuThu = dv.ChiSoNuocMoi - dv.ChiSoNuocCu
            };
        }

        private async Task<object> GetMyHoaDonChuaThanhToanAsync(int nguoiThueId)
        {
            var hoaDons = await _db.HoaDons.AsNoTracking()
                .Include(h => h.HopDong)
                .Where(h => !h.IsDeleted && h.HopDong.NguoiThueId == nguoiThueId && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan)
                .ToListAsync();

            if (!hoaDons.Any()) return new { message = "Tuyệt vời, bạn không có hóa đơn nào chưa thanh toán." };

            return hoaDons.Select(h => new
            {
                MaHoaDon = h.MaHoaDon,
                Thang = h.Thang,
                Nam = h.Nam,
                TongTien = h.TongTien
            });
        }
    }
}
