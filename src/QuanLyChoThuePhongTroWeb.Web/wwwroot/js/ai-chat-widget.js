// Widget trợ lý AI (Views/Shared/_AiChatWidget.cshtml). Endpoint và khóa lịch sử lấy từ data-* của #aiChatWidget.
document.addEventListener("DOMContentLoaded", function () {
    const fab = document.getElementById("btnAiOpenChat");
    const box = document.getElementById("aiChatBox");
    const closeBtn = document.getElementById("btnAiClose");
    const resetBtn = document.getElementById("btnAiReset");
    const form = document.getElementById("frmAiChat");
    const input = document.getElementById("txtAiMessage");
    const body = document.getElementById("aiChatBody");
    const typingIndicator = document.getElementById("aiTypingIndicator");
    const suggestionsEl = document.getElementById("aiSuggestions");
    const widget = document.getElementById("aiChatWidget");
    const endpoint = widget.dataset.endpoint;
    const storageKey = widget.dataset.storageKey;
    const MAX_MESSAGE_LENGTH = 1000;

    // Xóa lịch sử của khóa cũ hoặc của người dùng khác còn sót trong tab này
    Object.keys(sessionStorage).forEach(function (k) {
        if (k === "ai_chat_history" || (k.indexOf("ai_chat_history_") === 0 && k !== storageKey)) {
            sessionStorage.removeItem(k);
        }
    });

    // Tải lịch sử trò chuyện từ sessionStorage
    loadChatHistory();

    // Mở/đóng khung chat
    fab.addEventListener("click", function () {
        const isVisible = box.style.display === "flex";
        if (isVisible) {
            box.style.display = "none";
        } else {
            box.style.display = "flex";
            scrollToBottom();
            input.focus();
        }
    });

    closeBtn.addEventListener("click", function () {
        box.style.display = "none";
    });

    // Reset chat
    resetBtn.addEventListener("click", function () {
        sessionStorage.removeItem(storageKey);
        // Giữ lại lời chào đầu tiên, xóa các tin nhắn còn lại và hiện lại gợi ý
        const messages = body.querySelectorAll(".ai-message");
        messages.forEach((el, index) => {
            if (index > 0) el.remove();
        });
        showSuggestions(true);
    });

    // Submit câu hỏi
    form.addEventListener("submit", function (e) {
        e.preventDefault();
        const text = input.value.trim();
        if (!text) return;

        sendMessage(text);
    });

    // Click nút gợi ý nhanh
    document.querySelectorAll(".btn-ai-suggest").forEach(button => {
        button.addEventListener("click", function () {
            const text = this.getAttribute("data-prompt");
            sendMessage(text);
        });
    });

    function showSuggestions(show) {
        if (!suggestionsEl) return;
        suggestionsEl.classList.toggle("d-none", !show);
        if (show) {
            // Đưa gợi ý xuống cuối khung chat
            body.appendChild(suggestionsEl);
        }
    }

    function sendMessage(text) {
        if (text.length > MAX_MESSAGE_LENGTH) {
            appendMessage("model", "Câu hỏi tối đa 1.000 ký tự.");
            scrollToBottom();
            return;
        }

        showSuggestions(false);

        // Append tin nhắn của User
        appendMessage("user", text);
        input.value = "";
        scrollToBottom();

        // Hiển thị Typing Indicator
        typingIndicator.classList.remove("d-none");
        scrollToBottom();

        // Lấy lịch sử hiện tại
        const history = getSessionHistory().slice(-10);

        // Gọi API
        fetch(endpoint, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
            },
            body: JSON.stringify({
                message: text,
                history: history
            })
        })
        .then(res => res.json())
        .then(data => {
            typingIndicator.classList.add("d-none");
            if (data.success) {
                appendMessage("model", data.message);
                // Lưu lại lịch sử
                saveSessionHistory(text, data.message);
            } else {
                appendMessage("model", "⚠️ " + (data.message || "Đã xảy ra lỗi khi trao đổi với AI."));
            }
            scrollToBottom();
        })
        .catch(err => {
            typingIndicator.classList.add("d-none");
            appendMessage("model", "⚠️ Lỗi kết nối mạng: Không thể liên hệ với máy chủ AI.");
            scrollToBottom();
        });
    }

    function appendMessage(role, text) {
        const msgDiv = document.createElement("div");
        msgDiv.className = `ai-message ai-message-${role}`;

        const bubble = document.createElement("div");
        bubble.className = "ai-msg-bubble";
        if (role === "user") {
            // Tin nhắn của người dùng luôn là văn bản thuần
            bubble.textContent = text;
        } else {
            bubble.innerHTML = parseMarkdown(text);
        }

        msgDiv.appendChild(bubble);
        body.appendChild(msgDiv);
    }

    function scrollToBottom() {
        body.scrollTop = body.scrollHeight;
    }

    // Lấy lịch sử chat từ sessionStorage
    function getSessionHistory() {
        const histStr = sessionStorage.getItem(storageKey);
        return histStr ? JSON.parse(histStr) : [];
    }

    // Lưu lịch sử chat mới
    function saveSessionHistory(userMsg, aiMsg) {
        const hist = getSessionHistory();
        hist.push({ role: "user", message: userMsg });
        hist.push({ role: "model", message: aiMsg });
        // Giới hạn lịch sử lưu tối đa 16 tin nhắn để tránh quá tải token
        if (hist.length > 16) {
            hist.splice(0, 2);
        }
        sessionStorage.setItem(storageKey, JSON.stringify(hist));
    }

    // Tải lịch sử khi load trang
    function loadChatHistory() {
        const hist = getSessionHistory();
        if (hist && hist.length > 0) {
            // Lịch sử khôi phục cũng đi qua appendMessage (escape HTML trong parseMarkdown)
            hist.forEach(msg => {
                appendMessage(msg.role, msg.message);
            });
            showSuggestions(false);
            scrollToBottom();
        }
    }

    // Escape HTML để mọi văn bản (kể cả do AI trả về) không thể chèn thẻ
    function escapeHtml(text) {
        return String(text)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    // Hàm parse Markdown sang HTML đơn giản (escape trước, chỉ sinh các thẻ được hỗ trợ)
    function parseMarkdown(text) {
        if (!text) return "";

        // Xử lý xuống dòng
        let html = escapeHtml(text).replace(/\r\n/g, "\n").replace(/\n/g, "<br/>");

        // In đậm: **text**
        html = html.replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>");

        // Định dạng danh sách không thứ tự (ul/li)
        // Thay thế: * hoặc - ở đầu dòng
        html = html.replace(/(?:^|<br\/>)[*-]\s+(.*?)(?=<br\/>|$)/g, "<li>$1</li>");
        // Bao bọc các khối <li> bằng <ul>
        html = html.replace(/(<li>.*?<\/li>)/g, "<ul>$1</ul>");
        // Dọn dẹp các <ul> lân cận bị thừa
        html = html.replace(/<\/ul><br\/><ul>/g, "");
        html = html.replace(/<\/ul><ul>/g, "");

        // Xử lý Bảng biểu Markdown
        // Cú pháp: | Cột 1 | Cột 2 | \n |---|---| \n | Dữ liệu | Dữ liệu |
        if (html.includes("|")) {
            const lines = html.split("<br/>");
            let inTable = false;
            let tableHtml = "";
            let newLines = [];

            for (let i = 0; i < lines.length; i++) {
                const line = lines[i].trim();
                if (line.startsWith("|") && line.endsWith("|")) {
                    // Bỏ qua dòng gạch ngang phân cách tiêu đề |---|---|
                    if (line.includes("---") || line.includes("===")) {
                        continue;
                    }

                    const cells = line.split("|").slice(1, -1);
                    let rowHtml = "<tr>";

                    for (let c = 0; c < cells.length; c++) {
                        const cellText = cells[c].trim();
                        if (!inTable) {
                            // Dòng đầu tiên của bảng là Header
                            rowHtml += `<th>${cellText}</th>`;
                        } else {
                            rowHtml += `<td>${cellText}</td>`;
                        }
                    }
                    rowHtml += "</tr>";

                    if (!inTable) {
                        inTable = true;
                        tableHtml += "<table><thead>" + rowHtml + "</thead><tbody>";
                    } else {
                        tableHtml += rowHtml;
                    }
                } else {
                    if (inTable) {
                        inTable = false;
                        tableHtml += "</tbody></table>";
                        newLines.push(tableHtml);
                        tableHtml = "";
                    }
                    newLines.push(lines[i]);
                }
            }
            if (inTable) {
                tableHtml += "</tbody></table>";
                newLines.push(tableHtml);
            }
            html = newLines.join("<br/>");
        }

        return html;
    }
});
