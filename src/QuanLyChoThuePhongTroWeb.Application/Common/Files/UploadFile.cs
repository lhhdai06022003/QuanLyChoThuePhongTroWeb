using System.IO;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Files
{
    public sealed record UploadFile(
        Stream Content,
        string FileName,
        string ContentType,
        long Length);
}
