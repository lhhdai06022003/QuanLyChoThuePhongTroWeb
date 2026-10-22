using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IMeterOcrService
    {
        Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default);
    }
}
