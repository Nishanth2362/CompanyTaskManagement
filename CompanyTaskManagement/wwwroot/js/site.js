/**
 * 
 * TaskFlow - Interactive UI & Client-Side Features
 */

document.addEventListener('DOMContentLoaded', () => {
    initThemeToggle();
    initIndexPageFeatures();
    initFormPageFeatures();
    initGlobalCopyButtons();
    initUniversalSearchClearButtons();
});

/* ==========================================================================
   1. Theme Management (Light / Dark)
   ========================================================================== */
function initThemeToggle() {
    const themeBtn = document.getElementById('themeToggleBtn');
    if (!themeBtn) return;

    const darkIcon = themeBtn.querySelector('.theme-icon-dark');
    const lightIcon = themeBtn.querySelector('.theme-icon-light');

    const savedTheme = localStorage.getItem('taskflow_theme') || 'light';

    applyTheme(savedTheme);

    themeBtn.addEventListener('click', () => {
        const currentTheme = document.documentElement.getAttribute('data-bs-theme') || 'light';
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        applyTheme(newTheme);
        localStorage.setItem('taskflow_theme', newTheme);
        showAppToast(`Switched to ${newTheme.toUpperCase()} mode`, 'info');
    });

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        if (theme === 'dark') {
            if (darkIcon) darkIcon.classList.add('d-none');
            if (lightIcon) lightIcon.classList.remove('d-none');
            themeBtn.setAttribute('title', 'Switch to Light Mode');
            themeBtn.setAttribute('aria-label', 'Switch to Light Mode');
        } else {
            if (darkIcon) darkIcon.classList.remove('d-none');
            if (lightIcon) lightIcon.classList.add('d-none');
            themeBtn.setAttribute('title', 'Switch to Dark Mode');
            themeBtn.setAttribute('aria-label', 'Switch to Dark Mode');
        }
    }
}

/* ==========================================================================
   2. Global Toast Notification Helper
   ========================================================================== */
function showAppToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    const messageEl = document.getElementById('appToastMessage');
    const toastBody = document.getElementById('appToastBody');

    if (!toastEl || !messageEl) return;

    messageEl.textContent = message;

    // Update icon based on type
    const icon = toastBody.querySelector('i');
    if (icon) {
        icon.className = '';
        if (type === 'success') {
            icon.className = 'bi bi-check-circle-fill text-success fs-5';
        } else if (type === 'info') {
            icon.className = 'bi bi-info-circle-fill text-primary fs-5';
        } else if (type === 'warning') {
            icon.className = 'bi bi-exclamation-triangle-fill text-warning fs-5';
        } else {
            icon.className = 'bi bi-exclamation-octagon-fill text-danger fs-5';
        }
    }

    if (window.bootstrap && bootstrap.Toast) {
        const toast = bootstrap.Toast.getOrCreateInstance(toastEl, { delay: 3000 });
        toast.show();
    }
}

/* ==========================================================================
   3. Index Page (Unified Instant Filter Bar, Search, Responsive Views, Export, Quick View)
   ========================================================================== */
function initIndexPageFeatures() {
    const searchInput = document.getElementById('taskSearchInput');
    const clearBtn = document.getElementById('searchClearBtn');
    const companySelect = document.getElementById('companyFilterSelect');
    const employeeSelect = document.getElementById('employeeFilterSelect');
    const prioritySelect = document.getElementById('priorityFilterSelect');
    const statusSelect = document.getElementById('statusFilterSelect');
    const resetFilterBtn = document.getElementById('resetFilterBtn');

    const noResultsAlert = document.getElementById('noSearchResultsAlert');

    const filterTabBtns = Array.from(document.querySelectorAll('.filter-tab-btn'));
    const rows = Array.from(document.querySelectorAll('.task-row-item'));
    const gridCards = Array.from(document.querySelectorAll('#taskGridView .task-card-item'));

    if (rows.length === 0 && gridCards.length === 0) return;

    // Track active instant tab filter (default from active tab in HTML or 'all')
    const initialActiveTab = document.querySelector('.filter-tab-btn.active');
    let currentTabFilter = initialActiveTab ? (initialActiveTab.dataset.filter || 'all') : 'all';

    // Populate filter dropdowns dynamically from existing data
    populateDropdownFilters(rows, companySelect, employeeSelect);

    // Unified Instant Filter Tabs event listeners
    filterTabBtns.forEach(tabBtn => {
        tabBtn.addEventListener('click', () => {
            filterTabBtns.forEach(b => b.classList.remove('active'));
            tabBtn.classList.add('active');
            currentTabFilter = tabBtn.dataset.filter || 'all';
            applyCombinedFilter();
        });
    });

    // Combined filter function across tabs, search query, company, employee, priority, status
    function applyCombinedFilter() {
        const query = (searchInput ? searchInput.value : '').trim().toLowerCase();
        const selectedComp = companySelect ? companySelect.value.toLowerCase() : '';
        const selectedEmp = employeeSelect ? employeeSelect.value.toLowerCase() : '';
        const selectedPriority = prioritySelect ? prioritySelect.value.toLowerCase() : '';
        const selectedStatus = statusSelect ? statusSelect.value.toLowerCase() : '';

        // Show/hide clear search button
        if (clearBtn) {
            clearBtn.style.display = query.length > 0 ? 'block' : 'none';
        }

        const todayStr = new Date().toISOString().slice(0, 10);
        let visibleCount = 0;

        function checkItemMatch(el) {
            const id = (el.dataset.taskId || '').toLowerCase();
            const name = (el.dataset.taskName || '').toLowerCase();
            const desc = (el.dataset.taskDesc || '').toLowerCase();
            const comps = (el.dataset.companies || '').toLowerCase();
            const emps = (el.dataset.employees || '').toLowerCase();
            const project = (el.dataset.project || '').toLowerCase();
            const priority = (el.dataset.priority || '').toLowerCase();
            const status = (el.dataset.status || '').toLowerCase();
            const isToday = el.dataset.istoday === 'true' || el.dataset.duedate === todayStr || (!el.dataset.duedate && el.dataset.createddate === todayStr);
            const isOverdue = el.dataset.isoverdue === 'true' || (el.dataset.delayreason && status !== 'completed');

            // 1. Tab criteria
            let matchesTab = true;
            if (currentTabFilter === 'today') {
                matchesTab = isToday;
            } else if (currentTabFilter === 'inprogress') {
                matchesTab = status === 'inprogress';
            } else if (currentTabFilter === 'overdue' || currentTabFilter === 'incomplete') {
                matchesTab = isOverdue || (status !== 'completed' && el.dataset.delayreason);
            } else if (currentTabFilter === 'completed') {
                matchesTab = status === 'completed';
            } else if (currentTabFilter === 'urgent') {
                matchesTab = priority === 'urgent' || priority === 'high';
            }

            // 2. Search query criteria
            const matchesQuery = !query ||
                id.includes(query) ||
                name.includes(query) ||
                desc.includes(query) ||
                comps.includes(query) ||
                emps.includes(query) ||
                project.includes(query);

            // 3. Dropdown criteria
            const matchesComp = !selectedComp || comps.includes(selectedComp);
            const matchesEmp = !selectedEmp || emps.includes(selectedEmp);
            const matchesPriority = !selectedPriority || priority === selectedPriority;
            const matchesStatus = !selectedStatus || status === selectedStatus;

            return matchesTab && matchesQuery && matchesComp && matchesEmp && matchesPriority && matchesStatus;
        }

        // Filter Table Rows & Update Row Numbers
        let currentVisibleNum = 1;
        rows.forEach(row => {
            const isMatch = checkItemMatch(row);
            row.style.display = isMatch ? '' : 'none';
            if (isMatch) {
                visibleCount++;
                const numBadge = row.querySelector('.row-number-badge');
                if (numBadge) {
                    numBadge.textContent = currentVisibleNum++;
                }
            }
        });

        // Filter Mobile Grid Cards & Update Mobile Row Numbers
        let currentVisibleCardNum = 1;
        gridCards.forEach(card => {
            const isMatch = checkItemMatch(card);
            card.style.display = isMatch ? '' : 'none';
            if (isMatch) {
                const numBadge = card.querySelector('.row-number-badge');
                if (numBadge) {
                    numBadge.textContent = currentVisibleCardNum++;
                }
            }
        });

        // Update live total tasks counter on stat card
        const statTotalEl = document.getElementById('statTotalTasks');
        if (statTotalEl) {
            statTotalEl.textContent = visibleCount;
        }

        // Show/hide no matching results banner
        if (noResultsAlert) {
            noResultsAlert.classList.toggle('d-none', visibleCount > 0);
        }
    }

    if (searchInput) {
        searchInput.addEventListener('input', applyCombinedFilter);
    }
    if (clearBtn) {
        clearBtn.addEventListener('click', () => {
            searchInput.value = '';
            applyCombinedFilter();
            searchInput.focus();
        });
    }
    if (companySelect) {
        companySelect.addEventListener('change', applyCombinedFilter);
    }
    if (employeeSelect) {
        employeeSelect.addEventListener('change', applyCombinedFilter);
    }
    if (prioritySelect) {
        prioritySelect.addEventListener('change', applyCombinedFilter);
    }
    if (statusSelect) {
        statusSelect.addEventListener('change', applyCombinedFilter);
    }
    if (resetFilterBtn) {
        resetFilterBtn.addEventListener('click', () => {
            if (searchInput) searchInput.value = '';
            if (companySelect) companySelect.value = '';
            if (employeeSelect) employeeSelect.value = '';
            if (prioritySelect) prioritySelect.value = '';
            if (statusSelect) statusSelect.value = '';

            // Reset active tab to 'all'
            filterTabBtns.forEach(b => b.classList.remove('active'));
            const allBtn = document.getElementById('filterTabAll');
            if (allBtn) allBtn.classList.add('active');
            currentTabFilter = 'all';

            applyCombinedFilter();
            showAppToast('Filters reset to default view', 'info');
        });
    }

    // Run initial filter on page load
    applyCombinedFilter();

    // Export to CSV
    if (exportBtn) {
        exportBtn.addEventListener('click', () => {
            exportTasksToCsv(rows);
        });
    }

    // Quick View Modal triggers
    document.querySelectorAll('.quick-view-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = btn.dataset.id;
            const name = btn.dataset.name;
            const desc = btn.dataset.desc || 'No description provided.';
            const comps = btn.dataset.companies ? btn.dataset.companies.split(', ') : [];
            const emps = btn.dataset.employees ? btn.dataset.employees.split(', ') : [];

            document.getElementById('modalTaskId').textContent = `#${id.padStart(3, '0')}`;
            document.getElementById('modalTaskName').textContent = name;
            document.getElementById('modalTaskDesc').textContent = desc;

            const modalCompsEl = document.getElementById('modalTaskCompanies');
            modalCompsEl.innerHTML = '';
            if (comps.length > 0 && comps[0] !== '') {
                comps.forEach(c => {
                    const span = document.createElement('span');
                    span.className = 'company-badge-chip';
                    span.innerHTML = `<i class="bi bi-buildings"></i> <span>${c}</span>`;
                    modalCompsEl.appendChild(span);
                });
            } else {
                modalCompsEl.innerHTML = '<span class="empty-badge-pill">No companies assigned</span>';
            }

            const modalEmpsEl = document.getElementById('modalTaskEmployees');
            modalEmpsEl.innerHTML = '';
            if (emps.length > 0 && emps[0] !== '') {
                emps.forEach(e => {
                    const initial = e ? e.charAt(0) : '?';
                    const colorClass = getAvatarColorClass(e);
                    const span = document.createElement('span');
                    span.className = 'employee-badge-chip';
                    span.innerHTML = `<span class="avatar-initial-circle ${colorClass}">${initial}</span> <span>${e}</span>`;
                    modalEmpsEl.appendChild(span);
                });
            } else {
                modalEmpsEl.innerHTML = '<span class="empty-badge-pill">No employees assigned</span>';
            }

            document.getElementById('modalEditLink').href = `/Task/Edit/${id}`;
            document.getElementById('modalDetailsLink').href = `/Task/Details/${id}`;

            const copyModalBtn = document.getElementById('modalCopySummaryBtn');
            if (copyModalBtn) {
                copyModalBtn.onclick = () => {
                    const text = `Task #${id}: ${name}\nDescription: ${desc}\nCompanies: ${comps.join(', ')}\nEmployees: ${emps.join(', ')}`;
                    navigator.clipboard.writeText(text).then(() => {
                        showAppToast('Task summary copied to clipboard!');
                    });
                };
            }

            const modalInstance = new bootstrap.Modal(document.getElementById('quickViewModal'));
            modalInstance.show();
        });
    });
}

function populateDropdownFilters(rows, companySelect, employeeSelect) {
    if (!companySelect || !employeeSelect) return;

    const companies = new Set();
    const employees = new Set();

    rows.forEach(row => {
        const comps = (row.dataset.companies || '').split(', ');
        comps.forEach(c => { if (c.trim()) companies.add(c.trim()); });

        const emps = (row.dataset.employees || '').split(', ');
        emps.forEach(e => { if (e.trim()) employees.add(e.trim()); });
    });

    Array.from(companies).sort().forEach(c => {
        const opt = document.createElement('option');
        opt.value = c;
        opt.textContent = c;
        companySelect.appendChild(opt);
    });

    Array.from(employees).sort().forEach(e => {
        const opt = document.createElement('option');
        opt.value = e;
        opt.textContent = e;
        employeeSelect.appendChild(opt);
    });
}

function exportTasksToCsv(rows) {
    if (!rows || rows.length === 0) {
        showAppToast('No tasks available to export.', 'warning');
        return;
    }

    let csvContent = 'Task ID,Task Name,Description,Companies,Assigned Employees\n';

    rows.forEach(row => {
        if (row.style.display === 'none') return; // Export visible/filtered only

        const id = `"${row.dataset.taskId || ''}"`;
        const name = `"${(row.dataset.taskName || '').replace(/"/g, '""')}"`;
        const desc = `"${(row.dataset.taskDesc || '').replace(/"/g, '""')}"`;
        const comps = `"${(row.dataset.companies || '').replace(/"/g, '""')}"`;
        const emps = `"${(row.dataset.employees || '').replace(/"/g, '""')}"`;

        csvContent += `${id},${name},${desc},${comps},${emps}\n`;
    });

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `tasks_export_${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    showAppToast('CSV export downloaded successfully!');
}

/* ==========================================================================
   4. Create & Edit Form Features (Live Preview, Multi-select Search & Helpers)
   ========================================================================== */
function initFormPageFeatures() {
    const inputTaskName = document.getElementById('inputTaskName');
    const inputDesc = document.getElementById('inputDescription');
    const selectCompanies = document.getElementById('selectCompanies');
    const selectEmployees = document.getElementById('selectEmployees');

    const previewTitle = document.getElementById('previewTaskTitle');
    const previewDesc = document.getElementById('previewTaskDesc');
    const previewComps = document.getElementById('previewCompaniesList');
    const previewEmps = document.getElementById('previewEmployeesList');

    const nameCount = document.getElementById('taskNameCount');
    const descCount = document.getElementById('descCharCount');
    const companyCountBadge = document.getElementById('companySelectCount');
    const employeeCountBadge = document.getElementById('employeeSelectCount');

    if (!inputTaskName || !selectCompanies || !selectEmployees) return;

    // Character Counter & Live Title Update
    inputTaskName.addEventListener('input', () => {
        const val = inputTaskName.value;
        if (nameCount) nameCount.textContent = `${val.length}/100`;
        if (previewTitle) {
            previewTitle.textContent = val.trim() ? val : 'Task Name';
        }
    });

    // Character Counter & Live Description Update
    if (inputDesc) {
        inputDesc.addEventListener('input', () => {
            const val = inputDesc.value;
            if (descCount) descCount.textContent = `${val.length}/500`;
            if (previewDesc) {
                previewDesc.textContent = val.trim() ? val : 'Task description will appear here as you type...';
            }
        });
    }

    // Company Multi-Select live synchronizer
    function updateCompaniesPreview() {
        const selected = Array.from(selectCompanies.selectedOptions);
        if (companyCountBadge) {
            companyCountBadge.textContent = `${selected.length} selected`;
        }

        if (previewComps) {
            previewComps.innerHTML = '';
            if (selected.length === 0) {
                previewComps.innerHTML = '<span class="empty-badge-pill">No companies selected</span>';
            } else {
                selected.forEach(opt => {
                    const span = document.createElement('span');
                    span.className = 'company-badge-chip';
                    span.innerHTML = `<i class="bi bi-buildings"></i> <span>${opt.text}</span>`;
                    previewComps.appendChild(span);
                });
            }
        }
    }

    // Employee Multi-Select live synchronizer
    function updateEmployeesPreview() {
        const selected = Array.from(selectEmployees.selectedOptions);
        if (employeeCountBadge) {
            employeeCountBadge.textContent = `${selected.length} selected`;
        }

        if (previewEmps) {
            previewEmps.innerHTML = '';
            if (selected.length === 0) {
                previewEmps.innerHTML = '<span class="empty-badge-pill">No team members selected</span>';
            } else {
                selected.forEach(opt => {
                    const initial = opt.text ? opt.text.charAt(0) : '?';
                    const colorClass = getAvatarColorClass(opt.text);
                    const span = document.createElement('span');
                    span.className = 'employee-badge-chip';
                    span.innerHTML = `<span class="avatar-initial-circle ${colorClass}">${initial}</span> <span>${opt.text}</span>`;
                    previewEmps.appendChild(span);
                });
            }
        }
    }

    selectCompanies.addEventListener('change', updateCompaniesPreview);
    selectEmployees.addEventListener('change', updateEmployeesPreview);

    // Filter inside Companies Select
    const companyFilterInput = document.getElementById('companyFilterInput');
    if (companyFilterInput) {
        companyFilterInput.addEventListener('input', (e) => {
            const q = e.target.value.toLowerCase();
            Array.from(selectCompanies.options).forEach(opt => {
                const match = opt.text.toLowerCase().includes(q);
                opt.hidden = !match;
            });
        });
    }

    // Filter inside Employees Select
    const employeeFilterInput = document.getElementById('employeeFilterInput');
    if (employeeFilterInput) {
        employeeFilterInput.addEventListener('input', (e) => {
            const q = e.target.value.toLowerCase();
            Array.from(selectEmployees.options).forEach(opt => {
                const match = opt.text.toLowerCase().includes(q);
                opt.hidden = !match;
            });
        });
    }

    // Select All / Clear All Buttons
    const selectAllCompsBtn = document.getElementById('selectAllCompaniesBtn');
    const clearAllCompsBtn = document.getElementById('clearAllCompaniesBtn');
    if (selectAllCompsBtn) {
        selectAllCompsBtn.addEventListener('click', () => {
            Array.from(selectCompanies.options).forEach(opt => {
                if (!opt.hidden) opt.selected = true;
            });
            updateCompaniesPreview();
        });
    }
    if (clearAllCompsBtn) {
        clearAllCompsBtn.addEventListener('click', () => {
            Array.from(selectCompanies.options).forEach(opt => {
                opt.selected = false;
            });
            updateCompaniesPreview();
        });
    }

    const selectAllEmpsBtn = document.getElementById('selectAllEmployeesBtn');
    const clearAllEmpsBtn = document.getElementById('clearAllEmployeesBtn');
    if (selectAllEmpsBtn) {
        selectAllEmpsBtn.addEventListener('click', () => {
            Array.from(selectEmployees.options).forEach(opt => {
                if (!opt.hidden) opt.selected = true;
            });
            updateEmployeesPreview();
        });
    }
    if (clearAllEmpsBtn) {
        clearAllEmpsBtn.addEventListener('click', () => {
            Array.from(selectEmployees.options).forEach(opt => {
                opt.selected = false;
            });
            updateEmployeesPreview();
        });
    }

    // Initial Trigger on load (for edit form prefill)
    if (inputTaskName.value) {
        if (nameCount) nameCount.textContent = `${inputTaskName.value.length}/100`;
    }
    if (inputDesc && inputDesc.value) {
        if (descCount) descCount.textContent = `${inputDesc.value.length}/500`;
    }
    updateCompaniesPreview();
    updateEmployeesPreview();
}

/* ==========================================================================
   5. Global Copy Summary Button
   ========================================================================== */
function initGlobalCopyButtons() {
    document.querySelectorAll('.copy-summary-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const name = btn.dataset.name || '';
            const desc = btn.dataset.desc || '';
            const summary = `Task: ${name}\nDescription: ${desc}`;

            navigator.clipboard.writeText(summary).then(() => {
                showAppToast('Task summary copied to clipboard!');
            }).catch(() => {
                showAppToast('Failed to copy to clipboard', 'warning');
            });
        });
    });
}

/**
 * Deterministically pick an avatar gradient color class based on name hash
 */
function getAvatarColorClass(name) {
    if (!name) return 'avatar-color-0';
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
        hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const idx = Math.abs(hash) % 7;
    return `avatar-color-${idx}`;
}

/* ==========================================================================
   6. Universal Search Box Clear Cross Symbol Handler
   ========================================================================== */
function initUniversalSearchClearButtons() {
    function setupInput(input) {
        if (!input || input.dataset.clearAttached === 'true') return;
        input.dataset.clearAttached = 'true';

        let container = input.closest('.input-group') || input.closest('.search-input-group') || input.parentElement;
        let existingBtn = container ? container.querySelector('.js-search-clear-btn, .search-clear-btn, a[title*="Clear"], button[title*="Clear"]') : null;

        if (!existingBtn && container) {
            const clearBtn = document.createElement('button');
            clearBtn.type = 'button';
            clearBtn.className = 'btn btn-link p-0 text-muted border-0 bg-transparent text-decoration-none search-clear-cross-btn me-1 d-none js-search-clear-btn';
            clearBtn.title = 'Clear search';
            clearBtn.innerHTML = '<i class="bi bi-x-circle-fill text-secondary opacity-75"></i>';
            clearBtn.style.cssText = 'font-size: 1.05rem; flex-shrink: 0; outline: none; box-shadow: none;';

            const submitBtn = container.querySelector('button[type="submit"], button.btn-primary, button.btn-royal-gold, button.btn-warning');
            if (submitBtn) {
                container.insertBefore(clearBtn, submitBtn);
            } else {
                container.appendChild(clearBtn);
            }
            existingBtn = clearBtn;
        }

        if (existingBtn) {
            const toggleVisibility = () => {
                const val = (input.value || '').trim();
                if (val.length > 0) {
                    existingBtn.classList.remove('d-none');
                } else {
                    existingBtn.classList.add('d-none');
                }
            };

            input.addEventListener('input', toggleVisibility);
            input.addEventListener('keyup', toggleVisibility);
            input.addEventListener('change', toggleVisibility);
            toggleVisibility();

            existingBtn.addEventListener('click', (e) => {
                e.preventDefault();
                input.value = '';
                toggleVisibility();
                input.focus();

                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));

                if (window.jQuery) {
                    window.$(input).trigger('input').trigger('change').trigger('keyup');
                }

                const form = input.closest('form');
                if (form && form.method.toLowerCase() === 'get') {
                    const urlParams = new URLSearchParams(window.location.search);
                    if (urlParams.has('search') || urlParams.has(input.name)) {
                        form.submit();
                    }
                }
            });
        }
    }

    const selector = 'input[name="search"], input[id*="search"], input[id*="Search"], input[placeholder*="Search"], input[placeholder*="search"]';
    document.querySelectorAll(selector).forEach(setupInput);

    const observer = new MutationObserver(() => {
        document.querySelectorAll(selector).forEach(setupInput);
    });
    observer.observe(document.body, { childList: true, subtree: true });
}

