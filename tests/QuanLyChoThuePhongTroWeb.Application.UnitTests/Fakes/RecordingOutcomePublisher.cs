using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class RecordingOutcomePublisher : IPaymentOutcomePublisher
    {
        public List<TransferOutcome> Outcomes { get; } = new();
        public bool Throw { get; set; }

        public Task PublishAsync(TransferOutcome outcome, CancellationToken ct = default)
        {
            Outcomes.Add(outcome);

            if (Throw)
            {
                throw new InvalidOperationException("Publisher down");
            }

            return Task.CompletedTask;
        }
    }
}
