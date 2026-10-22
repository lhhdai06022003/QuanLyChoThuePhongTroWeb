document.addEventListener('DOMContentLoaded', () => {
    const modal = document.getElementById('listingPreviewModal');
    if (!modal) return;
    const form = document.getElementById('listingForm');
    const roomId = document.getElementById('listingRoomId');
    const roomLabel = document.getElementById('listingPreviewRoom');
    const title = document.getElementById('listingTitle');
    const visibility = document.getElementById('listingVisibility');
    const description = document.getElementById('listingDescription');
    const photos = document.getElementById('listingPhotos');
    const hint = document.getElementById('listingPhotoHint');
    const save = document.getElementById('listingSave');
    let existing = 0;
    const validatePhotos = () => {
        const files = Array.from(photos.files || []);
        const error = existing + files.length > 10 ? 'Mỗi phòng tối đa 10 ảnh.' :
            files.some(file => file.size <= 0 || file.size > 5 * 1024 * 1024) ? 'Mỗi ảnh tối đa 5 MB.' :
            files.some(file => !/\.(jpe?g|png|webp)$/i.test(file.name)) ? 'Chỉ chấp nhận JPG, PNG hoặc WebP.' : '';
        photos.setCustomValidity(error);
        hint.textContent = error || `${existing} ảnh hiện có. Đã chọn thêm ${files.length} ảnh. Tối đa 5 MB/ảnh, 10 ảnh/phòng.`;
        save.disabled = Boolean(error);
    };
    document.querySelectorAll('[data-listing-open]').forEach(button => {
        button.addEventListener('click', () => {
            form.reset();
            roomId.value = button.dataset.roomId || '';
            const room = button.dataset.room || '';
            const branch = button.dataset.branch || '';
            roomLabel.textContent = `Phòng ${room} · ${branch} · ${button.dataset.price || '0'} ₫/tháng`;
            title.value = button.dataset.title || `Phòng ${room} tại ${branch}`;
            description.value = button.dataset.description || '';
            visibility.value = button.dataset.visible === 'true' ? 'true' : 'false';
            existing = Number(button.dataset.images || 0);
            save.textContent = 'Lưu tin và ảnh';
            validatePhotos();
        });
    });
    photos.addEventListener('change', validatePhotos);
    form.addEventListener('submit', event => {
        validatePhotos();
        if (!form.checkValidity()) {
            event.preventDefault();
            form.reportValidity();
            return;
        }
        save.disabled = true;
        save.textContent = 'Đang lưu…';
    });
    window.addEventListener('pageshow', () => { save.textContent = 'Lưu tin và ảnh'; validatePhotos(); });
});
