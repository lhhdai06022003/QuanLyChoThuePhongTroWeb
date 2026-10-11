using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Ghi lại các lô email tạm tính được xếp hàng gửi nền; không gửi thật.
    public class RecordingProvisionalEmailDispatcher : IProvisionalInvoiceEmailDispatcher
    {
        public List<IReadOnlyList<PendingProvisionalEmail>> Batches { get; } = new();
        public bool ThrowOnEnqueue { get; set; }

        public void Enqueue(IReadOnlyList<PendingProvisionalEmail> emails)
        {
            if (ThrowOnEnqueue)
            {
                throw new InvalidOperationException("Không xếp hàng được");
            }

            Batches.Add(emails);
        }
    }
}
