using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IContractDocumentGenerator
    {
        byte[] GenerateHopDongWord(HopDongPrintRes data);
    }
}
