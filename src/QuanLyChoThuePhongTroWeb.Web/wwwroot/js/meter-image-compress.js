/*
 * Nén ảnh đồng hồ ở trình duyệt trước khi tải lên.
 * Chỉ nén khi cạnh lớn nhất > 1600 px hoặc dung lượng > 2 MB; kết quả là JPEG chất lượng 0,85, cạnh lớn nhất 1600 px.
 * Lỗi hoặc kết quả không nhỏ hơn bản gốc thì trả đúng tệp gốc. Giới hạn 5 MB ở máy chủ giữ nguyên.
 */
(function (window) {
    'use strict';

    var MAX_EDGE = 1600;
    var MAX_BYTES = 2 * 1024 * 1024;
    var QUALITY = 0.85;

    function canvasToBlob(canvas) {
        return new Promise(function (resolve) {
            canvas.toBlob(function (blob) { resolve(blob); }, 'image/jpeg', QUALITY);
        });
    }

    function renameToJpeg(name) {
        var base = (name || 'meter').replace(/\.[^.\/\\]+$/, '');
        return base + '.jpg';
    }

    async function compress(file) {
        try {
            if (!file || !/^image\//.test(file.type) || typeof createImageBitmap !== 'function') {
                return file;
            }

            // from-image: đúng chiều ảnh theo EXIF (ảnh chụp điện thoại).
            var bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
            var longest = Math.max(bitmap.width, bitmap.height);

            if (longest <= MAX_EDGE && file.size <= MAX_BYTES) {
                if (bitmap.close) bitmap.close();
                return file;
            }

            var scale = Math.min(1, MAX_EDGE / longest);
            var width = Math.max(1, Math.round(bitmap.width * scale));
            var height = Math.max(1, Math.round(bitmap.height * scale));

            var canvas = document.createElement('canvas');
            canvas.width = width;
            canvas.height = height;
            var ctx = canvas.getContext('2d');
            ctx.drawImage(bitmap, 0, 0, width, height);
            if (bitmap.close) bitmap.close();

            var blob = await canvasToBlob(canvas);
            if (!blob || blob.size >= file.size) {
                return file;
            }

            return new File([blob], renameToJpeg(file.name), { type: 'image/jpeg', lastModified: Date.now() });
        } catch (e) {
            return file;
        }
    }

    window.MeterImageCompress = { compress: compress };
})(window);
