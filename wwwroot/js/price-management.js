/* Quản lý giá (Admin) — tách từ Areas/Admin/Views/Price/Index.cshtml
   Toàn bộ logic trang giá: toast, chọn dòng/bulk, modal lịch, timeline Gantt,
   sửa giá inline, sort phía client, realtime. Khởi tạo qua PricePage.init(config). */
(function () {
    'use strict';

    let config = { urls: {} };

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    /** Toast popup (thay alert) - dùng toast-container của admin layout */
    function showPriceToast(type, message, title) {
        let container = document.querySelector('.toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            container.style.zIndex = '1100';
            document.body.appendChild(container);
        }
        const isSuccess = type === 'success';
        const isWarning = type === 'warning';
        const cls = isSuccess
            ? 'text-white border-0'
            : isWarning
                ? 'text-bg-warning border-0'
                : 'text-bg-danger border-0';
        const icon = isSuccess ? 'fa-check-circle' : isWarning ? 'fa-exclamation-triangle' : 'fa-exclamation-circle';
        const heading = title || (isSuccess ? 'Thành công' : isWarning ? 'Cảnh báo' : 'Lỗi');
        const el = document.createElement('div');
        el.className = `toast align-items-center ${cls}`;
        el.setAttribute('role', 'alert');
        el.setAttribute('aria-live', 'assertive');
        el.setAttribute('aria-atomic', 'true');
        if (isSuccess) el.setAttribute('style', 'background-color:#81c408;');
        el.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                <i class="fas ${icon} me-2"></i>
                <strong class="me-1">${esc(heading)}</strong>${esc(message)}
            </div>
            <button type="button" class="btn-close ${isWarning ? '' : 'btn-close-white'} me-2 m-auto" data-bs-dismiss="toast" aria-label="Đóng"></button>
        </div>`;
        container.appendChild(el);
        const toast = new bootstrap.Toast(el, { autohide: true, delay: 4200 });
        el.addEventListener('hidden.bs.toast', () => el.remove());
        toast.show();
    }

    /* ---- Hủy lịch ---- */
    function openCancelSchedule(id, label) {
        const form = document.getElementById('cancelScheduleForm');
        form.action = `${config.urls.cancelSchedule}/${id}`;
        document.getElementById('cancelScheduleLabel').textContent = label || 'Lịch giảm giá đã chọn';
        new bootstrap.Modal(document.getElementById('cancelScheduleModal')).show();
    }

    /* ---- Chọn dòng / bulk ---- */
    const updateSelection = () => {
        const n = document.querySelectorAll('.row-check:checked').length;
        const countEl = document.getElementById('selectedCount');
        if (countEl) countEl.textContent = n;
        const previewBtn = document.getElementById('bulkPreviewBtn');
        if (previewBtn) previewBtn.disabled = n === 0;
        document.getElementById('selChip')?.classList.toggle('has-sel', n > 0);
    };

    function bindSelection() {
        document.getElementById('selectAll')?.addEventListener('change', e => {
            document.querySelectorAll('.row-check').forEach(x => x.checked = e.target.checked);
            updateSelection();
        });
        document.querySelectorAll('.row-check').forEach(x => x.addEventListener('change', updateSelection));
        updateSelection();
    }

    function bindBulkForm() {
        document.getElementById('bulkForm')?.addEventListener('submit', e => {
            e.preventDefault();
            const rows = [...document.querySelectorAll('.row-check:checked')];
            if (!rows.length) return;
            const form = e.currentTarget;
            const value = Number(form.elements.value.value);
            const percent = form.elements.adjustmentType.value === 'Percentage';
            const increase = form.elements.direction.value === 'Increase';
            let invalid = 0;
            document.getElementById('bulkPreviewBody').innerHTML = rows.map(row => {
                const current = Number(row.dataset.price);
                const delta = percent ? Math.round(current * value / 100) : value;
                const next = increase ? current + delta : current - delta;
                if (next <= 0) invalid++;
                return `<tr${next <= 0 ? ' class="table-danger"' : ''}><td>${esc(row.dataset.label)}</td><td class="text-end">${current.toLocaleString('vi-VN')}đ</td><td class="text-end fw-bold${next <= 0 ? ' text-danger' : ''}">${next.toLocaleString('vi-VN')}đ</td></tr>`;
            }).join('');
            document.getElementById('bulkInvalidWarning').classList.toggle('d-none', invalid === 0);
            const applyBtn = document.getElementById('bulkApplyBtn');
            applyBtn.innerHTML = `<i class="fas fa-check me-1"></i>Áp dụng cho ${rows.length} dòng`;
            applyBtn.disabled = invalid > 0;
            new bootstrap.Modal(document.getElementById('bulkPreviewModal')).show();
        });
        document.getElementById('bulkApplyBtn')?.addEventListener('click', () => document.getElementById('bulkForm').submit());
    }

    /* ---- Modal lịch (tạo / sửa / nhân bản) ---- */
    function updateValueHint() {
        const percent = document.getElementById('scheduleType').value === 'Percentage';
        document.getElementById('scheduleValue').max = percent ? 99 : '';
        document.getElementById('scheduleValueHint').textContent = percent
            ? 'Phần trăm giảm, từ 1 đến 99(%).'
            : 'Giá bán cụ thể (đ), nên nhỏ hơn giá gốc.';
    }

    function resetScheduleForm() {
        const form = document.getElementById('scheduleForm');
        form.reset();
        form.action = config.urls.createSchedule;
        document.getElementById('scheduleModalTitle').innerHTML = '<i class="fas fa-calendar-plus"></i> Tạo lịch giảm giá';
        document.getElementById('scheduleTarget').disabled = false;
        document.getElementById('scheduleStart').min = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
        updateValueHint();
    }

    function openCreateSchedule() {
        resetScheduleForm();
        new bootstrap.Modal(document.getElementById('scheduleModal')).show();
    }

    function openCreateScheduleFor(productId, variantId) {
        resetScheduleForm();
        if (productId) {
            const sel = document.getElementById('scheduleTarget');
            sel.value = `${productId}:${variantId || ''}`;
            document.getElementById('scheduleProductId').value = productId;
            document.getElementById('scheduleVariantId').value = variantId || '';
        }
        new bootstrap.Modal(document.getElementById('scheduleModal')).show();
    }

    function openSchedule(data) {
        resetScheduleForm();
        document.getElementById('scheduleTarget').value = `${data.product}:${data.variant || ''}`;
        document.getElementById('scheduleProductId').value = data.product;
        document.getElementById('scheduleVariantId').value = data.variant || '';
        document.getElementById('scheduleType').value = data.type;
        document.getElementById('scheduleValue').value = data.value;
        document.getElementById('scheduleStart').value = data.start || '';
        document.getElementById('scheduleEnd').value = data.end || '';
        if (data.mode === 'edit') {
            document.getElementById('scheduleForm').action = config.urls.updateSchedule + '/' + data.id;
            document.getElementById('scheduleModalTitle').innerHTML = '<i class="fas fa-pen"></i> Sửa lịch giảm giá';
            document.getElementById('scheduleTarget').disabled = true;
        } else {
            document.getElementById('scheduleModalTitle').innerHTML = '<i class="fas fa-copy"></i> Nhân bản lịch giảm giá';
        }
        updateValueHint();
        new bootstrap.Modal(document.getElementById('scheduleModal')).show();
    }

    function bindScheduleModal() {
        document.getElementById('createScheduleBtn')?.addEventListener('click', openCreateSchedule);
        document.getElementById('scheduleType')?.addEventListener('change', updateValueHint);
        document.getElementById('scheduleTarget')?.addEventListener('change', e => {
            const p = e.target.value.split(':');
            document.getElementById('scheduleProductId').value = p[0] || '';
            document.getElementById('scheduleVariantId').value = p[1] || '';
        });
        document.querySelectorAll('.schedule-action').forEach(button => button.addEventListener('click', () => openSchedule(button.dataset)));
        document.querySelectorAll('.cancel-schedule-btn').forEach(btn => {
            btn.addEventListener('click', () => openCancelSchedule(btn.dataset.id, btn.dataset.label));
        });
    }

    /* ---- Modal sửa giá gốc ---- */
    function bindBasePriceModal() {
        document.querySelectorAll('.base-price-action').forEach(button => button.addEventListener('click', () => {
            const d = button.dataset;
            document.getElementById('baseProductId').value = d.product;
            document.getElementById('baseVariantId').value = d.variant || '';
            document.getElementById('newPrice').value = d.price;
            document.getElementById('basePriceLabel').textContent = d.label;
            document.getElementById('basePriceCurrent').textContent = Number(d.price).toLocaleString('vi-VN') + 'đ';
            new bootstrap.Modal(document.getElementById('basePriceModal')).show();
        }));
    }

    /* ---- Nhóm biến thể ---- */
    function bindGroupToggles() {
        document.querySelectorAll('.group-toggle').forEach(btn => btn.addEventListener('click', () => {
            const g = btn.dataset.group;
            const open = btn.classList.toggle('open');
            document.querySelectorAll(`.variant-row[data-group="${g}"]`).forEach(tr => tr.classList.toggle('d-none', !open));
            document.querySelectorAll(`.timeline-row[data-group="${g}"]`).forEach(tr => {
                tr.classList.add('d-none');
            });
            document.querySelectorAll(`.timeline-toggle[data-group="${g}"]`).forEach(b => {
                b.classList.remove('open');
                b.setAttribute('aria-expanded', 'false');
            });
        }));
    }

    /* ---- Timeline Gantt ---- */
    const tlStatusText = { active: 'Đang chạy', scheduled: 'Sắp chạy', ended: 'Đã kết thúc', cancelled: 'Đã hủy' };
    const tlStatusIcon = { active: 'fa-bolt', scheduled: 'fa-clock', ended: 'fa-check', cancelled: 'fa-ban' };
    const tlFmtDay = d => `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
    const tlFmtFull = d => `${tlFmtDay(d)}/${d.getFullYear()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    const tlValueLabel = i => (i.kind === 'fixed' || i.type === 'FixedPrice')
        ? `Còn ${Number(i.value).toLocaleString('vi-VN')}đ`
        : `-${Number(i.value).toLocaleString('vi-VN')}%`;

    function tlAssignLanes(items, maxTs) {
        const sorted = [...items].sort((a, b) => a._s - b._s || a._e - b._e);
        const laneEnds = [];
        for (const it of sorted) {
            const end = it.end ? it._e : maxTs;
            let lane = laneEnds.findIndex(e => e <= it._s);
            if (lane < 0) {
                lane = laneEnds.length;
                laneEnds.push(end);
            } else {
                laneEnds[lane] = end;
            }
            it._lane = lane;
        }
        return laneEnds.length || 1;
    }

    function tlHideTip() {
        document.querySelectorAll('.tl-tip.show').forEach(t => t.classList.remove('show'));
    }

    function tlShowTip(tipEl, html, x, y) {
        tipEl.innerHTML = html;
        tipEl.classList.add('show');
        const pad = 12;
        const rect = tipEl.getBoundingClientRect();
        let left = x + 12;
        let top = y + 14;
        if (left + rect.width > window.innerWidth - pad) left = x - rect.width - 12;
        if (top + rect.height > window.innerHeight - pad) top = y - rect.height - 10;
        tipEl.style.left = Math.max(pad, left) + 'px';
        tipEl.style.top = Math.max(pad, top) + 'px';
    }

    function tlBindInteractions(wrap, items) {
        const tip = wrap.querySelector('.tl-tip');
        const highlight = id => {
            wrap.querySelectorAll('.tl-bar, .tl-item').forEach(el => {
                el.classList.toggle('is-active-item', String(el.dataset.id) === String(id));
            });
        };
        const clearHighlight = () => {
            wrap.querySelectorAll('.is-active-item').forEach(el => el.classList.remove('is-active-item'));
        };

        wrap.querySelectorAll('.tl-bar, .tl-item').forEach(el => {
            const id = el.dataset.id;
            const item = items.find(x => String(x.id) === String(id));
            if (!item) return;
            const tipHtml = `<strong>${esc(tlValueLabel(item))}</strong><span class="muted">${esc(tlStatusText[item.status] || item.status)}</span><br>${esc(tlFmtFull(new Date(item.start)))} → ${item.end ? esc(tlFmtFull(new Date(item.end))) : 'Không giới hạn'}`;

            el.addEventListener('mouseenter', e => {
                highlight(id);
                tlShowTip(tip, tipHtml, e.clientX, e.clientY);
            });
            el.addEventListener('mousemove', e => {
                if (tip.classList.contains('show')) tlShowTip(tip, tipHtml, e.clientX, e.clientY);
            });
            el.addEventListener('mouseleave', () => {
                clearHighlight();
                tlHideTip();
            });
            el.addEventListener('focus', () => {
                highlight(id);
                const r = el.getBoundingClientRect();
                tlShowTip(tip, tipHtml, r.left + r.width / 2, r.bottom);
            });
            el.addEventListener('blur', () => {
                clearHighlight();
                tlHideTip();
            });
        });

        const editItem = item => {
            if (!item || item.status !== 'scheduled') return;
            openSchedule({
                mode: 'edit',
                id: item.id,
                product: item.productId,
                variant: item.variantId || '',
                type: item.type,
                value: item.value,
                start: item.startLocal,
                end: item.endLocal || ''
            });
        };
        wrap.querySelectorAll('.tl-bar.is-clickable, .tl-edit-btn').forEach(el => {
            const go = e => {
                e.preventDefault();
                e.stopPropagation();
                editItem(items.find(x => String(x.id) === String(el.dataset.id)));
            };
            el.addEventListener('click', go);
            el.addEventListener('keydown', e => {
                if (e.key === 'Enter' || e.key === ' ') go(e);
            });
        });

        wrap.querySelectorAll('.tl-clone-btn').forEach(el => {
            el.addEventListener('click', e => {
                e.stopPropagation();
                const item = items.find(x => String(x.id) === String(el.dataset.id));
                if (!item) return;
                openSchedule({
                    mode: 'clone',
                    product: item.productId,
                    variant: item.variantId || '',
                    type: item.type,
                    value: item.value
                });
            });
        });

        wrap.querySelector('.tl-create-btn')?.addEventListener('click', () => {
            openCreateScheduleFor(wrap.dataset.product, wrap.dataset.variant);
        });
    }

    function renderTimeline(wrap) {
        const items = JSON.parse(wrap.dataset.schedules || '[]');
        const productId = wrap.dataset.product || '';
        const variantId = wrap.dataset.variant || '';
        const label = wrap.dataset.label || '';
        // Preserve datasets: only replace children, not the wrap element itself
        const schedulesJson = wrap.dataset.schedules;

        if (!items.length) {
            wrap.innerHTML = `
            <div class="tl-empty">
                <div class="tl-empty-icon"><i class="far fa-calendar-times"></i></div>
                <div class="tl-empty-copy">
                    <strong>Chưa có lịch giảm giá</strong>
                    <p>${label ? esc(label) + ' · ' : ''}Tạo lịch để lên kế hoạch giá theo thời gian.</p>
                </div>
                <button type="button" class="btn btn-sm btn-primary tl-create-btn">
                    <i class="fas fa-calendar-plus me-1"></i>Tạo lịch
                </button>
            </div>`;
            wrap.dataset.schedules = schedulesJson;
            wrap.dataset.product = productId;
            wrap.dataset.variant = variantId;
            wrap.dataset.label = label;
            wrap.querySelector('.tl-create-btn')?.addEventListener('click', () => openCreateScheduleFor(productId, variantId));
            return;
        }

        const now = Date.now();
        items.forEach(i => {
            i._s = new Date(i.start).getTime();
            i._e = i.end ? new Date(i.end).getTime() : now + 14 * 864e5;
        });

        let min = Math.min(...items.map(i => i._s), now);
        let max = Math.max(...items.map(i => i.end ? i._e : now), now + 14 * 864e5);
        const hasOpen = items.some(i => !i.end);
        if (hasOpen) max = Math.max(max, now + 21 * 864e5);
        if (max - min < 7 * 864e5) {
            const pad = (7 * 864e5 - (max - min)) / 2;
            min -= pad;
            max += pad;
        }
        min -= (max - min) * 0.08;
        max += (max - min) * 0.06;
        const span = max - min;
        const pctNum = t => ((t - min) / span * 100);
        const pct = t => pctNum(t).toFixed(2) + '%';
        const laneCount = tlAssignLanes(items, max);
        const laneH = 28;
        const topPad = 22;
        const bottomPad = 22;
        const chartH = topPad + laneCount * laneH + bottomPad;
        const axisTop = topPad + laneCount * laneH + 4;
        const nowPct = pctNum(now);
        const nowLabelRight = nowPct > 78;

        let chartHtml = `<div class="tl-axis" style="top:${axisTop}px"></div>`;
        for (let k = 0; k <= 4; k++) {
            const t = min + span * k / 4;
            chartHtml += `<div class="tl-tick" style="left:${pct(t)};top:${axisTop + 8}px">${tlFmtDay(new Date(t))}</div>`;
        }
        chartHtml += `<div class="tl-now" style="left:${pct(now)};height:${axisTop - 2}px">
        <span class="tl-now-label${nowLabelRight ? ' is-right' : ''}">Hôm nay</span>
    </div>`;

        for (const i of items) {
            const s = i._s;
            const e = i.end ? i._e : max;
            const left = pctNum(s);
            const width = Math.max(pctNum(e) - left, 0.8);
            const narrow = width < 11;
            const clickable = i.status === 'scheduled';
            const lbl = tlValueLabel(i);
            const top = topPad + i._lane * laneH;
            chartHtml += `<div class="tl-bar ${i.status}${i.end ? '' : ' open-ended'}${narrow ? ' narrow' : ''}${clickable ? ' is-clickable' : ''}"
            data-id="${i.id}"
            role="${clickable ? 'button' : 'img'}"
            tabindex="0"
            aria-label="${esc(lbl + ' · ' + (tlStatusText[i.status] || i.status))}"
            style="left:${left.toFixed(2)}%;width:${width.toFixed(2)}%;top:${top}px">
            <span class="tl-bar-label">${esc(lbl)}</span>
        </div>`;
        }

        const listHtml = items.map(i => {
            const canEdit = i.status === 'scheduled';
            const canCancel = i.status === 'active' || i.status === 'scheduled';
            const actions = [];
            if (canEdit) {
                actions.push(`<button type="button" class="btn btn-sm btn-outline-primary btn-text-sm tl-edit-btn" data-id="${i.id}" title="Sửa lịch"><i class="fas fa-pen me-1"></i>Sửa</button>`);
            }
            actions.push(`<button type="button" class="btn-icon tl-clone-btn" data-id="${i.id}" title="Nhân bản lịch" aria-label="Nhân bản lịch"><i class="fas fa-copy"></i></button>`);
            if (canCancel) {
                const itemLabel = label || tlValueLabel(i);
                actions.push(`<button type="button" class="btn btn-sm btn-outline-danger btn-text-sm cancel-schedule-btn" data-id="${i.id}" data-label="${esc(itemLabel)}" title="Hủy lịch"><i class="fas fa-ban me-1"></i>Hủy</button>`);
            }
            return `<li class="tl-item" data-id="${i.id}" tabindex="0">
            <span class="tl-item-status ${i.status}"><i class="fas ${tlStatusIcon[i.status] || 'fa-circle'}"></i>${esc(tlStatusText[i.status] || i.status)}</span>
            <div class="tl-item-main">
                <div class="tl-item-value">${esc(tlValueLabel(i))}</div>
                <div class="tl-item-period">
                    <span>${esc(tlFmtFull(new Date(i.start)))}</span>
                    <span class="sep"><i class="fas fa-arrow-right"></i></span>
                    <span>${i.end ? esc(tlFmtFull(new Date(i.end))) : 'Không giới hạn'}</span>
                </div>
            </div>
            <div class="tl-item-actions">${actions.join('')}</div>
        </li>`;
        }).join('');

        wrap.innerHTML = `
        <div class="tl-head">
            <span class="tl-head-title"><i class="fas fa-chart-line"></i> Timeline giá${label ? ' · ' + esc(label) : ''}</span>
            <div class="tl-legend">
                <span><i class="dot active"></i> Đang chạy</span>
                <span><i class="dot scheduled"></i> Sắp chạy</span>
                <span><i class="dot ended"></i> Đã kết thúc</span>
                <span><i class="dot cancelled"></i> Đã hủy</span>
            </div>
            <span class="tl-count">${items.length} lịch</span>
        </div>
        <div class="tl-body">
            <div class="tl-chart" style="height:${chartH}px">${chartHtml}</div>
            <ul class="tl-list">${listHtml}</ul>
        </div>
        <div class="tl-tip" role="tooltip"></div>`;
        // dataset survives on the element itself in modern browsers when only innerHTML changes,
        // but re-assign defensively in case of quirks
        wrap.dataset.schedules = schedulesJson;
        wrap.dataset.product = productId;
        wrap.dataset.variant = variantId;
        wrap.dataset.label = label;
        tlBindInteractions(wrap, items);
        wrap.querySelectorAll('.cancel-schedule-btn').forEach(btn => {
            btn.addEventListener('click', e => {
                e.stopPropagation();
                openCancelSchedule(btn.dataset.id, btn.dataset.label || label);
            });
        });
    }

    function closeAllTimelines(exceptId) {
        document.querySelectorAll('.timeline-row').forEach(tr => {
            if (exceptId && tr.id === exceptId) return;
            tr.classList.add('d-none');
        });
        document.querySelectorAll('.timeline-toggle').forEach(b => {
            if (exceptId && b.dataset.target === exceptId) return;
            b.classList.remove('open');
            b.setAttribute('aria-expanded', 'false');
        });
    }

    function bindTimelineToggles() {
        document.querySelectorAll('.timeline-toggle').forEach(btn => btn.addEventListener('click', () => {
            const panel = document.getElementById(btn.dataset.target);
            if (!panel) return;
            const opening = panel.classList.contains('d-none');
            if (opening) closeAllTimelines(panel.id);
            panel.classList.toggle('d-none', !opening);
            btn.classList.toggle('open', opening);
            btn.setAttribute('aria-expanded', opening ? 'true' : 'false');
            if (opening) {
                const wrap = panel.querySelector('.timeline-wrap');
                if (wrap) renderTimeline(wrap);
            } else {
                tlHideTip();
            }
        }));
    }

    /* ---- Sửa giá gốc inline ---- */
    function bindInlineEditor() {
        document.querySelectorAll('.base-price-cell').forEach(cell => cell.addEventListener('click', () => {
            if (cell.querySelector('.inline-price-editor')) return;
            const original = cell.innerHTML;
            cell.innerHTML = `<div class="input-group input-group-sm inline-price-editor"><input type="number" min="1" step="0.01" class="form-control form-control-sm" value="${cell.dataset.price}"><span class="input-group-text">đ</span></div><div class="inline-price-error d-none"></div>`;
            const input = cell.querySelector('input');
            input.focus();
            input.select();
            const cancel = () => { cell.innerHTML = original; };
            const showError = msg => {
                const err = cell.querySelector('.inline-price-error');
                err.textContent = msg;
                err.classList.remove('d-none');
            };
            input.addEventListener('keydown', async e => {
                if (e.key === 'Escape') { cancel(); return; }
                if (e.key !== 'Enter') return;
                const newPrice = Number(input.value);
                if (!newPrice || newPrice <= 0) { showError('Giá phải lớn hơn 0.'); return; }
                input.disabled = true;
                try {
                    const token = document.querySelector('#bulkForm input[name="__RequestVerificationToken"]').value;
                    const body = new URLSearchParams({
                        productId: cell.dataset.product,
                        productVariantId: cell.dataset.variant || '',
                        newPrice
                    });
                    const res = await fetch(config.urls.updateBasePrice, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/x-www-form-urlencoded',
                            'RequestVerificationToken': token,
                            'X-Requested-With': 'XMLHttpRequest'
                        },
                        body
                    });
                    const json = await res.json();
                    if (json.success) {
                        cell.dataset.price = newPrice;
                        cell.innerHTML = `<span class="base-price-value">${newPrice.toLocaleString('vi-VN')}đ</span><i class="fas fa-pen edit-hint"></i>`;
                        const row = cell.closest('tr');
                        row.querySelector('.base-price-action').dataset.price = newPrice;
                        row.querySelector('.row-check').dataset.price = newPrice;
                        showPriceToast('success', 'Đã cập nhật giá gốc.');
                    } else {
                        input.disabled = false;
                        showError(json.error || 'Không lưu được giá.');
                        showPriceToast('error', json.error || 'Không lưu được giá.');
                    }
                } catch {
                    input.disabled = false;
                    showError('Lỗi kết nối, thử lại.');
                    showPriceToast('error', 'Lỗi kết nối, thử lại.');
                }
            });
            input.addEventListener('blur', () => {
                if (cell.querySelector('.inline-price-editor') && !input.disabled) cancel();
            });
        }));
    }

    /* ---- Sort phía client (group-aware) ---- */
    function bindSorting() {
        document.querySelectorAll('th.sortable').forEach(th => th.addEventListener('click', () => {
            const key = th.dataset.sort;
            const dir = th.dataset.dir === 'asc' ? 'desc' : 'asc';
            document.querySelectorAll('th.sortable').forEach(x => {
                delete x.dataset.dir;
                x.querySelector('.sort-arrow').className = 'fas fa-sort sort-arrow';
            });
            th.dataset.dir = dir;
            th.querySelector('.sort-arrow').className = `fas fa-sort-${dir === 'asc' ? 'up' : 'down'} sort-arrow active`;
            const tbody = document.getElementById('priceTbody');
            const blocks = [];
            let current = null;
            for (const tr of [...tbody.children]) {
                if (tr.classList.contains('group-parent')) {
                    current = {
                        parent: tr,
                        rows: [],
                        name: (tr.dataset.name || '').toLowerCase(),
                        base: Number(tr.dataset.base),
                        effective: Number(tr.dataset.effective)
                    };
                    blocks.push(current);
                } else if (tr.classList.contains('price-row')) {
                    if (!tr.dataset.group) {
                        current = {
                            parent: null,
                            rows: [],
                            name: (tr.dataset.name || '').toLowerCase(),
                            base: Number(tr.dataset.base),
                            effective: Number(tr.dataset.effective)
                        };
                        blocks.push(current);
                    }
                    current.rows.push(tr);
                } else if (tr.classList.contains('timeline-row')) {
                    current?.rows.push(tr);
                } else {
                    blocks.push({ parent: null, rows: [tr], name: '', base: 0, effective: 0, fixed: true });
                }
            }
            const cmp = (a, b) => {
                const av = key === 'name' ? a.name : key === 'base' ? a.base : a.effective;
                const bv = key === 'name' ? b.name : key === 'base' ? b.base : b.effective;
                const r = typeof av === 'string' ? av.localeCompare(bv, 'vi') : av - bv;
                return dir === 'asc' ? r : -r;
            };
            blocks.filter(b => !b.fixed).sort(cmp).forEach(b => {
                if (b.parent) tbody.appendChild(b.parent);
                b.rows.forEach(tr => tbody.appendChild(tr));
            });
        }));
    }

    /* ---- Realtime ---- */
    function bindRealtime() {
        if (window.ecommerceHub) {
            window.ecommerceHub.on('PriceChanged', () => {
                if (!document.querySelector('.modal.show') && !document.querySelector('.inline-price-editor')) {
                    window.location.reload();
                }
            });
        }
    }

    function init(cfg) {
        config = Object.assign(config, cfg || {});
        bindSelection();
        bindBulkForm();
        bindScheduleModal();
        bindBasePriceModal();
        bindGroupToggles();
        bindTimelineToggles();
        bindInlineEditor();
        bindRealtime();
    }

    window.PricePage = { init };
})();
