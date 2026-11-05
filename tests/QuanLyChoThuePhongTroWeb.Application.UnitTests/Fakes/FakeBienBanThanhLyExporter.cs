using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeBienBanThanhLyExporter : IBienBanThanhLyExporter
    {
        public BienBanThanhLyDto? LastData { get; private set; }

        public byte[] ExportPdf(BienBanThanhLyDto data)
        {
            LastData = data;
            return new byte[] { 0x25, 0x50, 0x44, 0x46 };
        }
    }
}
