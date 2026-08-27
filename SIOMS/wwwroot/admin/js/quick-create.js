/*
 * SIOMS - Quick Create Searchable Dropdown
 * ------------------------------------------------------------------
 * Turns a normal <select> into a searchable, AJAX-backed dropdown with
 * an inline "Create '<name>'" option when no match is found.
 *
 * Usage: give the wrapping <div> the class "qc-select-wrapper" plus:
 *   data-quick-endpoint      = URL of the QuickCreate POST action
 *   data-quick-label         = Friendly label used in prompts (e.g. "Customer")
 *   data-name-field          = form field name to POST for the entity name (default: "name")
 *
 * The inline create form always has a Name field. It can optionally show one
 * extra ("secondary") field, configured with:
 *   data-secondary-field     = form field name to POST (default: "phone")
 *   data-secondary-label     = label shown above the field (default: "Contact Number")
 *   data-secondary-type      = "tel" | "text" | "textarea" (default: "tel")
 *   data-secondary-required  = "true" to make the secondary field mandatory (default: "false")
 *   data-secondary-intl      = "true" to use the standard international phone widget
 *                               (intl-tel-input) for type="tel" (default: "true" for tel)
 *
 * Backward-compatible aliases (still honoured if the newer attributes are absent):
 *   data-phone-field         = same as data-secondary-field
 *
 * Examples:
 *   Supplier on an Order page (contact required once a supplier is created):
 *     data-quick-endpoint="/Supplier/QuickCreate" data-quick-label="Supplier"
 *     data-secondary-field="phone" data-secondary-label="Contact Number"
 *     data-secondary-type="tel" data-secondary-required="true"
 *
 *   Category on the Product page (optional description, no phone):
 *     data-quick-endpoint="/Category/QuickCreate" data-quick-label="Category"
 *     data-secondary-field="description" data-secondary-label="Description (optional)"
 *     data-secondary-type="textarea" data-secondary-required="false"
 *
 * Inside the wrapper there must be exactly one <select class="qc-native-select">
 * (the real form field, kept in the DOM so existing submit/validation logic
 * and any "change" listeners on it keep working untouched).
 *
 * No page refresh: everything here runs over fetch()/AJAX.
 */
(function () {
    'use strict';

    var qcInstanceCounter = 0;

    // Standard international phone widget settings - matches the implementation
    // already used on the normal Supplier/Customer/Purchaser Create & Edit pages.
    var ITI_OPTIONS = {
        initialCountry: 'pk',
        preferredCountries: ['pk', 'us', 'gb', 'ae', 'in', 'sa', 'ca', 'au'],
        separateDialCode: true,
        utilsScript: 'https://cdn.jsdelivr.net/npm/intl-tel-input@18.2.1/build/js/utils.js',
        autoPlaceholder: 'polite',
        formatOnDisplay: true,
        allowDropdown: true,
        nationalMode: false
    };

    function getAntiForgeryToken(root) {
        var tokenInput = (root.closest('form') || document).querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function closeAllDropdowns(except) {
        document.querySelectorAll('.qc-dropdown-list.qc-open').forEach(function (el) {
            if (el !== except) {
                el.classList.remove('qc-open');
            }
        });
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }

    function initWidget(wrapper) {
        var nativeSelect = wrapper.querySelector('.qc-native-select');
        if (!nativeSelect) return;

        qcInstanceCounter += 1;
        var instanceId = qcInstanceCounter;

        var endpoint = wrapper.getAttribute('data-quick-endpoint');
        var label = wrapper.getAttribute('data-quick-label') || 'Item';
        var nameField = wrapper.getAttribute('data-name-field') || 'name';
        var allowQuickCreate = !!endpoint;

        // ----- Secondary field configuration (phone / description / etc.) -----
        var secondaryField = wrapper.getAttribute('data-secondary-field') ||
            wrapper.getAttribute('data-phone-field') || 'phone';
        var secondaryLabel = wrapper.getAttribute('data-secondary-label') || 'Contact Number';
        var secondaryType = (wrapper.getAttribute('data-secondary-type') || 'tel').toLowerCase();
        var secondaryRequired = wrapper.getAttribute('data-secondary-required') === 'true';
        var secondaryIntlAttr = wrapper.getAttribute('data-secondary-intl');
        var secondaryUseIntl = secondaryType === 'tel' && secondaryIntlAttr !== 'false';

        // Build the visible search input + dropdown list
        var searchWrap = document.createElement('div');
        searchWrap.className = 'qc-input-group';

        var searchInput = document.createElement('input');
        searchInput.type = 'text';
        searchInput.className = nativeSelect.className.replace('qc-native-select', '').trim() || 'form-control';
        searchInput.classList.add('qc-search-input');
        searchInput.setAttribute('autocomplete', 'off');
        searchInput.setAttribute('placeholder', 'Search ' + label + '...');

        var dropdownList = document.createElement('div');
        dropdownList.className = 'qc-dropdown-list';

        searchWrap.appendChild(searchInput);
        searchWrap.appendChild(dropdownList);
        wrapper.insertBefore(searchWrap, nativeSelect);
        nativeSelect.classList.add('d-none');

        function readOptions() {
            return Array.from(nativeSelect.querySelectorAll('option')).filter(function (o) {
                return o.value !== '';
            });
        }

        function syncDisplayFromSelect() {
            var opt = nativeSelect.options[nativeSelect.selectedIndex];
            searchInput.value = (opt && opt.value !== '') ? opt.text : '';
        }

        function selectValue(value, text) {
            nativeSelect.value = value;
            searchInput.value = text;
            var evt = new Event('change', { bubbles: true });
            nativeSelect.dispatchEvent(evt);
            closeList();
        }

        function closeList() {
            dropdownList.classList.remove('qc-open');
            dropdownList.innerHTML = '';
        }

        function renderList(filterText) {
            dropdownList.innerHTML = '';
            var filter = (filterText || '').trim().toLowerCase();
            var options = readOptions();
            var matches = options.filter(function (o) {
                return o.text.toLowerCase().indexOf(filter) !== -1;
            });

            if (matches.length === 0) {
                var emptyItem = document.createElement('div');
                emptyItem.className = 'qc-dropdown-item qc-dropdown-empty text-muted';
                emptyItem.textContent = 'No ' + label.toLowerCase() + ' found';
                dropdownList.appendChild(emptyItem);
            } else {
                matches.forEach(function (o) {
                    var item = document.createElement('div');
                    item.className = 'qc-dropdown-item';
                    item.textContent = o.text;
                    item.dataset.value = o.value;
                    item.addEventListener('mousedown', function (e) {
                        e.preventDefault();
                        selectValue(o.value, o.text);
                    });
                    dropdownList.appendChild(item);
                });
            }

            var exactMatch = options.some(function (o) {
                return o.text.toLowerCase() === filter;
            });

            if (allowQuickCreate && filter.length > 0 && !exactMatch) {
                var createItem = document.createElement('div');
                createItem.className = 'qc-dropdown-item qc-create-item';
                createItem.innerHTML = '<i class="fas fa-plus-circle me-1"></i> Create "' + escapeHtml(filterText.trim()) + '"';
                createItem.addEventListener('mousedown', function (e) {
                    e.preventDefault();
                    showInlineCreateForm(filterText.trim());
                });
                dropdownList.appendChild(createItem);
            }

            dropdownList.classList.add('qc-open');
        }

        function buildSecondaryFieldMarkup(secondaryInputId, secondaryFeedbackId) {
            var requiredMark = secondaryRequired ? ' *' : '';
            var labelHtml = '<label class="form-label small mb-1">' + escapeHtml(secondaryLabel) + requiredMark + '</label>';

            if (secondaryType === 'textarea') {
                return '' +
                    '<div class="mb-2">' +
                    '  ' + labelHtml +
                    '  <textarea class="form-control form-control-sm qc-new-secondary" id="' + secondaryInputId + '" rows="2"></textarea>' +
                    '</div>';
            }

            if (secondaryType === 'tel') {
                return '' +
                    '<div class="mb-2">' +
                    '  ' + labelHtml +
                    '  <div class="phone-input-wrapper">' +
                    '    <input type="tel" class="form-control form-control-sm qc-new-secondary phone-input" id="' + secondaryInputId + '" autocomplete="off">' +
                    '    <div id="' + secondaryFeedbackId + '" class="phone-feedback small mt-1"></div>' +
                    '  </div>' +
                    '</div>';
            }

            // plain text fallback
            return '' +
                '<div class="mb-2">' +
                '  ' + labelHtml +
                '  <input type="text" class="form-control form-control-sm qc-new-secondary" id="' + secondaryInputId + '">' +
                '</div>';
        }

        function initSecondaryPhoneWidget(secondaryInput, secondaryFeedbackId) {
            if (!secondaryUseIntl || typeof window.intlTelInput !== 'function') {
                return null;
            }

            var iti = window.intlTelInput(secondaryInput, ITI_OPTIONS);
            secondaryInput._iti = iti;

            function feedbackEl() {
                return document.getElementById(secondaryFeedbackId);
            }

            function validatePhone() {
                var value = secondaryInput.value.trim();
                var feedback = feedbackEl();

                if (value === '') {
                    secondaryInput.classList.remove('is-invalid', 'is-valid');
                    if (feedback) feedback.textContent = '';
                    return !secondaryRequired;
                }

                if (iti.isValidNumber()) {
                    secondaryInput.classList.remove('is-invalid');
                    secondaryInput.classList.add('is-valid');
                    if (feedback) {
                        feedback.textContent = '✓ Valid phone number';
                        feedback.className = 'phone-feedback small mt-1 text-success';
                    }
                    return true;
                }

                secondaryInput.classList.remove('is-valid');
                secondaryInput.classList.add('is-invalid');
                if (feedback) {
                    feedback.textContent = 'Please enter a valid phone number for the selected country.';
                    feedback.className = 'phone-feedback small mt-1 text-danger';
                }
                return false;
            }

            // Restrict input to digits and plus sign only
            secondaryInput.addEventListener('keypress', function (e) {
                var char = String.fromCharCode(e.which);
                if (!/[\d+]/.test(char) && e.which !== 8 && e.which !== 46 && e.which !== 37 && e.which !== 39) {
                    e.preventDefault();
                }
            });

            secondaryInput.addEventListener('paste', function (e) {
                var pasted = (e.clipboardData || window.clipboardData).getData('text');
                var cleaned = pasted.replace(/[^0-9+]/g, '');
                if (cleaned !== pasted) {
                    e.preventDefault();
                    var start = this.selectionStart;
                    var end = this.selectionEnd;
                    var value = this.value;
                    this.value = value.substring(0, start) + cleaned + value.substring(end);
                    this.selectionStart = this.selectionEnd = start + cleaned.length;
                    var event = new Event('input', { bubbles: true });
                    this.dispatchEvent(event);
                }
            });

            secondaryInput.addEventListener('blur', function () {
                if (iti.isValidNumber()) {
                    this.value = iti.getNumber();
                }
                validatePhone();
            });

            secondaryInput.addEventListener('input', function () {
                secondaryInput.classList.remove('is-invalid', 'is-valid');
                var feedback = feedbackEl();
                if (feedback) feedback.textContent = '';
            });

            secondaryInput.addEventListener('countrychange', validatePhone);

            iti.validate = validatePhone;
            return iti;
        }

        function showInlineCreateForm(prefillName) {
            dropdownList.innerHTML = '';
            dropdownList.classList.add('qc-open');

            var secondaryInputId = 'qcSecondary_' + instanceId;
            var secondaryFeedbackId = secondaryInputId + 'Feedback';

            var form = document.createElement('div');
            form.className = 'qc-inline-create-form p-2';
            form.innerHTML =
                '<div class="mb-2">' +
                '  <label class="form-label small mb-1">' + escapeHtml(label) + ' Name *</label>' +
                '  <input type="text" class="form-control form-control-sm qc-new-name" value="' + escapeHtml(prefillName || '') + '">' +
                '</div>' +
                buildSecondaryFieldMarkup(secondaryInputId, secondaryFeedbackId) +
                '<div class="qc-inline-error text-danger small mb-2" style="display:none;"></div>' +
                '<div class="d-flex justify-content-end gap-2">' +
                '  <button type="button" class="btn btn-sm btn-secondary qc-cancel-btn">Cancel</button>' +
                '  <button type="button" class="btn btn-sm btn-primary qc-save-btn"><i class="fas fa-save me-1"></i>Save</button>' +
                '</div>';

            dropdownList.appendChild(form);

            var nameInput = form.querySelector('.qc-new-name');
            var secondaryInput = form.querySelector('.qc-new-secondary');
            var errorBox = form.querySelector('.qc-inline-error');
            var saveBtn = form.querySelector('.qc-save-btn');
            var cancelBtn = form.querySelector('.qc-cancel-btn');

            var phoneWidget = secondaryType === 'tel' ? initSecondaryPhoneWidget(secondaryInput, secondaryFeedbackId) : null;

            nameInput.focus();
            nameInput.select();

            cancelBtn.addEventListener('mousedown', function (e) {
                e.preventDefault();
                closeList();
            });

            saveBtn.addEventListener('mousedown', function (e) {
                e.preventDefault();
                var newName = nameInput.value.trim();
                if (!newName) {
                    errorBox.textContent = label + ' name is required';
                    errorBox.style.display = 'block';
                    nameInput.focus();
                    return;
                }

                // Validate / normalize the secondary field before saving.
                var secondaryValue = secondaryInput ? secondaryInput.value.trim() : '';

                if (secondaryType === 'tel' && phoneWidget) {
                    if (secondaryValue === '' && secondaryRequired) {
                        errorBox.textContent = secondaryLabel + ' is required';
                        errorBox.style.display = 'block';
                        secondaryInput.focus();
                        return;
                    }
                    if (secondaryValue !== '' && !phoneWidget.isValidNumber()) {
                        errorBox.textContent = 'Please enter a valid ' + secondaryLabel.toLowerCase() + ' for the selected country.';
                        errorBox.style.display = 'block';
                        secondaryInput.classList.add('is-invalid');
                        secondaryInput.focus();
                        return;
                    }
                    if (secondaryValue !== '') {
                        // Use the fully formatted international number for submission.
                        secondaryValue = phoneWidget.getNumber();
                    }
                } else if (secondaryRequired && secondaryValue === '') {
                    errorBox.textContent = secondaryLabel + ' is required';
                    errorBox.style.display = 'block';
                    secondaryInput.focus();
                    return;
                }

                saveBtn.disabled = true;
                saveBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Saving...';
                errorBox.style.display = 'none';

                var body = new URLSearchParams();
                body.append(nameField, newName);
                body.append(secondaryField, secondaryValue);

                fetch(endpoint, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                        'RequestVerificationToken': getAntiForgeryToken(wrapper),
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: body.toString()
                })
                    .then(function (res) { return res.json(); })
                    .then(function (data) {
                        if (data.success) {
                            var newOption = document.createElement('option');
                            newOption.value = data.id;
                            newOption.text = data.name;
                            nativeSelect.appendChild(newOption);
                            selectValue(String(data.id), data.name);
                        } else {
                            errorBox.textContent = data.message || ('Could not create ' + label.toLowerCase());
                            errorBox.style.display = 'block';
                            saveBtn.disabled = false;
                            saveBtn.innerHTML = '<i class="fas fa-save me-1"></i>Save';
                        }
                    })
                    .catch(function () {
                        errorBox.textContent = 'Network error - please try again';
                        errorBox.style.display = 'block';
                        saveBtn.disabled = false;
                        saveBtn.innerHTML = '<i class="fas fa-save me-1"></i>Save';
                    });
            });
        }

        searchInput.addEventListener('focus', function () {
            closeAllDropdowns(dropdownList);
            renderList(searchInput.value);
        });

        searchInput.addEventListener('input', function () {
            renderList(searchInput.value);
        });

        searchInput.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeList();
                searchInput.blur();
            }
        });

        document.addEventListener('click', function (e) {
            if (!wrapper.contains(e.target)) {
                if (dropdownList.classList.contains('qc-open') && !dropdownList.querySelector('.qc-inline-create-form')) {
                    // Only auto-close plain list clicks-away; inline form has its own cancel
                    closeList();
                    syncDisplayFromSelect();
                } else if (dropdownList.classList.contains('qc-open')) {
                    closeList();
                    syncDisplayFromSelect();
                }
            }
        });

        // Initialize display with any preselected value (e.g. validation redisplay)
        syncDisplayFromSelect();
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.qc-select-wrapper').forEach(initWidget);
    });
})();
