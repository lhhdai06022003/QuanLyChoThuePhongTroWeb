const approveForm = document.getElementById('approveReservationForm');
const closeForm = document.getElementById('closeReservationForm');

if (approveForm && closeForm) {
    const approveSummary = document.getElementById('approveReservationSummary');
    const approveAmount = document.getElementById('approveReservationAmount');
    const closeSummary = document.getElementById('closeReservationSummary');

    document.querySelectorAll('[data-reservation-approve]').forEach((button) => {
        button.addEventListener('click', () => {
            approveForm.reset();
            approveForm.action = button.dataset.actionUrl;
            approveSummary.textContent = `Phòng ${button.dataset.room} · Khách ${button.dataset.customer}`;
            approveAmount.textContent = `${button.dataset.amount} ₫`;
        });
    });

    document.querySelectorAll('[data-reservation-close]').forEach((button) => {
        button.addEventListener('click', () => {
            closeForm.reset();
            closeForm.action = button.dataset.actionUrl;
            closeSummary.textContent = `Phòng ${button.dataset.room} · Khách ${button.dataset.customer}`;
        });
    });
}
