(function (window, $) {
    'use strict';

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    class MeterImageViewer {
        // Nhận một phần tử/selector, hoặc object tuỳ chọn { containerId, imageId, zoomInBtnId, ... }
        constructor(containerEl) {
            var opts = (containerEl && containerEl.containerId) ? containerEl : null;
            this.opts = opts;
            this.$container = opts ? $('#' + opts.containerId) : $(containerEl);
            this.$img = opts ? $('#' + opts.imageId) : this.$container.find('.meter-viewer-img');
            this.scale = 1.0;
            this.rotation = 0;
            this.initEvents();
        }

        initEvents() {
            var self = this;
            var bind = function (selector, handler) {
                $(document).on('click', selector, function (e) {
                    e.preventDefault();
                    handler.call(self);
                });
            };
            if (this.opts) {
                bind('#' + this.opts.zoomInBtnId, this.zoomIn);
                bind('#' + this.opts.zoomOutBtnId, this.zoomOut);
                bind('#' + this.opts.rotateBtnId, this.rotate);
                bind('#' + this.opts.resetBtnId, this.reset);
                return;
            }
            this.$container.on('click', '.btn-zoom-in', function (e) { e.preventDefault(); self.zoomIn(); });
            this.$container.on('click', '.btn-zoom-out', function (e) { e.preventDefault(); self.zoomOut(); });
            this.$container.on('click', '.btn-rotate', function (e) { e.preventDefault(); self.rotate(); });
            this.$container.on('click', '.btn-reset', function (e) { e.preventDefault(); self.reset(); });
        }

        loadImage(url) {
            this.reset();
            if (!this.$img || !this.$img.length) {
                this.$img = this.$container.find('.meter-viewer-img');
            }
            this.$img.attr('src', url);
        }

        setImage(url) {
            this.reset();
            var $viewport = this.$container.find('.meter-viewer-viewport');
            this.$img = $viewport.find('.meter-viewer-img');
            if (this.$img.length === 0) {
                this.$img = $('<img>').addClass('meter-viewer-img img-fluid');
                $viewport.empty().append(this.$img);
            }
            this.$img.attr('src', url);
        }

        updateTransform() {
            if (this.$img && this.$img.length) {
                this.$img.css({
                    transform: 'scale(' + this.scale + ') rotate(' + this.rotation + 'deg)',
                    transition: 'transform 0.2s ease-in-out',
                    transformOrigin: 'center center'
                });
            }
        }

        zoomIn() {
            if (this.scale < 3.0) {
                this.scale = Math.min(3.0, Math.round((this.scale + 0.25) * 100) / 100);
                this.updateTransform();
            }
        }

        zoomOut() {
            if (this.scale > 0.5) {
                this.scale = Math.max(0.5, Math.round((this.scale - 0.25) * 100) / 100);
                this.updateTransform();
            }
        }

        rotate() {
            this.rotation = (this.rotation + 90) % 360;
            this.updateTransform();
        }

        reset() {
            this.scale = 1.0;
            this.rotation = 0;
            this.updateTransform();
        }
    }

    window.MeterImageViewer = MeterImageViewer;
    window.escapeHtml = escapeHtml;
})(window, window.jQuery || window.$);
