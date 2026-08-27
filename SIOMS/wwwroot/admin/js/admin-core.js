// SIOMS Admin Panel - Core JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // ===== Sidebar Toggle =====
    // Desktop: expands/collapses sidebar (icon-only mode)
    // Mobile: slides sidebar in/out from left
    const sidebarToggle = document.getElementById('sidebarToggle');
    const mainSidebar = document.getElementById('mainSidebar');
    const sidebarOverlay = document.getElementById('sidebarOverlay');
    const MOBILE_BREAKPOINT = 768;
    const SIDEBAR_STORAGE_KEY = 'sioms-sidebar-collapsed';

    function isMobile() {
        return window.innerWidth <= MOBILE_BREAKPOINT;
    }

    function closeMobileSidebar() {
        if (mainSidebar) {
            mainSidebar.classList.remove('sidebar-mobile-open');
        }
        if (sidebarOverlay) {
            sidebarOverlay.classList.remove('active');
        }
        document.body.classList.remove('sidebar-open');
    }

    function openMobileSidebar() {
        if (mainSidebar) {
            mainSidebar.classList.add('sidebar-mobile-open');
        }
        if (sidebarOverlay) {
            sidebarOverlay.classList.add('active');
        }
        document.body.classList.add('sidebar-open');
    }

    // Initialize sidebar state
    function initSidebar() {
        if (isMobile()) {
            // Mobile: sidebar hidden by default
            closeMobileSidebar();
        } else {
            // Desktop: restore collapsed state from localStorage
            try {
                var isCollapsed = localStorage.getItem(SIDEBAR_STORAGE_KEY) === '1';
                if (isCollapsed) {
                    document.body.classList.add('sidebar-collapse');
                } else {
                    document.body.classList.remove('sidebar-collapse');
                }
            } catch (err) {
                document.body.classList.remove('sidebar-collapse');
            }
        }
    }

    // Toggle sidebar
    if (sidebarToggle && mainSidebar) {
        sidebarToggle.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();

            if (isMobile()) {
                var isOpen = mainSidebar.classList.contains('sidebar-mobile-open');
                if (isOpen) {
                    closeMobileSidebar();
                } else {
                    openMobileSidebar();
                }
            } else {
                var isCollapsed = document.body.classList.toggle('sidebar-collapse');
                try {
                    localStorage.setItem(SIDEBAR_STORAGE_KEY, isCollapsed ? '1' : '0');
                } catch (err) {
                    // localStorage unavailable
                }
            }
        });
    }

    // Close mobile overlay sidebar when clicking the backdrop
    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', closeMobileSidebar);
    }

    // Close mobile sidebar when clicking a menu link (navigation)
    if (mainSidebar) {
        mainSidebar.querySelectorAll('a.nav-link:not([href="#"])').forEach(function(link) {
            link.addEventListener('click', function() {
                if (isMobile()) {
                    closeMobileSidebar();
                }
            });
        });
    }

    // Handle window resize
    var resizeTimer;
    window.addEventListener('resize', function() {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function() {
            if (!isMobile()) {
                closeMobileSidebar();
                try {
                    var isCollapsed = localStorage.getItem(SIDEBAR_STORAGE_KEY) === '1';
                    if (isCollapsed) {
                        document.body.classList.add('sidebar-collapse');
                    } else {
                        document.body.classList.remove('sidebar-collapse');
                    }
                } catch (err) {
                    document.body.classList.remove('sidebar-collapse');
                }
            } else {
                closeMobileSidebar();
                document.body.classList.remove('sidebar-collapse');
            }
        }, 200);
    });

    // ===== Accordion Menus - Enhanced =====
    // Handle treeview toggles for Products, Orders, Reports
    const treeviewLinks = document.querySelectorAll('.nav-sidebar .has-treeview > .nav-link');

    treeviewLinks.forEach(function(link) {
        link.addEventListener('click', function(e) {
            // Don't navigate - just toggle the submenu
            e.preventDefault();
            e.stopPropagation();

            var parentLi = this.closest('.has-treeview');
            if (!parentLi) return;

            // On mobile, only toggle if sidebar is open
            if (isMobile()) {
                var isOpen = mainSidebar.classList.contains('sidebar-mobile-open');
                if (!isOpen) {
                    // If sidebar is closed on mobile, open it first
                    openMobileSidebar();
                    // Then toggle after a small delay
                    setTimeout(function() {
                        toggleTreeview(parentLi);
                    }, 300);
                    return;
                }
            }

            toggleTreeview(parentLi);
        });
    });

    function toggleTreeview(parentLi) {
        // Close other open menus (single open at a time)
        var siblings = parentLi.parentElement.querySelectorAll('.has-treeview');
        siblings.forEach(function(sibling) {
            if (sibling !== parentLi && sibling.classList.contains('menu-open')) {
                sibling.classList.remove('menu-open');
            }
        });

        // Toggle this menu
        parentLi.classList.toggle('menu-open');
    }

    // ===== Auto-collapse submenus after navigation =====
    // When a submenu item is clicked, collapse its parent after navigation
    document.querySelectorAll('.nav-treeview .nav-link:not([href="#"])').forEach(function(link) {
        link.addEventListener('click', function() {
            var parentTreeview = this.closest('.has-treeview');
            if (parentTreeview) {
                // Store reference to collapse after navigation
                var linkHref = this.getAttribute('href');
                // Only collapse if it's a real link (not # or javascript:)
                if (linkHref && linkHref !== '#' && !linkHref.startsWith('javascript:')) {
                    setTimeout(function() {
                        if (!isMobile()) {
                            // On desktop, collapse after navigation
                            parentTreeview.classList.remove('menu-open');
                        }
                    }, 300);
                }
            }
        });
    });

    // ===== Auto-open active menu on page load =====
    // If a submenu item is active, ensure its parent is open
    function initActiveMenu() {
        var activeSubmenuItem = document.querySelector('.nav-treeview .nav-link.active');
        if (activeSubmenuItem) {
            var parentTreeview = activeSubmenuItem.closest('.has-treeview');
            if (parentTreeview) {
                parentTreeview.classList.add('menu-open');
            }
        }
    }
    initActiveMenu();

    // ===== Auto-dismiss Alerts =====
    setTimeout(function() {
        document.querySelectorAll('.alert:not(.alert-permanent)').forEach(function(alert) {
            var bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        });
    }, 5000);

    // ===== Tooltips =====
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function(tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // ===== Popovers =====
    var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    popoverTriggerList.map(function(popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl);
    });

    // ===== Confirm Delete =====
    document.querySelectorAll('form.delete-confirm-form').forEach(function(form) {
        form.addEventListener('submit', function(e) {
            var message = form.dataset.message || 'Are you sure you want to delete this item?';
            if (!confirm(message)) {
                e.preventDefault();
            }
        });
    });

    document.querySelectorAll('a.btn-delete-confirm').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.preventDefault();
            var message = this.dataset.message || 'Are you sure you want to delete this item?';
            var url = this.href;
            if (confirm(message) && url) {
                window.location.href = url;
            }
        });
    });

    // ===== Select All Checkbox =====
    var selectAllCheckbox = document.getElementById('selectAll');
    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener('change', function() {
            var checkboxes = document.querySelectorAll('.select-item');
            checkboxes.forEach(function(cb) {
                cb.checked = selectAllCheckbox.checked;
            });
        });
    }

    // ===== Table Row Click =====
    document.querySelectorAll('.clickable-row').forEach(function(row) {
        row.addEventListener('click', function() {
            var url = this.dataset.url;
            if (url) {
                window.location.href = url;
            }
        });
    });

    // ===== Notification Function =====
    function showNotification(message, type) {
        var alertDiv = document.createElement('div');
        alertDiv.className = 'alert alert-' + type + ' alert-dismissible fade show position-fixed top-0 end-0 m-3';
        alertDiv.style.zIndex = '9999';
        alertDiv.style.maxWidth = '400px';
        alertDiv.innerHTML = `
            <i class="fas fa-${type === 'success' ? 'check-circle' : 'exclamation-circle'} me-2"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        document.body.appendChild(alertDiv);

        setTimeout(function() {
            if (alertDiv.parentNode) {
                alertDiv.parentNode.removeChild(alertDiv);
            }
        }, 5000);
    }

    // ===== Loading Button State =====
    document.querySelectorAll('.btn-loading').forEach(function(btn) {
        btn.addEventListener('click', function() {
            var originalText = this.innerHTML;
            this.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i> Loading...';
            this.disabled = true;

            setTimeout(function() {
                btn.innerHTML = originalText;
                btn.disabled = false;
            }, 3000);
        });
    });

    // ===== Cancel button redirect fix =====
    document.querySelectorAll('.cancel-btn, .btn-cancel').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            var returnUrl = this.dataset.returnUrl || '/';
            if (this.href && this.href !== '#') {
                return;
            }
            e.preventDefault();
            window.location.href = returnUrl;
        });
    });
});