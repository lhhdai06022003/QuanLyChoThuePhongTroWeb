document.querySelectorAll('[data-room-gallery]').forEach((gallery) => {
    const main = gallery.querySelector('[data-gallery-main]');
    const thumbs = [...gallery.querySelectorAll('[data-gallery-thumb]')];
    if (!main || thumbs.length < 2) return;

    const count = gallery.querySelector('[data-gallery-count]');
    const strip = gallery.querySelector('.guest-gallery-thumbs');
    let current = 0;

    const show = (next) => {
        current = (next + thumbs.length) % thumbs.length;
        const selected = thumbs[current];
        main.src = selected.dataset.gallerySrc;
        main.alt = selected.dataset.galleryAlt || 'Ảnh phòng';
        thumbs.forEach((thumb, index) => thumb.setAttribute('aria-pressed', String(index === current)));
        count.textContent = `${current + 1} / ${thumbs.length}`;
        if (strip) {
            const left = selected.offsetLeft - strip.offsetLeft - (strip.clientWidth - selected.offsetWidth) / 2;
            const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            strip.scrollTo({ left, behavior: reduceMotion ? 'auto' : 'smooth' });
        }
    };

    thumbs.forEach((thumb, index) => thumb.addEventListener('click', () => show(index)));
    gallery.querySelector('[data-gallery-prev]').addEventListener('click', () => show(current - 1));
    gallery.querySelector('[data-gallery-next]').addEventListener('click', () => show(current + 1));
    gallery.addEventListener('keydown', (event) => {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
        event.preventDefault();
        show(current + (event.key === 'ArrowRight' ? 1 : -1));
    });
});
