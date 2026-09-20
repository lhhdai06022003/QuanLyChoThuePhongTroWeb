namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IVietQRService
    {
        string GenerateVietQRString(string bankIdOrBin, string accountNumber, double amount, string memo);
        byte[] GenerateQRCodePNGBytes(string payload);
    }
}
