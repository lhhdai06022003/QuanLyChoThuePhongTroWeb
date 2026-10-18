using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class RecordingPaymentNotificationQueue : IPaymentNotificationQueue
    {
        public List<PaymentNotificationWorkItem> Items { get; } = new();
        // False: mô phỏng hàng đợi đầy.
        public bool Accept { get; set; } = true;
        public bool Throw { get; set; }

        public ValueTask<bool> EnqueueAsync(PaymentNotificationWorkItem item, CancellationToken ct = default)
        {
            if (Throw)
            {
                throw new InvalidOperationException("Queue down");
            }

            if (!Accept)
            {
                return new ValueTask<bool>(false);
            }

            Items.Add(item);
            return new ValueTask<bool>(true);
        }
    }
}
