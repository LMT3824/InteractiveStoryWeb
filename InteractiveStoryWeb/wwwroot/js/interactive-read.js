// interactive-read.js - Xử lý điều hướng và tương tác đọc truyện

$(document).ready(function () {
    // Hàm hiển thị thông báo
    function showAlert(type, message) {
        $('#alert-container').empty();
        var $alert = $('<div class="alert alert-' + type + ' alert-dismissible fade show" role="alert">' +
            message + '<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button></div>');
        $('#alert-container').prepend($alert);
        setTimeout(function () { $alert.alert('close'); }, 3000);
    }

    // Xử lý submit form Lưu/Xóa khỏi thư viện
    $('.library-form').on('submit', function (e) {
        e.preventDefault();

        var $form = $(this);
        var action = $form.data('action');
        var storyId = $form.data('story-id');
        var token = $('input[name="__RequestVerificationToken"]').val();

        $.ajax({
            url: '/Story/' + action,
            type: 'POST',
            data: {
                storyId: storyId,
                __RequestVerificationToken: token
            },
            success: function (response) {
                if (response.success) {
                    var newAction = action === 'AddToLibrary' ? 'RemoveFromLibrary' : 'AddToLibrary';
                    var newText = action === 'AddToLibrary' ? 'Xóa khỏi thư viện' : 'Lưu vào thư viện';
                    $form.data('action', newAction);
                    $form.find('.library-btn').text(newText);

                    $('.library-form').each(function () {
                        $(this).data('action', newAction);
                        $(this).find('.library-btn').text(newText);
                    });

                    showAlert('success', response.message);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function (xhr, status, error) {
                var errorMessage = xhr.responseJSON && xhr.responseJSON.message
                    ? xhr.responseJSON.message
                    : 'Có lỗi xảy ra khi thực hiện thao tác.';
                showAlert('danger', errorMessage);
            }
        });
    });

    // Hàm xử lý chuyển chương
    function navigateChapter(chapterId, isNext) {
        console.log('Navigating chapter with ID:', chapterId, 'isNext:', isNext);
        $.ajax({
            url: isNext ? '/Segment/NextChapterJson' : '/Segment/PrevChapterJson',
            type: 'GET',
            data: { currentChapterId: chapterId },
            success: function (response) {
                console.log('Navigate chapter response:', response);
                if (response.success) {
                    updateSegment(response.data);
                } else {
                    showAlert('warning', response.message);
                }
            },
            error: function (xhr, status, error) {
                console.log('Navigate chapter error:', xhr, status, error);
                var errorMessage = xhr.responseJSON && xhr.responseJSON.message
                    ? xhr.responseJSON.message
                    : 'Có lỗi xảy ra khi chuyển chương. Status: ' + xhr.status + ', Error: ' + (xhr.statusText || error);
                showAlert('danger', errorMessage);
            }
        });
    }

    // Xử lý nút "Chương trước"
    $(document).on('click', '.prev-chapter', function (e) {
        e.preventDefault();
        var chapterId = $(this).data('current-chapter-id');
        navigateChapter(chapterId, false);
    });

    // Xử lý nút "Chương sau"
    $(document).on('click', '.next-chapter', function (e) {
        e.preventDefault();
        var chapterId = $(this).data('current-chapter-id');
        navigateChapter(chapterId, true);
    });

    // Xử lý lựa chọn
    $(document).on('click', '.choice-button', function (e) {
        e.preventDefault();
        var choiceId = $(this).data('choice-id');
        var token = $('input[name="__RequestVerificationToken"]').val();
        console.log('Sending AJAX with choiceId:', choiceId, 'Token:', token);

        $.ajax({
            url: '/Choice/ChooseJson',
            type: 'POST',
            contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
            data: $.param({
                id: choiceId,
                __RequestVerificationToken: token
            }),
            success: function (response) {
                console.log('AJAX Success:', response);
                if (response.success) {
                    updateSegment(response.data);
                } else {
                    showAlert('danger', response.message);
                }
            },
            error: function (xhr, status, error) {
                console.log('AJAX Error:', { status: xhr.status, statusText: xhr.statusText, response: xhr.responseJSON, error: error });
                var errorMessage = xhr.responseJSON && xhr.responseJSON.message
                    ? xhr.responseJSON.message
                    : 'Có lỗi xảy ra khi chọn lựa chọn. Status: ' + xhr.status + ', Error: ' + (xhr.statusText || error);
                showAlert('danger', errorMessage);
            }
        });
    });

    // Hàm cập nhật nội dung đoạn
    function updateSegment(data) {
        console.log('Updating segment with data:', data);
        if (!data || !data.chapterId) {
            showAlert('danger', 'Không thể cập nhật nội dung: Dữ liệu chương không hợp lệ.');
            return;
        }

        // Cập nhật tiêu đề truyện
        $('#chapter-header').html(`
            <h3 class="story-title">${data.storyTitle}</h3>
            <div class="author-info">
                <img src="${data.authorAvatarUrl}" alt="Avatar" class="author-avatar" />
                <a href="/Account/Profile/${data.authorId || ''}" title="Xem trang cá nhân">
                    ${data.authorUserName || 'Không xác định'}
                </a>
            </div>
        `);

        // Cập nhật tiêu đề chương
        $('#chapter-title').text(data.chapterTitle);

        // Cập nhật thời gian
        var metaHtml = `<p><span class="time-info">Thời gian đăng: ${data.createdAt}</span>`;
        if (data.updatedAt) {
            metaHtml += `<span class="separator">|</span><span class="time-info">Thời gian chỉnh sửa: ${data.updatedAt}</span>`;
        }
        metaHtml += `</p>`;
        $('#chapter-meta').html(metaHtml);

        // Cập nhật nút điều hướng
        $('.prev-chapter').data('current-chapter-id', data.chapterId);
        $('.next-chapter').data('current-chapter-id', data.chapterId);

        // Cập nhật nội dung đoạn
        var contentHtml = '';
        if (data.imageUrl && data.imagePosition === 'Top') {
            contentHtml += `<div class="text-center mb-3">
                <img src="${data.imageUrl}" class="img-fluid rounded shadow segment-image" alt="Ảnh minh họa" />
            </div>`;
        }
        contentHtml += `<div id="segment-text" data-segment-id="${data.segmentId}">${data.content}</div>`;
        if (data.imageUrl && data.imagePosition === 'Bottom') {
            contentHtml += `<div class="text-center mb-3">
                <img src="${data.imageUrl}" class="img-fluid rounded shadow segment-image" alt="Ảnh minh họa" />
            </div>`;
        }
        $('#segment-content .card-body').html(contentHtml);

        // Cập nhật danh sách lựa chọn
        var choicesHtml = '';
        if (data.choices && data.choices.length > 0) {
            choicesHtml = '<ul class="list-group mb-4">';
            data.choices.forEach(function (choice) {
                choicesHtml += `
                    <li class="list-group-item">
                        <button class="btn btn-outline-dark w-100 choice-button" data-choice-id="${choice.id}">
                            ${choice.choiceText}
                        </button>
                    </li>`;
            });
            choicesHtml += '</ul>';
        } else {
            choicesHtml = '<p class="text-muted">Không có lựa chọn nào khả dụng cho đoạn này.</p>';
        }
        $('#choices-container').html(choicesHtml);

        // Cập nhật trạng thái thư viện
        var libraryAction = data.isInLibrary ? 'RemoveFromLibrary' : 'AddToLibrary';
        var libraryText = data.isInLibrary ? 'Xóa khỏi thư viện' : 'Lưu vào thư viện';
        $('.library-form').each(function () {
            $(this).data('action', libraryAction);
            $(this).find('.library-btn').text(libraryText);
        });

        // Load highlights cho segment mới (CHỈ 1 LẦN)
        if (window.isAuthenticated && window.HighlightManager) {
            setTimeout(function () {
                $.ajax({
                    url: '/Highlight/GetHighlights',
                    type: 'GET',
                    data: { segmentId: data.segmentId },
                    success: function (response) {
                        console.log('Loaded highlights for segment:', response);
                        if (response.success) {
                            window.HighlightManager.reloadHighlights(response.highlights);
                        }
                    },
                    error: function (xhr) {
                        console.log('Error loading highlights:', xhr);
                    }
                });
            }, 300);
        }

        // Cuộn lên đầu trang
        window.scrollTo(0, 0);
    }
});