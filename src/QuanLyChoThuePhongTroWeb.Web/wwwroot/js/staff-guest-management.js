document.addEventListener('DOMContentLoaded', () => {
    const modal = document.getElementById('listingPreviewModal');
    if (!modal) return;

    const roomLabel = document.getElementById('listingPreviewRoom');
    const titleInput = document.getElementById('listingTitle');
    const visibilityInput = document.getElementById('listingVisibility');
    const descriptionInput = document.getElementById('listingDescription');
    const photoInput = document.getElementById('listingPhotos');
    const photoHint = document.getElementById('listingPhotoHint');

    document.querySelectorAll('[data-listing-open]').forEach(button => {
        button.addEventListener('click', () => {
            const room = button.dataset.room || '';
            const branch = button.dataset.branch || '';
            const imageCount = Number(button.dataset.images || 0);

            roomLabel.textContent = `Phòng ${room} · ${branch} · ${button.dataset.price || '0'} ₫/tháng`;
            titleInput.value = `Phòng ${room} tại ${branch}`;
            visibilityInput.value = button.dataset.visible === 'true' ? 'true' : 'false';
            descriptionInput.value = '';
            photoInput.value = '';
            photoHint.textContent = `${imageCount} ảnh hiện có. Chọn ảnh JPEG, PNG hoặc WebP.`;
            visibilityInput.disabled = button.dataset.ready !== 'true';
            if (visibilityInput.disabled) {
                photoHint.textContent += ' Phòng chưa đủ điều kiện đăng công khai.';
            }
        });
    });

    photoInput.addEventListener('change', () => {
        const count = photoInput.files ? photoInput.files.length : 0;
        if (count > 0) {
            photoHint.textContent = `Đã chọn ${count} ảnh.`;
        }
    });
});
