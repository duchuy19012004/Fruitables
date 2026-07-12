// wwwroot/js/search-suggest.js
(function () {
    'use strict';

    if (window.__fruitablesSearchSuggestInit) return;
    window.__fruitablesSearchSuggestInit = true;

    var DEBOUNCE_MS = 250;
    var MIN_LEN = 2;

    function escapeHtml(text) {
        var d = document.createElement('div');
        d.textContent = text == null ? '' : String(text);
        return d.innerHTML;
    }

    function formatPrice(n) {
        try {
            return new Intl.NumberFormat('vi-VN').format(Number(n)) + 'đ';
        } catch (e) {
            return String(n) + 'đ';
        }
    }

    function ensureWrap(input) {
        var parent = input.parentElement;
        if (!parent) return null;
        if (parent.classList.contains('search-suggest-wrap')) return parent;
        // Prefer wrapping just the input when parent is input-group: insert wrap around input
        var wrap = document.createElement('div');
        wrap.className = 'search-suggest-wrap flex-grow-1';
        parent.insertBefore(wrap, input);
        wrap.appendChild(input);
        return wrap;
    }

    function closeDropdown(state) {
        if (state.dropdown && state.dropdown.parentNode) {
            state.dropdown.remove();
        }
        state.dropdown = null;
        state.items = [];
        state.activeIndex = -1;
        inputAria(state.input, false);
    }

    function inputAria(input, expanded) {
        input.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-autocomplete', 'list');
    }

    function setActive(state, index) {
        state.activeIndex = index;
        if (!state.dropdown) return;
        var nodes = state.dropdown.querySelectorAll('[data-suggest-index]');
        for (var i = 0; i < nodes.length; i++) {
            nodes[i].classList.toggle('is-active', i === index);
        }
        if (index >= 0 && nodes[index]) {
            state.input.setAttribute('aria-activedescendant', nodes[index].id);
            nodes[index].scrollIntoView({ block: 'nearest' });
        } else {
            state.input.removeAttribute('aria-activedescendant');
        }
    }

    function render(state, data) {
        closeDropdown(state);
        var wrap = ensureWrap(state.input);
        if (!wrap) return;

        var dd = document.createElement('div');
        dd.className = 'search-suggest-dropdown';
        dd.setAttribute('role', 'listbox');
        dd.id = 'search-suggest-list-' + state.uid;

        var items = [];
        var html = '';

        function pushItem(kind, labelHtml, href, extraClass) {
            var idx = items.length;
            var id = 'ssi-' + state.uid + '-' + idx;
            items.push({ href: href });
            html +=
                '<a href="' +
                escapeHtml(href) +
                '" class="search-suggest-item ' +
                (extraClass || '') +
                '" role="option" id="' +
                id +
                '" data-suggest-index="' +
                idx +
                '">' +
                labelHtml +
                '</a>';
        }

        var products = data.products || data.Products || [];
        var categories = data.categories || data.Categories || [];
        var keywords = data.keywords || data.Keywords || [];
        var viewAll = data.viewAllUrl || data.ViewAllUrl || '/Shop';
        var q = data.query || data.Query || state.input.value || '';

        if (products.length) {
            html += '<div class="search-suggest-group-title">Sản phẩm</div>';
            products.forEach(function (p) {
                var name = p.name || p.Name || '';
                var url = p.url || p.Url || '#';
                var img = p.imageUrl || p.ImageUrl;
                var price = p.salePrice != null ? p.salePrice : p.SalePrice != null ? p.SalePrice : p.price != null ? p.price : p.Price;
                var listPrice = p.price != null ? p.price : p.Price;
                var sale = p.salePrice != null ? p.salePrice : p.SalePrice;
                var thumb = img
                    ? '<img class="search-suggest-thumb" src="' + escapeHtml(img) + '" alt="">'
                    : '<span class="search-suggest-thumb-placeholder"><i class="fa fa-leaf"></i></span>';
                var priceHtml = '';
                if (sale != null && listPrice != null && Number(sale) < Number(listPrice)) {
                    priceHtml =
                        '<span class="search-suggest-price"><del>' +
                        escapeHtml(formatPrice(listPrice)) +
                        '</del>' +
                        escapeHtml(formatPrice(sale)) +
                        '</span>';
                } else if (price != null) {
                    priceHtml =
                        '<span class="search-suggest-price">' +
                        escapeHtml(formatPrice(price)) +
                        '</span>';
                }
                pushItem(
                    'product',
                    thumb +
                        '<span class="search-suggest-meta"><span class="search-suggest-name">' +
                        escapeHtml(name) +
                        '</span>' +
                        priceHtml +
                        '</span>',
                    url
                );
            });
        }

        if (categories.length) {
            html += '<div class="search-suggest-group-title">Danh mục</div>';
            categories.forEach(function (c) {
                var name = c.name || c.Name || '';
                var url = c.url || c.Url || '#';
                pushItem(
                    'cat',
                    '<span class="search-suggest-thumb-placeholder"><i class="fa fa-folder"></i></span>' +
                        '<span class="search-suggest-meta"><span class="search-suggest-name">' +
                        escapeHtml(name) +
                        '</span></span>',
                    url
                );
            });
        }

        if (keywords.length) {
            html += '<div class="search-suggest-group-title">Gợi ý</div>';
            keywords.forEach(function (k) {
                var text = k.text || k.Text || '';
                var url = k.url || k.Url || '#';
                pushItem(
                    'kw',
                    '<span class="search-suggest-thumb-placeholder"><i class="fa fa-search"></i></span>' +
                        '<span class="search-suggest-meta"><span class="search-suggest-name">' +
                        escapeHtml(text) +
                        '</span></span>',
                    url
                );
            });
        }

        if (!products.length && !categories.length && !keywords.length) {
            html +=
                '<div class="search-suggest-empty">Không có gợi ý phù hợp</div>';
        }

        var viewIdx = items.length;
        items.push({ href: viewAll });
        html +=
            '<a href="' +
            escapeHtml(viewAll) +
            '" class="search-suggest-view-all" role="option" id="ssi-' +
            state.uid +
            '-' +
            viewIdx +
            '" data-suggest-index="' +
            viewIdx +
            '">Xem tất cả kết quả cho “' +
            escapeHtml(q) +
            '”</a>';

        dd.innerHTML = html;
        wrap.appendChild(dd);
        state.dropdown = dd;
        state.items = items;
        state.activeIndex = -1;
        inputAria(state.input, true);

        dd.addEventListener('mousedown', function (e) {
            // prevent input blur before navigation
            e.preventDefault();
        });
    }

    function fetchSuggest(state, q) {
        var seq = ++state.seq;
        fetch('/api/search/suggest?q=' + encodeURIComponent(q), {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        })
            .then(function (res) {
                if (!res.ok) throw new Error('suggest failed');
                return res.json();
            })
            .then(function (data) {
                if (seq !== state.seq) return;
                if ((state.input.value || '').trim() !== q) return;
                render(state, data || {});
            })
            .catch(function () {
                if (seq !== state.seq) return;
                closeDropdown(state);
            });
    }

    function onInput(state) {
        var q = (state.input.value || '').trim();
        if (q.length < MIN_LEN) {
            closeDropdown(state);
            return;
        }
        clearTimeout(state.timer);
        state.timer = setTimeout(function () {
            fetchSuggest(state, q);
        }, DEBOUNCE_MS);
    }

    function onKeyDown(state, e) {
        if (!state.dropdown) {
            if (e.key === 'ArrowDown' && (state.input.value || '').trim().length >= MIN_LEN) {
                onInput(state);
            }
            return;
        }
        if (e.key === 'Escape') {
            e.preventDefault();
            closeDropdown(state);
            return;
        }
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            var next = state.activeIndex + 1;
            if (next >= state.items.length) next = 0;
            setActive(state, next);
            return;
        }
        if (e.key === 'ArrowUp') {
            e.preventDefault();
            var prev = state.activeIndex - 1;
            if (prev < 0) prev = state.items.length - 1;
            setActive(state, prev);
            return;
        }
        if (e.key === 'Enter' && state.activeIndex >= 0 && state.items[state.activeIndex]) {
            e.preventDefault();
            window.location.href = state.items[state.activeIndex].href;
        }
        // Enter with no selection → let form submit (Shop search)
    }

    function bindInput(input, index) {
        if (input.dataset.searchSuggestBound === '1') return;
        input.dataset.searchSuggestBound = '1';
        inputAria(input, false);

        var state = {
            input: input,
            uid: String(index) + '-' + Math.random().toString(36).slice(2, 7),
            timer: null,
            seq: 0,
            dropdown: null,
            items: [],
            activeIndex: -1
        };

        input.addEventListener('input', function () {
            onInput(state);
        });
        input.addEventListener('keydown', function (e) {
            onKeyDown(state, e);
        });
        input.addEventListener('blur', function () {
            setTimeout(function () {
                closeDropdown(state);
            }, 150);
        });
        input.addEventListener('focus', function () {
            var q = (input.value || '').trim();
            if (q.length >= MIN_LEN) onInput(state);
        });
    }

    function boot() {
        document.querySelectorAll('[data-search-suggest]').forEach(function (el, i) {
            bindInput(el, i);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
