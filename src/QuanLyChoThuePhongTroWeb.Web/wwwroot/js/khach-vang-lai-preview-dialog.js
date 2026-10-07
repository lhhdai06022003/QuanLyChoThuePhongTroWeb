document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-preview-dialog-open]').forEach(button => {
        button.addEventListener('click', () => {
            const dialog = document.getElementById(button.dataset.previewDialogOpen);
            if (dialog instanceof HTMLDialogElement) dialog.showModal();
        });
    });

    document.querySelectorAll('[data-preview-dialog-close]').forEach(button => {
        button.addEventListener('click', () => button.closest('dialog')?.close());
    });
});
