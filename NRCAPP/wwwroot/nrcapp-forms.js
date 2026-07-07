(function () {
    const qs = (selector) => document.querySelector(selector);
    const value = (selector) => (qs(selector)?.value || "").trim();

    function showMessage(text, ok) {
        const box = qs("#admin-login-message");
        if (!box) { return; }
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
        if (!response.ok) { throw new Error(data.message || "تعذر تنفيذ العملية على الخادم."); }
        return data;
    }

    function showPlanResult(text, ok) {
        const box = qs("#plan-result");
        if (!box) { return; }
        box.hidden = false;
        box.textContent = text;
        box.classList.toggle("success", ok);
        box.classList.toggle("warning", !ok);
    }

    function wirePlanForm() {
        const button = qs("#plan-submit-button");
        const form = qs("#distribution-form");
        if (!button || !form) { return; }

        button.addEventListener("click", async (event) => {
            if (window.Blazor) { return; }
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

    window.handleAdminLogin = async function () {
        try {
            const data = await postJson("/api/auth/admin", {
                username: value("#admin-username"),
                password: value("#admin-password")
            });
            window.location.assign("/admin/dashboard");
        } catch (error) {
            showMessage(error.message, false);
        }
    };

    window.handleAdminLogout = async function () {
        await postJson("/api/auth/admin/logout", {});
        window.location.assign("/admin/login");
    };

    function initializeForms() {
        wirePlanForm();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initializeForms);
    } else {
        initializeForms();
    }
})();
