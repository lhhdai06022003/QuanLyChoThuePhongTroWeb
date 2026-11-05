using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs
{
    public sealed class BaoCaoFileDto
    {
        public byte[] NoiDung { get; init; } = Array.Empty<byte>();
        public string TenFile { get; init; } = "";
    }
}
