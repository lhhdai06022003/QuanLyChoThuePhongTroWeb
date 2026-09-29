const viewingActionForm = document.getElementById('viewingActionForm');

if (viewingActionForm) {
    const actionValue = document.getElementById('viewingActionValue');
    const title = document.getElementById('viewingActionTitle');
    const room = document.getElementById('viewingActionRoom');
    const timeGroup = document.getElementById('viewingActionTimeGroup');
    const slotSelect = document.getElementById('viewingActionSlot');
    const slotStatus = document.getElementById('viewingActionSlotStatus');
    const submit = document.getElementById('viewingActionSubmit');
    const labels = {
        Reschedule: 'Đổi lịch',
        Cancel: 'Hủy lịch',
        Complete: 'Đã xem phòng'
    };
    let slotRequest;

    slotSelect.addEventListener('change', () => {
        submit.disabled = !slotSelect.value;
    });

    document.querySelectorAll('[data-viewing-action]').forEach((button) => {
        button.addEventListener('click', async () => {
            const action = button.dataset.viewingAction;
            if (!Object.hasOwn(labels, action)) return;

            slotRequest?.abort();
            slotRequest = undefined;
            viewingActionForm.reset();
            viewingActionForm.action = button.dataset.viewingUrl;
            actionValue.value = action;
            title.textContent = labels[action];
            room.textContent = `Phòng ${button.dataset.viewingRoom}`;
            timeGroup.hidden = action !== 'Reschedule';
            slotSelect.disabled = true;
            slotSelect.required = action === 'Reschedule';
            slotSelect.replaceChildren(new Option('Chọn khung giờ còn chỗ', ''));
            submit.textContent = labels[action];
            submit.classList.toggle('btn-danger', action === 'Cancel');
            submit.classList.toggle('btn-primary', action !== 'Cancel');
            submit.disabled = action === 'Reschedule';
            if (action !== 'Reschedule') return;

            slotStatus.textContent = 'Đang tải các khung giờ còn chỗ...';
            slotRequest = new AbortController();
            const request = slotRequest;
            try {
                const response = await fetch(button.dataset.viewingSlotsUrl, {
                    credentials: 'same-origin',
                    signal: request.signal
                });
                if (!response.ok) throw new Error('Không tải được khung giờ.');
                const slots = await response.json();
                if (request !== slotRequest) return;
                for (const slot of slots) {
                    slotSelect.add(new Option(slot.label, String(slot.id)));
                }
                slotSelect.disabled = slots.length === 0;
                slotStatus.textContent = slots.length > 0
                    ? 'Chọn khung giờ đã thống nhất với khách.'
                    : 'Phòng này chưa có khung giờ nào còn chỗ. Hãy tạo khung giờ mới trước khi đổi lịch.';
            } catch (error) {
                if (error.name !== 'AbortError' && request === slotRequest) {
                    slotStatus.textContent = 'Không tải được khung giờ. Đóng và mở lại để thử tiếp.';
                }
            }
        });
    });

    document.getElementById('viewingActionModal').addEventListener('hidden.bs.modal', () => {
        slotRequest?.abort();
        slotRequest = undefined;
    });
}
