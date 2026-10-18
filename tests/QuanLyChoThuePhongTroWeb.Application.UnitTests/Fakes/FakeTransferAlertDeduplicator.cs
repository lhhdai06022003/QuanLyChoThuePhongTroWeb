using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeTransferAlertDeduplicator : ITransferAlertDeduplicator
    {
        private readonly HashSet<string> _seen = new();

        public bool Throw { get; set; }
        // Số lần được hỏi: Recorded, Duplicate, Ignored, Failed không được đi qua bộ chống lặp.
        public int Calls { get; private set; }

        public bool TryRegister(string dedupKey)
        {
            Calls++;
            if (Throw)
            {
                throw new InvalidOperationException("Cache down");
            }

            return _seen.Add(dedupKey);
        }
    }
}
