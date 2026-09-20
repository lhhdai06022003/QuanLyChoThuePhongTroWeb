using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
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
        
        Task<ServiceResult> ChatWithAssistantAsync(string userMessage, List<ChatMessageDto> history, string userRole, int? nguoiThueId = null);
    }
}
