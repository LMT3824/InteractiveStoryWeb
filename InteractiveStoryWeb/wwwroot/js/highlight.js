(function () {
    'use strict';

    let highlightMode = false;
    let currentSelection = null;
    let currentHighlightId = null;
    let selectedColor = 'yellow';
    let modalSelectedColor = 'yellow';
    let highlights = [];
    let originalContent = '';
    let invalidHighlights = []; // Lưu các highlight không tìm thấy

    $(document).ready(function () {
        if (!window.isAuthenticated) return;

        loadExistingHighlights();
        initializeEventListeners();
    });

    function loadExistingHighlights() {
        if (window.existingHighlights && window.existingHighlights.length > 0) {
            highlights = window.existingHighlights;

            // Lưu content gốc lần đầu
            const segmentText = document.getElementById('segment-text');
            if (segmentText && !originalContent) {
                originalContent = segmentText.innerHTML;
            }

            setTimeout(() => applyHighlightsToContent(), 100);
        }
    }

    function applyHighlightsToContent() {
        const segmentText = document.getElementById('segment-text');
        if (!segmentText) {
            console.log('segment-text not found');
            return;
        }

        if (!originalContent) {
            originalContent = segmentText.innerHTML;
        }

        if (highlights.length === 0) {
            console.log('No highlights to apply');
            segmentText.innerHTML = originalContent;
            return;
        }

        // Reset về HTML gốc
        segmentText.innerHTML = originalContent;

        // Reset invalid highlights
        invalidHighlights = [];

        console.log('Applying', highlights.length, 'highlights');

        // Validate từng highlight trước
        const fullText = segmentText.textContent || segmentText.innerText;
        const validHighlights = [];

        highlights.forEach(function (h) {
            const textAtOffset = fullText.substring(h.startOffset, h.endOffset);

            if (textAtOffset === h.highlightedText) {
                // Text khớp, giữ highlight
                validHighlights.push(h);
            } else {
                // Thử tìm bằng context
                let found = false;

                if (h.contextBefore && h.contextAfter) {
                    const searchText = h.contextBefore + h.highlightedText + h.contextAfter;
                    const index = fullText.indexOf(searchText);

                    if (index >= 0) {
                        // Update offset mới
                        h.startOffset = index + h.contextBefore.length;
                        h.endOffset = h.startOffset + h.highlightedText.length;
                        validHighlights.push(h);
                        found = true;
                    }
                }

                // Nếu vẫn không tìm thấy, thử tìm chỉ text
                if (!found) {
                    const textIndex = fullText.indexOf(h.highlightedText);
                    if (textIndex >= 0) {
                        // Kiểm tra xem có phải là occurrence đầu tiên không
                        // (tránh highlight nhầm chỗ)
                        const beforeContext = fullText.substring(Math.max(0, textIndex - 10), textIndex);

                        // So sánh với context cũ
                        if (h.contextBefore && h.contextBefore.endsWith(beforeContext)) {
                            h.startOffset = textIndex;
                            h.endOffset = textIndex + h.highlightedText.length;
                            validHighlights.push(h);
                            found = true;
                        }
                    }
                }

                if (!found) {
                    // Highlight không còn hợp lệ
                    invalidHighlights.push(h);
                    console.warn('Invalid highlight:', h.highlightedText);
                }
            }
        });

        // Apply chỉ valid highlights
        const sortedHighlights = [...validHighlights].sort((a, b) => b.startOffset - a.startOffset);

        sortedHighlights.forEach(function (h) {
            try {
                applyHighlightByOffset(segmentText, h);
            } catch (e) {
                console.error('Error applying highlight:', h, e);
            }
        });

        // Thông báo nếu có highlight không hợp lệ
        if (invalidHighlights.length > 0) {
            showInvalidHighlightsNotification();
        }

        console.log('Valid highlights applied:', validHighlights.length);
        console.log('Invalid highlights:', invalidHighlights.length);
    }

    // Thêm function thông báo
    function showInvalidHighlightsNotification() {
        if (invalidHighlights.length === 0) return;

        const message = `Có ${invalidHighlights.length} highlight không tìm thấy do nội dung đã thay đổi. Bạn có muốn xóa chúng không?`;

        // Tạo div thông báo
        const notificationDiv = $(`
        <div class="alert alert-warning alert-dismissible fade show" style="position: fixed; top: 70px; right: 20px; z-index: 9999; max-width: 400px;">
            <strong>Thông báo:</strong> ${message}
            <div class="mt-2">
                <button type="button" class="btn btn-sm btn-danger me-2" id="deleteInvalidHighlights">Xóa highlights lỗi</button>
                <button type="button" class="btn btn-sm btn-secondary" data-bs-dismiss="alert">Để sau</button>
            </div>
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `);

        $('body').append(notificationDiv);

        // Xử lý nút xóa
        $('#deleteInvalidHighlights').on('click', function () {
            deleteInvalidHighlights();
            notificationDiv.alert('close');
        });

        // Tự động ẩn sau 10 giây
        setTimeout(function () {
            notificationDiv.alert('close');
        }, 10000);
    }

    function deleteInvalidHighlights() {
        if (invalidHighlights.length === 0) return;

        const deletePromises = invalidHighlights.map(function (h) {
            return $.ajax({
                url: '/Highlight/Delete',
                type: 'POST',
                data: {
                    id: h.id,
                    __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
                }
            });
        });

        Promise.all(deletePromises).then(function () {
            // Xóa khỏi mảng highlights
            highlights = highlights.filter(function (h) {
                return !invalidHighlights.some(invalid => invalid.id === h.id);
            });

            // Reset invalid highlights
            invalidHighlights = [];

            // Apply lại
            applyHighlightsToContent();

            showAlert('success', 'Đã xóa các highlight không hợp lệ.');
        }).catch(function () {
            showAlert('danger', 'Có lỗi khi xóa highlights.');
        });
    }

    function applyHighlightByOffset(container, highlightData) {
        let currentOffset = 0;
        let startContainer = null;
        let startOffset = 0;
        let endContainer = null;
        let endOffset = 0;
        let found = false;

        // Hàm đệ quy duyệt tất cả text nodes
        function traverseTextNodes(node) {
            if (found) return;

            if (node.nodeType === Node.TEXT_NODE) {
                const textLength = node.textContent.length;
                const nodeStart = currentOffset;
                const nodeEnd = currentOffset + textLength;

                // Kiểm tra start position
                if (!startContainer && highlightData.startOffset >= nodeStart && highlightData.startOffset < nodeEnd) {
                    startContainer = node;
                    startOffset = highlightData.startOffset - nodeStart;
                }

                // Kiểm tra end position
                if (!endContainer && highlightData.endOffset > nodeStart && highlightData.endOffset <= nodeEnd) {
                    endContainer = node;
                    endOffset = highlightData.endOffset - nodeStart;
                    found = true;
                    return;
                }

                currentOffset += textLength;
            } else if (node.nodeType === Node.ELEMENT_NODE) {
                // Bỏ qua các mark tags đã có
                if (node.tagName === 'MARK') {
                    currentOffset += node.textContent.length;
                    return;
                }

                // Duyệt các child nodes
                for (let child of node.childNodes) {
                    traverseTextNodes(child);
                    if (found) return;
                }
            }
        }

        traverseTextNodes(container);

        if (!startContainer || !endContainer) {
            console.warn('Could not find nodes for highlight:', highlightData);
            return;
        }

        // Tạo range
        const range = document.createRange();

        try {
            range.setStart(startContainer, startOffset);
            range.setEnd(endContainer, endOffset);

            // Tạo mark element
            const mark = document.createElement('mark');
            mark.className = 'user-highlight highlight-' + highlightData.color;
            mark.setAttribute('data-highlight-id', highlightData.id);
            mark.setAttribute('data-note', highlightData.note || '');
            mark.setAttribute('title', highlightData.note ? 'Click để xem ghi chú' : '');
            mark.style.cssText = 'background-color: ' + getColorValue(highlightData.color) + '; cursor: pointer; padding: 2px 0; border-radius: 2px;';

            // Wrap content
            if (startContainer === endContainer) {
                // Cùng text node - đơn giản
                const text = startContainer.textContent;
                const before = text.substring(0, startOffset);
                const highlighted = text.substring(startOffset, endOffset);
                const after = text.substring(endOffset);

                mark.textContent = highlighted;

                const beforeNode = document.createTextNode(before);
                const afterNode = document.createTextNode(after);

                const parent = startContainer.parentNode;
                parent.replaceChild(afterNode, startContainer);
                parent.insertBefore(mark, afterNode);
                parent.insertBefore(beforeNode, mark);
            } else {
                // Cross nhiều nodes - dùng surroundContents
                const contents = range.extractContents();
                mark.appendChild(contents);
                range.insertNode(mark);
            }
        } catch (e) {
            console.error('Error creating range:', e, highlightData);
        }
    }

    // Helper function: Escape regex special characters
    function escapeRegex(str) {
        return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    // Helper function: Create mark tag
    function createMarkTag(highlight) {
        return '<mark class="user-highlight highlight-' + highlight.color + '" ' +
            'data-highlight-id="' + highlight.id + '" ' +
            'data-note="' + (highlight.note || '').replace(/"/g, '&quot;') + '" ' +
            'title="' + (highlight.note ? 'Click để xem ghi chú' : '') + '" ' +
            'style="background-color: ' + getColorValue(highlight.color) + '; cursor: pointer; padding: 2px 0; border-radius: 2px;">$1</mark>';
    }

    // Helper function: Escape regex special characters
    function escapeRegex(str) {
        return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    // Helper function: Create mark tag
    function createMarkTag(highlight) {
        return '<mark class="user-highlight highlight-' + highlight.color + '" ' +
            'data-highlight-id="' + highlight.id + '" ' +
            'data-note="' + (highlight.note || '').replace(/"/g, '&quot;') + '" ' +
            'title="' + (highlight.note ? 'Click để xem ghi chú' : '') + '" ' +
            'style="background-color: ' + getColorValue(highlight.color) + '; cursor: pointer; padding: 2px 0; border-radius: 2px;">$1</mark>';
    }

    function getColorValue(colorName) {
        const colors = {
            'yellow': 'rgba(255, 255, 0, 0.4)',
            'green': 'rgba(144, 238, 144, 0.4)',
            'blue': 'rgba(173, 216, 230, 0.4)',
            'pink': 'rgba(255, 182, 193, 0.4)',
            'orange': 'rgba(255, 165, 0, 0.4)'
        };
        return colors[colorName] || colors['yellow'];
    }

    function initializeEventListeners() {
        // Toggle highlight mode
        // Toggle highlight mode với floating button
        $(document).on('click', '#floating-highlight-btn', function () {
            highlightMode = !highlightMode;

            if (highlightMode) {
                $(this).addClass('active');
                showAlert('info', 'Chế độ Highlight đã BẬT. Chọn text để highlight.');
            } else {
                $(this).removeClass('active');
                $('#highlight-toolbar').hide();
                showAlert('info', 'Chế độ Highlight đã TẮT.');
            }

            console.log('Highlight mode:', highlightMode);
        });

        // Mở modal quản lý highlights
        $(document).on('click', '#floating-manage-btn', function () {
            openManageHighlightsModal();
        });

        // Chọn tất cả highlights
        $(document).on('click', '#selectAllHighlights', function () {
            selectAllHighlights();
        });

        // Xóa highlights đã chọn
        $(document).on('click', '#deleteSelectedHighlights', function () {
            deleteSelectedHighlights();
        });

        // Xử lý selection
        $(document).on('mouseup touchend', '#segment-text', function (e) {
            if (!highlightMode) return;

            setTimeout(function () {
                const selection = window.getSelection();
                const selectedText = selection.toString().trim();

                // Kiểm tra không cho highlight nhiều dòng
                if (selectedText.includes("\n") || selectedText.includes("\r")) {
                    showAlert("warning", "Chỉ có thể highlight trong một đoạn (không qua nhiều dòng).");
                    $("#highlight-toolbar").hide();
                    return;
                }
                if (selectedText.length > 0 && selectedText.length <= 500) {
                    currentSelection = {
                        text: selectedText,
                        range: selection.getRangeAt(0).cloneRange() // LƯU LẠI RANGE
                    };
                    showHighlightToolbar(e);
                } else if (selectedText.length > 500) {
                    showAlert('warning', 'Vui lòng chọn đoạn text ngắn hơn (tối đa 500 ký tự).');
                    $('#highlight-toolbar').hide();
                } else {
                    $('#highlight-toolbar').hide();
                }
            }, 10);
        });

        // Click vào highlight
        $(document).on('click', '.user-highlight', function (e) {
            e.stopPropagation();
            currentHighlightId = $(this).data('highlight-id');
            showHighlightTooltip(e, $(this).data('note'));
        });

        // Chọn màu
        // Chọn màu trên toolbar (tạo highlight NGAY không cần modal)
        $('.color-btn').on('click', function () {
            selectedColor = $(this).data('color');
            modalSelectedColor = selectedColor; // Sync với modal
            $('.color-btn').removeClass('active');
            $(this).addClass('active');

            // Tạo highlight ngay với màu đã chọn
            createHighlight(''); // Note rỗng
        });

        // Chọn màu trong modal
        $(document).on('click', '.modal-color-btn', function () {
            modalSelectedColor = $(this).data('color');
            $('.modal-color-btn').removeClass('active');
            $(this).addClass('active');
        });

        // Thêm note
        $('#add-note-btn').on('click', function () {
            modalSelectedColor = selectedColor; // Dùng màu đang chọn ở toolbar
            $('.modal-color-btn').removeClass('active');
            $('.modal-color-btn[data-color="' + modalSelectedColor + '"]').addClass('active');
            $('#noteModal').modal('show');
        });

        // Lưu note
        $('#saveNoteBtn').on('click', function () {
            createHighlight($('#noteText').val().trim());
            $('#noteModal').modal('hide');
            $('#noteText').val('');
        });

        // Sửa note
        $(document).on('click', '.edit-note-btn', function () {
            const highlight = highlights.find(function (h) { return h.id === currentHighlightId; });
            if (highlight) {
                $('#noteText').val(highlight.note || '');
                $('#highlight-tooltip').hide();

                // Set màu hiện tại
                modalSelectedColor = highlight.color;
                $('.modal-color-btn').removeClass('active');
                $('.modal-color-btn[data-color="' + modalSelectedColor + '"]').addClass('active');

                // Đổi title modal
                $('#noteModal').find('.modal-title').text('Sửa ghi chú');

                // Unbind sự kiện cũ và bind sự kiện mới
                $('#saveNoteBtn').off('click').on('click', function () {
                    const note = $('#noteText').val().trim();
                    updateHighlight(currentHighlightId, modalSelectedColor, note); // THAY ĐỔI: dùng modalSelectedColor
                    $('#noteModal').modal('hide');
                    $('#noteText').val('');
                    // Đổi lại title về mặc định
                    $('#noteModal').find('.modal-title').text('Thêm ghi chú');
                });

                $('#noteModal').modal('show');
            }
        });

        // Reset modal khi đóng
        $('#noteModal').on('hidden.bs.modal', function () {
            $('#noteText').val('');
            $('#noteModal').find('.modal-title').text('Thêm ghi chú');
            modalSelectedColor = selectedColor; // Reset về màu mặc định
            $('.modal-color-btn').removeClass('active');
            $('.modal-color-btn[data-color="' + modalSelectedColor + '"]').addClass('active');

            // Bind lại sự kiện mặc định cho nút Save
            $('#saveNoteBtn').off('click').on('click', function () {
                createHighlight($('#noteText').val().trim());
                $('#noteModal').modal('hide');
            });
        });

        // Xóa highlight
        $(document).on('click', '.delete-highlight-btn', function () {
            if (confirm('Bạn có chắc muốn xóa highlight này?')) {
                deleteHighlight(currentHighlightId);
            }
        });

        // Ẩn toolbar/tooltip
        $(document).on('click', function (e) {
            if (!$(e.target).closest('#highlight-toolbar, .user-highlight').length) {
                $('#highlight-toolbar, #highlight-tooltip').hide();
            }
        });
    }

    function showHighlightToolbar(event) {
        const toolbar = $('#highlight-toolbar');
        let x = event.pageX;
        let y = event.pageY - 60;

        const toolbarWidth = 350;
        const windowWidth = $(window).width();

        if (x + toolbarWidth > windowWidth) {
            x = windowWidth - toolbarWidth - 20;
        }

        if (y < 0) {
            y = event.pageY + 20;
        }

        toolbar.css({
            left: x + 'px',
            top: y + 'px',
            display: 'block'
        });

        $('.color-btn').removeClass('active');
        $('.color-btn[data-color="' + selectedColor + '"]').addClass('active');
    }

    function showHighlightTooltip(event, note) {
        const content = note ? '<strong>Ghi chú:</strong><br>' + note : '<em>Chưa có ghi chú</em>';
        $('#highlight-tooltip').find('.tooltip-content').html(content);
        $('#highlight-tooltip').css({
            left: event.pageX + 'px',
            top: (event.pageY + 20) + 'px',
            display: 'block'
        });
        $('#highlight-toolbar').hide();
    }

    function createHighlight(note) {
        if (!currentSelection) return;

        const segmentId = $('#segment-text').data('segment-id');
        if (!segmentId) {
            showAlert('danger', 'Không xác định được segment ID.');
            return;
        }

        const segmentText = document.getElementById('segment-text');
        const selectedText = currentSelection.text;

        const range = currentSelection.range;
        if (!range) {
            showAlert('danger', 'Không thể xác định vị trí text.');
            return;
        }

        // Tính offset từ đầu segment
        const preRange = document.createRange();
        preRange.selectNodeContents(segmentText);
        preRange.setEnd(range.startContainer, range.startOffset);
        const startOffset = preRange.toString().length;
        const endOffset = startOffset + selectedText.length;

        // Lấy toàn bộ text
        const fullText = segmentText.textContent || segmentText.innerText;

        // Lấy context
        const contextBefore = fullText.substring(Math.max(0, startOffset - 50), startOffset);
        const contextAfter = fullText.substring(endOffset, Math.min(fullText.length, endOffset + 50));

        console.log('Selection info:', {
            text: selectedText,
            startOffset: startOffset,
            endOffset: endOffset,
            contextBefore: contextBefore,
            contextAfter: contextAfter
        });

        const colorToUse = note === '' ? selectedColor : modalSelectedColor;

        const data = {
            chapterSegmentId: segmentId,
            highlightedText: selectedText,
            contextBefore: contextBefore,
            contextAfter: contextAfter,
            startOffset: startOffset,    // LƯU OFFSET
            endOffset: endOffset,         // LƯU OFFSET
            color: colorToUse,
            note: note || ''
        };

        $.ajax({
            url: '/Highlight/Create',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            headers: {
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            },
            success: function (response) {
                if (response.success) {
                    highlights.push(response.highlight);
                    applyHighlightsToContent();
                    $('#highlight-toolbar').hide();
                    window.getSelection().removeAllRanges();
                    showAlert('success', response.message);
                    if (typeof updateHighlightCount === 'function') {
                        updateHighlightCount();
                    }
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function (xhr) {
                const msg = xhr.responseJSON && xhr.responseJSON.message
                    ? xhr.responseJSON.message
                    : 'Có lỗi xảy ra khi tạo highlight.';
                showAlert('danger', msg);
            }
        });
    }

    function updateHighlight(highlightId, color, note) {
        const data = {
            id: highlightId,
            color: color,
            note: note
        };

        const token = $('input[name="__RequestVerificationToken"]').val();

        $.ajax({
            url: '/Highlight/Update',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            headers: {
                'RequestVerificationToken': token
            },
            success: function (response) {
                if (response.success) {
                    showAlert('success', response.message);

                    // Cập nhật trong mảng highlights
                    const index = highlights.findIndex(function (h) { return h.id === highlightId; });
                    if (index !== -1) {
                        highlights[index].color = color;
                        highlights[index].note = note;
                    }

                    // Apply lại highlights (sẽ reset về gốc và apply lại tất cả)
                    applyHighlightsToContent();
                    $('#highlight-tooltip').hide();
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function (xhr) {
                const errorMsg = xhr.responseJSON && xhr.responseJSON.message
                    ? xhr.responseJSON.message
                    : 'Có lỗi xảy ra khi cập nhật highlight.';
                showAlert('danger', errorMsg);
            }
        });
    }

    function deleteHighlight(id) {
        $.ajax({
            url: '/Highlight/Delete',
            type: 'POST',
            data: {
                id: id,
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
            },
            success: function (response) {
                if (response.success) {
                    // Xóa khỏi mảng
                    highlights = highlights.filter(function (h) { return h.id !== id; });

                    // Apply lại highlights (sẽ reset về gốc)
                    applyHighlightsToContent();
                    $('#highlight-tooltip').hide();

                    showAlert('success', response.message);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function () {
                showAlert('danger', 'Có lỗi xảy ra khi xóa highlight.');
            }
        });
    }

    function showAlert(type, message) {
        const alert = $('<div class="alert alert-' + type + ' alert-dismissible fade show">' +
            message + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
        $('#alert-container').html(alert);
        setTimeout(function () { alert.alert('close'); }, 3000);
    }

    // ========== QUẢN LÝ HIGHLIGHTS ==========

    function openManageHighlightsModal() {
        console.log('Opening manage highlights modal');
        console.log('Total highlights:', highlights.length);

        const modal = new bootstrap.Modal(document.getElementById('manageHighlightsModal'));
        loadHighlightsForManagement();
        modal.show();
    }

    function loadHighlightsForManagement() {
        const listContainer = $('#highlights-list');
        const noHighlightsMsg = $('#no-highlights-message');
        const countText = $('#highlight-count-text');

        listContainer.empty();

        console.log('Loading highlights for management:', highlights.length);

        if (highlights.length === 0) {
            noHighlightsMsg.show();
            listContainer.hide();
            countText.text('Tổng: 0 highlights');
            $('#deleteSelectedHighlights').prop('disabled', true);
            $('#selected-count-text').hide();
            return;
        }

        noHighlightsMsg.hide();
        listContainer.show();
        countText.text(`Tổng: ${highlights.length} highlight${highlights.length > 1 ? 's' : ''}`);

        // Render từng highlight
        highlights.forEach(function (h) {
            const highlightItem = $(`
                <div class="highlight-item" data-highlight-id="${h.id}">
                    <div class="highlight-checkbox">
                        <input type="checkbox" class="form-check-input highlight-select-cb" data-id="${h.id}">
                    </div>
                    <div class="highlight-content">
                        <div class="highlight-text">
                            <mark class="highlight-${h.color}" style="background-color: ${getColorValue(h.color)}; padding: 2px 4px; border-radius: 2px;">
                                ${escapeHtml(h.highlightedText)}
                            </mark>
                        </div>
                        ${h.note ? `<div class="highlight-note"><small><strong>Ghi chú:</strong> ${escapeHtml(h.note)}</small></div>` : ''}
                    </div>
                </div>
            `);

            listContainer.append(highlightItem);
        });

        console.log('Rendered highlight items:', $('.highlight-item').length);

        // Reset button states
        updateManageButtonStates();

        // Gắn event cho checkboxes (sử dụng event delegation)
        listContainer.off('change', '.highlight-select-cb').on('change', '.highlight-select-cb', function () {
            console.log('Checkbox changed');
            updateManageButtonStates();
        });
    }

    function selectAllHighlights() {
        console.log('selectAllHighlights called');
        const allCheckboxes = $('.highlight-select-cb');
        console.log('Found checkboxes:', allCheckboxes.length);

        if (allCheckboxes.length === 0) {
            console.warn('No checkboxes found!');
            return;
        }

        const allChecked = allCheckboxes.length > 0 && allCheckboxes.length === allCheckboxes.filter(':checked').length;

        allCheckboxes.prop('checked', !allChecked);
        updateManageButtonStates();
    }

    function updateManageButtonStates() {
        const selectedCount = $('.highlight-select-cb:checked').length;
        const totalCount = $('.highlight-select-cb').length;
        const selectAllBtn = $('#selectAllHighlights');
        const deleteBtn = $('#deleteSelectedHighlights');
        const selectedCountText = $('#selected-count-text');

        console.log('updateManageButtonStates - Selected:', selectedCount, 'Total:', totalCount);

        if (selectedCount > 0) {
            deleteBtn.prop('disabled', false);
            selectedCountText.text(`Đã chọn: ${selectedCount}`).show();

            // Update text của nút chọn tất cả
            if (selectedCount === totalCount) {
                selectAllBtn.text('Bỏ chọn tất cả');
            } else {
                selectAllBtn.text('Chọn tất cả');
            }
        } else {
            deleteBtn.prop('disabled', true);
            selectedCountText.hide();
            selectAllBtn.text('Chọn tất cả');
        }
    }

    function deleteSelectedHighlights() {
        console.log('deleteSelectedHighlights called');

        const selectedIds = $('.highlight-select-cb:checked').map(function () {
            return $(this).data('id');
        }).get();

        console.log('Selected IDs:', selectedIds);

        if (selectedIds.length === 0) {
            showAlert('warning', 'Vui lòng chọn ít nhất một highlight để xóa.');
            return;
        }

        if (!confirm(`Bạn có chắc muốn xóa ${selectedIds.length} highlight${selectedIds.length > 1 ? 's' : ''} đã chọn?`)) {
            return;
        }

        const deletePromises = selectedIds.map(function (id) {
            return $.ajax({
                url: '/Highlight/Delete',
                type: 'POST',
                data: {
                    id: id,
                    __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
                }
            });
        });

        Promise.all(deletePromises).then(function () {
            // Xóa khỏi mảng highlights
            highlights = highlights.filter(function (h) {
                return !selectedIds.includes(h.id);
            });

            // Apply lại highlights
            applyHighlightsToContent();

            // Reload danh sách trong modal
            loadHighlightsForManagement();

            showAlert('success', `Đã xóa ${selectedIds.length} highlight${selectedIds.length > 1 ? 's' : ''} thành công!`);
        }).catch(function () {
            showAlert('danger', 'Có lỗi xảy ra khi xóa highlights.');
        });
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Export functions
    window.HighlightManager = {
        reloadHighlights: function (newHighlights) {
            console.log('Reloading highlights:', newHighlights);
            highlights = newHighlights;

            // Reset originalContent để lưu content mới
            originalContent = '';

            // Đợi DOM render xong mới apply
            setTimeout(function () {
                const segmentText = document.getElementById('segment-text');
                if (segmentText) {
                    originalContent = segmentText.innerHTML;
                    applyHighlightsToContent();
                }
            }, 200);
        }
    };

})();