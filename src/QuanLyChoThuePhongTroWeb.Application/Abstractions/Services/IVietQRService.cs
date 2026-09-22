namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IVietQRService
    {
        string GenerateVietQRString(string bankIdOrBin, string accountNumber, decimal amount, string memo);
        byte[] GenerateQRCodePNGBytes(string payload);
    }
}
