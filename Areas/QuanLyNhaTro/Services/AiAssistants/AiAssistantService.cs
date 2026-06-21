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
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.AiAssistant;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.AiAssistants
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

        public async Task<List<PhongTroChuaChotRes>> GetPhongTroChuaChotDienNuocAsync(int thang, int nam)
        {
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

        public async Task<List<HoaDonChuaThanhToanRes>> GetHoaDonChuaThanhToanAsync(int thang, int nam)
        {
            return await _db.HoaDons
                .AsNoTracking()
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted && 
                            h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan && 
                            h.Thang == thang && 
                            h.Nam == nam && 
                            h.HopDong.PhongTro.TrangThai == TrangThaiPhong.DaThue)
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
            // Chuyển đổi sang UTC để khớp với lưu trữ trong PostgreSQL
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

        public async Task<List<DoanhThuChiNhanhRes>> GetDoanhThuChiNhanhAsync(int thang, int nam)
        {
            var lss = await _db.LichSuThanhToans
                .AsNoTracking()
                .Include(l => l.HoaDon).ThenInclude(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Where(l => !l.IsDeleted && !l.HoaDon.IsDeleted && l.HoaDon.Thang == thang && l.HoaDon.Nam == nam)
                .ToListAsync();

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

            if (dv == null) return null;

            return new ChiSoDienNuocRes
            {
                SoPhong = dv.PhongTro.SoPhong,
                Thang = dv.Thang,
                Nam = dv.Nam,
                TieuThuDien = dv.ChiSoDienMoi - dv.ChiSoDienCu,
                TieuThuNuoc = dv.ChiSoNuocMoi - dv.ChiSoNuocCu
            };
        }

        public async Task<string> ChatWithAssistantAsync(string userMessage, List<ChatMessageDto> history)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return "Lỗi cấu hình: Chưa thiết lập Gemini API Key trong hệ thống.";
            }

            // Tạo danh sách contents theo chuẩn API Gemini
            var formattedContents = new List<object>();

            if (history != null)
            {
                foreach (var msg in history)
                {
                    // Lịch sử tin nhắn
                    formattedContents.Add(new
                    {
                        role = msg.Role,
                        parts = new[] { new { text = msg.Message } }
                    });
                }
            }

            // Thêm câu hỏi mới của người dùng
            formattedContents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            // Khai báo tools (Function Declarations) gửi sang Gemini
            var toolsConfig = new object[]
            {
                new
                {
                    function_declarations = new object[]
                    {
                        new
                        {
                            name = "GetPhongTroChuaChotDienNuocAsync",
                            description = "Lấy danh sách các phòng đang thuê nhưng chưa được chốt số điện nước trong tháng và năm chỉ định.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    thang = new { type = "INTEGER", description = "Tháng cần kiểm tra (1-12)" },
                                    nam = new { type = "INTEGER", description = "Năm cần kiểm tra (ví dụ 2026)" }
                                },
                                required = new string[] { "thang", "nam" }
                            }
                        },
                        new
                        {
                            name = "GetHoaDonChuaThanhToanAsync",
                            description = "Lấy danh sách các hóa đơn chưa được thanh toán trong tháng và năm chỉ định.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    thang = new { type = "INTEGER", description = "Tháng cần kiểm tra (1-12)" },
                                    nam = new { type = "INTEGER", description = "Năm cần kiểm tra (ví dụ 2026)" }
                                },
                                required = new string[] { "thang", "nam" }
                            }
                        },
                        new
                        {
                            name = "GetDoanhThuThucThuAsync",
                            description = "Tính tổng doanh thu thực tế đã thu được từ lịch sử thanh toán trong khoảng thời gian từ ngày bắt đầu đến ngày kết thúc.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    tuNgay = new { type = "STRING", description = "Ngày bắt đầu (định dạng YYYY-MM-DD)" },
                                    denNgay = new { type = "STRING", description = "Ngày kết thúc (định dạng YYYY-MM-DD)" }
                                },
                                required = new string[] { "tuNgay", "denNgay" }
                            }
                        },
                        new
                        {
                            name = "GetPhongTrongAsync",
                            description = "Lấy danh sách các phòng đang trống. Có thể lọc theo mức giá tối đa.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    mucGiaToiDa = new { type = "NUMBER", description = "Mức giá thuê tối đa (nếu có, ví dụ 3000000)" }
                                }
                            }
                        },
                        new
                        {
                            name = "GetHopDongSapHetHanAsync",
                            description = "Lấy danh sách các hợp đồng sắp hết hạn trong X ngày tới.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    soNgay = new { type = "INTEGER", description = "Số ngày tới (ví dụ 30)" }
                                },
                                required = new string[] { "soNgay" }
                            }
                        },
                        new
                        {
                            name = "GetThongTinKhachThueAsync",
                            description = "Tra cứu thông tin khách thuê dựa vào tên hoặc số phòng.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    tuKhoa = new { type = "STRING", description = "Tên khách thuê hoặc số phòng (ví dụ 'Nguyễn Văn A' hoặc '101')" }
                                },
                                required = new string[] { "tuKhoa" }
                            }
                        },
                        new
                        {
                            name = "GetCongNoPhongAsync",
                            description = "Tra cứu tổng công nợ (số tiền chưa thanh toán) của một phòng cụ thể.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    soPhong = new { type = "STRING", description = "Số phòng cần tra cứu (ví dụ '101')" }
                                },
                                required = new string[] { "soPhong" }
                            }
                        },
                        new
                        {
                            name = "GetDoanhThuChiNhanhAsync",
                            description = "Tính doanh thu thu được theo từng chi nhánh trong tháng và năm chỉ định.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    thang = new { type = "INTEGER", description = "Tháng (1-12)" },
                                    nam = new { type = "INTEGER", description = "Năm (ví dụ 2026)" }
                                },
                                required = new string[] { "thang", "nam" }
                            }
                        },
                        new
                        {
                            name = "GetChiSoDienNuocAsync",
                            description = "Lấy thông tin chỉ số điện nước (cũ, mới, tiêu thụ) của một phòng trong tháng và năm chỉ định.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    soPhong = new { type = "STRING", description = "Số phòng (ví dụ '101')" },
                                    thang = new { type = "INTEGER", description = "Tháng (1-12)" },
                                    nam = new { type = "INTEGER", description = "Năm (ví dụ 2026)" }
                                },
                                required = new string[] { "soPhong", "thang", "nam" }
                            }
                        }
                    }
                }
            };

            // Payload chung cho yêu cầu gửi đi
            var requestPayload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = "Bạn là Trợ lý AI của hệ thống Quản lý nhà trọ. Hãy dùng các công cụ (tools) được cung cấp để trả lời các câu hỏi về: phòng trống, hợp đồng, thông tin khách, doanh thu, công nợ, điện nước, hóa đơn. Chỉ sử dụng thông tin từ công cụ. Nếu không có dữ liệu, hãy nói rõ. Trả lời bằng tiếng Việt lịch sự, thân thiện, và định dạng Markdown rõ ràng, đẹp mắt (sử dụng bảng biểu hoặc danh sách nếu có nhiều dữ liệu)." } }
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
                return $"Lỗi kết nối Gemini API (HTTP {response.StatusCode}): {errContent}";
            }

            var responseJsonStr = await response.Content.ReadAsStringAsync();
            var rootNode = JsonNode.Parse(responseJsonStr);
            var part = rootNode?["candidates"]?[0]?["content"]?["parts"]?[0];

            if (part == null)
            {
                return "Trợ lý AI tạm thời không phản hồi. Vui lòng thử lại sau.";
            }

            // Kiểm tra xem Gemini có yêu cầu gọi hàm (Function Call) không
            if (part["functionCall"] != null)
            {
                var functionCall = part["functionCall"];
                var functionName = functionCall["name"]?.ToString();
                var args = functionCall["args"];

                object functionResult = null;
                try
                {
                    if (functionName == "GetPhongTroChuaChotDienNuocAsync")
                    {
                        int thang = int.Parse(args["thang"]?.ToString() ?? "0");
                        int nam = int.Parse(args["nam"]?.ToString() ?? "0");
                        functionResult = await GetPhongTroChuaChotDienNuocAsync(thang, nam);
                    }
                    else if (functionName == "GetHoaDonChuaThanhToanAsync")
                    {
                        int thang = int.Parse(args["thang"]?.ToString() ?? "0");
                        int nam = int.Parse(args["nam"]?.ToString() ?? "0");
                        functionResult = await GetHoaDonChuaThanhToanAsync(thang, nam);
                    }
                    else if (functionName == "GetDoanhThuThucThuAsync")
                    {
                        DateTime tuNgay = DateTime.Parse(args["tuNgay"]?.ToString() ?? DateTime.MinValue.ToString());
                        DateTime denNgay = DateTime.Parse(args["denNgay"]?.ToString() ?? DateTime.MaxValue.ToString());
                        // Đặt mốc thời gian kết thúc đến cuối ngày
                        denNgay = denNgay.Date.AddDays(1).AddTicks(-1);
                        var totalRevenue = await GetDoanhThuThucThuAsync(tuNgay, denNgay);
                        functionResult = new { totalRevenue = totalRevenue, currency = "VND" };
                    }
                    else if (functionName == "GetPhongTrongAsync")
                    {
                        double? mucGiaToiDa = null;
                        if (args["mucGiaToiDa"] != null && double.TryParse(args["mucGiaToiDa"].ToString(), out double price))
                        {
                            mucGiaToiDa = price;
                        }
                        functionResult = await GetPhongTrongAsync(mucGiaToiDa);
                    }
                    else if (functionName == "GetHopDongSapHetHanAsync")
                    {
                        int soNgay = int.Parse(args["soNgay"]?.ToString() ?? "30");
                        functionResult = await GetHopDongSapHetHanAsync(soNgay);
                    }
                    else if (functionName == "GetThongTinKhachThueAsync")
                    {
                        string tuKhoa = args["tuKhoa"]?.ToString() ?? "";
                        functionResult = await GetThongTinKhachThueAsync(tuKhoa);
                    }
                    else if (functionName == "GetCongNoPhongAsync")
                    {
                        string soPhong = args["soPhong"]?.ToString() ?? "";
                        var debt = await GetCongNoPhongAsync(soPhong);
                        functionResult = new { totalDebt = debt, currency = "VND" };
                    }
                    else if (functionName == "GetDoanhThuChiNhanhAsync")
                    {
                        int thang = int.Parse(args["thang"]?.ToString() ?? "0");
                        int nam = int.Parse(args["nam"]?.ToString() ?? "0");
                        functionResult = await GetDoanhThuChiNhanhAsync(thang, nam);
                    }
                    else if (functionName == "GetChiSoDienNuocAsync")
                    {
                        string soPhong = args["soPhong"]?.ToString() ?? "";
                        int thang = int.Parse(args["thang"]?.ToString() ?? "0");
                        int nam = int.Parse(args["nam"]?.ToString() ?? "0");
                        functionResult = await GetChiSoDienNuocAsync(soPhong, thang, nam);
                    }
                }
                catch (Exception ex)
                {
                    functionResult = new { error = $"Lỗi thực thi hàm C#: {ex.Message}" };
                }

                // Thực hiện lượt gọi thứ 2 gửi kết quả của function cho Gemini để tổng hợp câu trả lời
                // Ta cần thêm model's functionCall và function's response vào contents gửi đi
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
                        parts = new[] { new { text = "Bạn là Trợ lý AI của hệ thống Quản lý nhà trọ. Hãy dùng các công cụ (tools) được cung cấp để trả lời các câu hỏi về: phòng trống, hợp đồng, thông tin khách, doanh thu, công nợ, điện nước, hóa đơn. Chỉ sử dụng thông tin từ công cụ. Nếu không có dữ liệu, hãy nói rõ. Trả lời bằng tiếng Việt lịch sự, thân thiện, và định dạng Markdown rõ ràng, đẹp mắt (sử dụng bảng biểu hoặc danh sách nếu có nhiều dữ liệu)." } }
                    },
                    contents = formattedContents,
                    tools = toolsConfig
                };

                var secondHttpContent = new StringContent(JsonSerializer.Serialize(secondRequestPayload, serializeOptions), Encoding.UTF8, "application/json");
                var secondResponse = await _httpClient.PostAsync(url, secondHttpContent);

                if (!secondResponse.IsSuccessStatusCode)
                {
                    var errContent = await secondResponse.Content.ReadAsStringAsync();
                    return $"Lỗi kết nối Gemini API lượt 2 (HTTP {secondResponse.StatusCode}): {errContent}";
                }

                var secondResponseJsonStr = await secondResponse.Content.ReadAsStringAsync();
                var secondRootNode = JsonNode.Parse(secondResponseJsonStr);
                var finalAnswer = secondRootNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                return finalAnswer ?? "Trợ lý AI không phản hồi sau khi lấy dữ liệu.";
            }

            // Nếu không gọi hàm, trả về text bình thường
            return part["text"]?.ToString() ?? "Trợ lý AI không phản hồi.";
        }
    }
}
