(function () {
    const qs = (selector) => document.querySelector(selector);
    const value = (selector) => (qs(selector)?.value || "").trim();

    function showMessage(text, ok) {
        const box = qs("#entry-message");
        if (!box) {
            return;
        }

        box.hidden = false;
        box.textContent = text;
        box.classList.toggle("success", ok);
        box.classList.toggle("warning", !ok);
    }

    async function postJson(url, payload) {
        const response = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const text = await response.text();
        const data = text ? JSON.parse(text) : {};

        if (!response.ok) {
            throw new Error(data.message || "تعذر تنفيذ العملية على الخادم.");
        }

        return data;
    }

    function switchRole(role) {
        document.querySelectorAll("[data-role-target]").forEach((button) => {
            button.classList.toggle("active", button.dataset.roleTarget === role);
        });
        document.querySelectorAll("[data-role-panel]").forEach((panel) => {
            panel.hidden = panel.dataset.rolePanel !== role;
        });
        const box = qs("#entry-message");
        if (box) {
            box.hidden = true;
        }
    }

    function switchMode(mode) {
        const group = qs(`[data-mode-target="${mode}"]`)?.closest(".auth-mode");
        if (!group) {
            return;
        }

        group.querySelectorAll("[data-mode-target]").forEach((button) => {
            button.classList.toggle("active", button.dataset.modeTarget === mode);
        });

        const rolePanel = group.closest("[data-role-panel]");
        rolePanel?.querySelectorAll("[data-mode-panel]").forEach((panel) => {
            panel.hidden = panel.dataset.modePanel !== mode;
        });
    }

    async function handleEntryAction(action) {
        try {
            if (action === "register-org") {
                const data = await postJson("/api/auth/organization/register", {
                    ngoName: value("#org-register-name"),
                    licenseId: value("#org-register-license"),
                    authorizedPerson: value("#org-register-person"),
                    passcode: value("#org-register-passcode")
                });
                window.location.assign(`/org/dashboard?orgId=${data.actorId}`);
                return;
            }

            if (action === "login-org") {
                const data = await postJson("/api/auth/organization", {
                    licenseId: value("#org-login-license"),
                    passcode: value("#org-login-passcode")
                });
                window.location.assign(`/org/dashboard?orgId=${data.actorId}`);
                return;
            }

            if (action === "register-citizen") {
                const nationalId = value("#citizen-register-national");
                const sector = value("#citizen-register-sector");
                await postJson("/api/auth/individual/register", {
                    fullName: value("#citizen-register-name"),
                    nationalId,
                    familyMembersCount: Number(value("#citizen-register-family")),
                    phoneNumber: value("#citizen-register-phone"),
                    currentSector: sector
                });
                window.location.assign(`/citizen/profile?nationalId=${encodeURIComponent(nationalId)}&sector=${encodeURIComponent(sector)}`);
                return;
            }

            if (action === "login-citizen") {
                const nationalId = value("#citizen-login-national");
                const sector = value("#citizen-login-sector");
                await postJson("/api/auth/individual", { nationalId });
                window.location.assign(`/citizen/profile?nationalId=${encodeURIComponent(nationalId)}&sector=${encodeURIComponent(sector)}`);
                return;
            }

            if (action === "login-admin") {
                await postJson("/api/auth/admin", {
                    username: value("#admin-username"),
                    password: value("#admin-password")
                });
                window.location.assign("/admin/dashboard");
            }
        } catch (error) {
            showMessage(error.message, false);
        }
    }

    function wireEntryGateway() {
        if (!qs("[data-entry-gateway]")) {
            return;
        }

        document.querySelectorAll("[data-role-target]:not([onclick])").forEach((button) => {
            button.addEventListener("click", () => switchRole(button.dataset.roleTarget));
        });
        document.querySelectorAll("[data-mode-target]:not([onclick])").forEach((button) => {
            button.addEventListener("click", () => switchMode(button.dataset.modeTarget));
        });
        document.querySelectorAll("[data-action]:not([onclick])").forEach((button) => {
            button.addEventListener("click", () => handleEntryAction(button.dataset.action));
        });
    }

    function showPlanResult(text, ok) {
        const box = qs("#plan-result");
        if (!box) {
            return;
        }

        box.hidden = false;
        box.textContent = text;
        box.classList.toggle("success", ok);
        box.classList.toggle("warning", !ok);
    }

    function wirePlanForm() {
        const button = qs("#plan-submit-button");
        const form = qs("#distribution-form");
        if (!button || !form) {
            return;
        }

        button.addEventListener("click", async (event) => {
            if (window.Blazor) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();

            try {
                const data = await postJson("/api/distribution-plans", {
                    aidType: value("#plan-aid-type"),
                    scheduledDate: value("#plan-scheduled-date"),
                    latitude: 31.501,
                    longitude: 34.466,
                    targetSector: value("#plan-sector"),
                    quantity: Number(value("#plan-quantity")),
                    organizationId: Number(form.dataset.orgId),
                    maxBeneficiaryCapacity: Number(value("#plan-capacity"))
                });
                showPlanResult(data.message || `تم حفظ الخطة رقم ${data.planId}.`, data.accepted);
                window.setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                showPlanResult(error.message, false);
            }
        }, true);
    }

    window.nrcappForms = {
        switchRole,
        switchMode,
        handleEntryAction
    };

    function initializeForms() {
        wireEntryGateway();
        wirePlanForm();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initializeForms);
    } else {
        initializeForms();
    }
})();
