using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.AiAssistant;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.AiAssistants
{
    public interface IAiAssistantService
    {
        Task<List<PhongTroChuaChotRes>> GetPhongTroChuaChotDienNuocAsync(int thang = 0, int nam = 0);
        Task<List<HoaDonChuaThanhToanRes>> GetHoaDonChuaThanhToanAsync(int thang = 0, int nam = 0);
        Task<double> GetDoanhThuThucThuAsync(DateTime tuNgay, DateTime denNgay);
        
        Task<List<PhongTrongRes>> GetPhongTrongAsync(double? mucGiaToiDa);
        Task<List<HopDongSapHetHanRes>> GetHopDongSapHetHanAsync(int soNgay);
        Task<List<ThongTinKhachThueRes>> GetThongTinKhachThueAsync(string tuKhoa);
        Task<double> GetCongNoPhongAsync(string soPhong);
        Task<List<DoanhThuChiNhanhRes>> GetDoanhThuChiNhanhAsync(int thang = 0, int nam = 0);
        Task<ChiSoDienNuocRes> GetChiSoDienNuocAsync(string soPhong, int thang, int nam);
        
        Task<QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.ServiceResult> ChatWithAssistantAsync(string userMessage, List<ChatMessageDto> history, string userRole, int? nguoiThueId = null);
    }
}
