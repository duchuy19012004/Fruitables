/**
 * Order History Filter - AJAX Module
 * Xử lý lọc đơn hàng real-time không cần tải lại trang
 */
const OrderHistoryFilter = {
    // Configuration
    config: {
        debounceDelay: 300,
        ajaxTimeout: 10000,
        containerSelector: '#orderListContainer',
        contentSelector: '#orderListContent',
        loadingSelector: '#loadingOverlay',
        toastContainerSelector: '#toastContainer',
        filterUrl: '/OrderHistory/Filter'
    },

    // State
    state: {
        status: '',
        searchTerm: '',
        page: 1,
        pageSize: 10,
        debounceTimer: null,
        abortController: null,
        lastRequest: null
    },

    /**
     * Khởi tạo module
     */
    init: function() {
        // Lấy giá trị ban đầu từ DOM
        this.state.status = document.getElementById('statusInput')?.value || '';
        this.state.searchTerm = document.getElementById('searchInput')?.value || '';
        this.state.page = parseInt(document.getElementById('pageInput')?.value) || 1;
        this.state.pageSize = parseInt(document.getElementById('pageSizeSelect')?.value) || 10;

        // Bind event listeners
        this.bindTabEvents();
        this.bindSearchEvents();
        this.bindPageSizeEvents();
        this.bindPaginationEvents();
        this.bindPopStateEvent();
    },

    /**
     * 6.2: Tab click handling
     */
    bindTabEvents: function() {
        var self = this;
        document.querySelectorAll('.status-tab').forEach(function(tab) {
            tab.addEventListener('click', function(e) {
                e.preventDefault();
                var status = this.dataset.status || '';
                self.handleTabClick(status);
            });
        });
    },

    handleTabClick: function(status) {
        // Update active state ngay lập tức
        document.querySelectorAll('.status-tab').forEach(function(tab) {
            tab.classList.remove('active');
            if (tab.dataset.status === status) {
                tab.classList.add('active');
            }
        });

        // Update state và fetch
        this.state.status = status;
        this.state.page = 1; // Reset về trang 1 khi đổi tab
        document.getElementById('statusInput').value = status;
        
        this.fetchOrders();
    },

    /**
     * 6.3: Search với debounce 300ms
     */
    bindSearchEvents: function() {
        var self = this;
        var searchInput = document.getElementById('searchInput');
        var searchSpinner = document.getElementById('searchSpinner');

        if (searchInput) {
            searchInput.addEventListener('input', function() {
                var term = this.value;
                
                // Hiển thị spinner
                if (searchSpinner) {
                    searchSpinner.classList.remove('d-none');
                }

                // Clear timer cũ
                if (self.state.debounceTimer) {
                    clearTimeout(self.state.debounceTimer);
                }

                // Debounce 300ms
                self.state.debounceTimer = setTimeout(function() {
                    self.handleSearch(term);
                }, self.config.debounceDelay);
            });

            // Handle Enter key
            searchInput.addEventListener('keypress', function(e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    if (self.state.debounceTimer) {
                        clearTimeout(self.state.debounceTimer);
                    }
                    self.handleSearch(this.value);
                }
            });
        }
    },

    handleSearch: function(term) {
        this.state.searchTerm = term;
        this.state.page = 1; // Reset về trang 1 khi search
        this.fetchOrders();
    },

    /**
     * 6.4: Page size và pagination handling
     */
    bindPageSizeEvents: function() {
        var self = this;
        var pageSizeSelect = document.getElementById('pageSizeSelect');

        if (pageSizeSelect) {
            pageSizeSelect.addEventListener('change', function() {
                self.handlePageSizeChange(parseInt(this.value));
            });
        }
    },

    handlePageSizeChange: function(size) {
        this.state.pageSize = size;
        this.state.page = 1; // Reset về trang 1 khi đổi page size
        this.fetchOrders();
    },

    bindPaginationEvents: function() {
        var self = this;
        // Use event delegation vì pagination sẽ được replace bởi AJAX
        document.addEventListener('click', function(e) {
            var pageLink = e.target.closest('.page-link[data-page]');
            if (pageLink) {
                e.preventDefault();
                var page = parseInt(pageLink.dataset.page);
                if (page && page > 0) {
                    self.handlePageChange(page);
                }
            }
        });
    },

    handlePageChange: function(page) {
        this.state.page = page;
        this.fetchOrders();
    },

    /**
     * 6.5: URL state management
     */
    bindPopStateEvent: function() {
        var self = this;
        window.addEventListener('popstate', function(e) {
            if (e.state) {
                self.state.status = e.state.status || '';
                self.state.searchTerm = e.state.searchTerm || '';
                self.state.page = e.state.page || 1;
                self.state.pageSize = e.state.pageSize || 10;

                // Update UI elements
                self.updateUIFromState();
                
                // Fetch without pushing state
                self.fetchOrders(false);
            }
        });
    },

    updateUIFromState: function() {
        // Update tabs
        document.querySelectorAll('.status-tab').forEach(function(tab) {
            tab.classList.remove('active');
            if (tab.dataset.status === this.state.status) {
                tab.classList.add('active');
            }
        }.bind(this));

        // Update search input
        var searchInput = document.getElementById('searchInput');
        if (searchInput) {
            searchInput.value = this.state.searchTerm;
        }

        // Update page size select
        var pageSizeSelect = document.getElementById('pageSizeSelect');
        if (pageSizeSelect) {
            pageSizeSelect.value = this.state.pageSize;
        }

        // Update hidden inputs
        document.getElementById('statusInput').value = this.state.status;
        document.getElementById('pageInput').value = this.state.page;
    },

    updateURL: function() {
        var params = new URLSearchParams();
        
        if (this.state.status) {
            params.set('Status', this.state.status);
        }
        if (this.state.searchTerm) {
            params.set('SearchTerm', this.state.searchTerm);
        }
        if (this.state.page > 1) {
            params.set('Page', this.state.page);
        }
        if (this.state.pageSize !== 10) {
            params.set('PageSize', this.state.pageSize);
        }

        var url = window.location.pathname;
        var queryString = params.toString();
        if (queryString) {
            url += '?' + queryString;
        }

        var stateObj = {
            status: this.state.status,
            searchTerm: this.state.searchTerm,
            page: this.state.page,
            pageSize: this.state.pageSize
        };

        history.pushState(stateObj, '', url);
    },

    /**
     * 6.1: Core functionality - fetchOrders
     */
    fetchOrders: function(pushState) {
        var self = this;
        pushState = pushState !== false; // Default true

        // Cancel previous request nếu có
        if (this.state.abortController) {
            this.state.abortController.abort();
        }

        // Tạo AbortController mới
        this.state.abortController = new AbortController();

        // Build query params
        var params = new URLSearchParams();
        if (this.state.status) {
            params.set('Status', this.state.status);
        }
        if (this.state.searchTerm) {
            params.set('SearchTerm', this.state.searchTerm);
        }
        params.set('Page', this.state.page);
        params.set('PageSize', this.state.pageSize);

        // Lưu request cuối để retry
        this.state.lastRequest = {
            status: this.state.status,
            searchTerm: this.state.searchTerm,
            page: this.state.page,
            pageSize: this.state.pageSize
        };

        // Show loading
        this.showLoading();

        // Update URL nếu cần
        if (pushState) {
            this.updateURL();
        }

        // Timeout handler
        var timeoutId = setTimeout(function() {
            self.state.abortController.abort();
            self.hideLoading();
            self.showError('Yêu cầu quá lâu. Vui lòng thử lại.');
        }, this.config.ajaxTimeout);

        // Fetch request
        fetch(this.config.filterUrl + '?' + params.toString(), {
            method: 'GET',
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            signal: this.state.abortController.signal
        })
        .then(function(response) {
            clearTimeout(timeoutId);

            // 6.6: Handle 401 redirect to login
            if (response.status === 401) {
                window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search);
                return null;
            }

            if (!response.ok) {
                throw new Error('Server error: ' + response.status);
            }

            return response.text();
        })
        .then(function(html) {
            if (html !== null) {
                self.updateUI(html);
            }
        })
        .catch(function(error) {
            clearTimeout(timeoutId);

            // Ignore abort errors
            if (error.name === 'AbortError') {
                return;
            }

            self.hideLoading();

            // 6.6: Error handling
            if (!navigator.onLine) {
                self.showError('Không thể kết nối. Vui lòng kiểm tra mạng.');
            } else {
                self.showError('Có lỗi xảy ra. Vui lòng thử lại sau.');
            }
        });
    },

    /**
     * 6.1: Core functionality - updateUI
     */
    updateUI: function(html) {
        var content = document.querySelector(this.config.contentSelector);
        if (content) {
            content.innerHTML = html;
        }

        // Hide loading và search spinner
        this.hideLoading();
        var searchSpinner = document.getElementById('searchSpinner');
        if (searchSpinner) {
            searchSpinner.classList.add('d-none');
        }

        // Update hidden page input
        document.getElementById('pageInput').value = this.state.page;
    },

    /**
     * Loading indicator
     */
    showLoading: function() {
        var overlay = document.querySelector(this.config.loadingSelector);
        if (overlay) {
            overlay.classList.remove('d-none');
        }
    },

    hideLoading: function() {
        var overlay = document.querySelector(this.config.loadingSelector);
        if (overlay) {
            overlay.classList.add('d-none');
        }
    },

    /**
     * 6.6: Error handling - Toast notification
     */
    showError: function(message) {
        var container = document.querySelector(this.config.toastContainerSelector);
        if (!container) return;

        var toastId = 'toast-' + Date.now();
        var toastHtml = 
            '<div class="toast show" role="alert" id="' + toastId + '">' +
                '<div class="toast-header bg-danger text-white">' +
                    '<i class="fa fa-exclamation-circle me-2"></i>' +
                    '<strong class="me-auto">Lỗi</strong>' +
                    '<button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>' +
                '</div>' +
                '<div class="toast-body">' +
                    '<div class="d-flex justify-content-between align-items-center">' +
                        '<span>' + this.escapeHtml(message) + '</span>' +
                        '<button type="button" class="btn btn-sm btn-outline-danger ms-2" onclick="OrderHistoryFilter.retry()">' +
                            '<i class="fa fa-refresh me-1"></i>Thử lại' +
                        '</button>' +
                    '</div>' +
                '</div>' +
            '</div>';

        container.insertAdjacentHTML('beforeend', toastHtml);

        // Auto hide after 10 seconds
        setTimeout(function() {
            var toast = document.getElementById(toastId);
            if (toast) {
                var bsToast = new bootstrap.Toast(toast);
                bsToast.hide();
                setTimeout(function() {
                    toast.remove();
                }, 500);
            }
        }, 10000);
    },

    /**
     * 6.6: Retry functionality
     */
    retry: function() {
        if (this.state.lastRequest) {
            this.state.status = this.state.lastRequest.status;
            this.state.searchTerm = this.state.lastRequest.searchTerm;
            this.state.page = this.state.lastRequest.page;
            this.state.pageSize = this.state.lastRequest.pageSize;
            this.fetchOrders(false);
        }
    },

    /**
     * Helper: Escape HTML để tránh XSS
     */
    escapeHtml: function(text) {
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
};

// Initialize khi DOM ready
document.addEventListener('DOMContentLoaded', function() {
    OrderHistoryFilter.init();
});
