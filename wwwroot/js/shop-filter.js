(() => {
    const catalog = document.getElementById('shopCatalog');
    const status = document.getElementById('shopFilterStatus');
    const searchForm = document.querySelector('.shop-hero-search');
    if (!catalog || !status) return;

    let activeRequest;

    function setStatus(message, isError = false) {
        status.textContent = message;
        status.classList.toggle('is-error', isError);
    }

    function closeMobileFilter() {
        const canvas = document.getElementById('shopFilterOffcanvas');
        if (!canvas || !window.bootstrap?.Offcanvas) return;
        window.bootstrap.Offcanvas.getInstance(canvas)?.hide();
    }

    function syncSearchInput(url) {
        const input = document.getElementById('shopSearch');
        if (input) input.value = url.searchParams.get('search') || '';
    }

    async function loadCatalog(target, historyMode = 'push') {
        const url = new URL(target, window.location.origin);
        if (url.origin !== window.location.origin) {
            window.location.assign(url.toString());
            return;
        }

        activeRequest?.abort();
        const request = new AbortController();
        activeRequest = request;
        closeMobileFilter();
        catalog.classList.add('is-loading');
        catalog.setAttribute('aria-busy', 'true');
        setStatus('Đang cập nhật sản phẩm…');

        try {
            const response = await fetch(url.toString(), {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html'
                },
                signal: request.signal
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const html = await response.text();
            if (activeRequest !== request) return;
            catalog.innerHTML = html;
            syncSearchInput(url);

            if (historyMode === 'push') window.history.pushState({}, '', url);
            if (historyMode === 'replace') window.history.replaceState({}, '', url);

            const resultText = catalog.querySelector('.shop-toolbar > div:first-child span')?.textContent?.trim();
            setStatus(resultText ? `Đã cập nhật. ${resultText}.` : 'Đã cập nhật sản phẩm.');
        } catch (error) {
            if (error.name !== 'AbortError') {
                setStatus('Không thể cập nhật sản phẩm. Vui lòng thử lại.', true);
            }
        } finally {
            if (activeRequest === request) {
                activeRequest = null;
                catalog.classList.remove('is-loading');
                catalog.removeAttribute('aria-busy');
            }
        }
    }

    function urlFromForm(form) {
        const url = new URL(form.action || window.location.href, window.location.origin);
        url.search = '';
        for (const [key, value] of new FormData(form).entries()) {
            const normalized = value.toString().trim();
            if (normalized) url.searchParams.set(key, normalized);
        }
        url.searchParams.delete('page');
        return url;
    }

    document.addEventListener('click', event => {
        if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest('[data-shop-filter-link], .shop-pagination a, .shop-empty-state a');
        if (!link || link.getAttribute('aria-disabled') === 'true' || !link.href) return;
        event.preventDefault();
        loadCatalog(link.href, 'push');
    });

    document.addEventListener('change', event => {
        if (!event.target.matches('[data-shop-sort]')) return;
        loadCatalog(event.target.value, 'push');
    });

    document.addEventListener('submit', event => {
        const form = event.target;
        if (form.matches('#shopPriceForm')) {
            event.preventDefault();
            loadCatalog(urlFromForm(form), 'push');
            return;
        }
        if (form === searchForm) {
            event.preventDefault();
            const url = new URL(window.location.href);
            const search = document.getElementById('shopSearch')?.value.trim();
            if (search) url.searchParams.set('search', search);
            else url.searchParams.delete('search');
            url.searchParams.delete('page');
            loadCatalog(url, 'push');
        }
    });

    document.addEventListener('input', event => {
        if (!event.target.matches('#rangeInput')) return;
        const output = document.getElementById('amount');
        if (output) output.textContent = Number(event.target.value).toLocaleString('vi-VN');
    });

    window.addEventListener('popstate', () => loadCatalog(window.location.href, 'none'));
})();
