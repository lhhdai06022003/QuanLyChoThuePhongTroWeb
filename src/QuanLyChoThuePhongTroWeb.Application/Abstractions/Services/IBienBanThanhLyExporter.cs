using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IBienBanThanhLyExporter
    {
        byte[] ExportPdf(BienBanThanhLyDto data);
    }
}
