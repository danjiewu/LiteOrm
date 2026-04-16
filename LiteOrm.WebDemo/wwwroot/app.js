(function () {
    const storageKeys = {
        token: "liteorm-demo-token",
        user: "liteorm-demo-user"
    };

    function getToken() {
        return localStorage.getItem(storageKeys.token) || "";
    }

    function getUser() {
        const text = localStorage.getItem(storageKeys.user);
        return text ? JSON.parse(text) : null;
    }

    function saveSession(token, user) {
        localStorage.setItem(storageKeys.token, token);
        localStorage.setItem(storageKeys.user, JSON.stringify(user));
    }

    function clearSession() {
        localStorage.removeItem(storageKeys.token);
        localStorage.removeItem(storageKeys.user);
    }

    async function apiFetch(url, options) {
        const finalOptions = options || {};
        const headers = { ...(finalOptions.headers || {}) };
        const token = getToken();
        if (token) {
            headers.Authorization = `Bearer ${token}`;
        }

        const response = await fetch(url, { ...finalOptions, headers });
        const text = await response.text();
        const data = text ? JSON.parse(text) : null;
        if (!response.ok) {
            throw new Error(data?.error || data?.title || text || `HTTP ${response.status}`);
        }
        return data;
    }

    function setStatus(id, message, kind) {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = message;
        el.className = `status${kind ? ` ${kind}` : ""}`;
    }

    function renderJson(id, data) {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = JSON.stringify(data, null, 2);
    }

    function getObjectValue(item, fieldName) {
        if (!item || !fieldName) {
            return "";
        }

        if (Object.prototype.hasOwnProperty.call(item, fieldName)) {
            return item[fieldName];
        }

        const camelCaseName = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
        if (Object.prototype.hasOwnProperty.call(item, camelCaseName)) {
            return item[camelCaseName];
        }

        const matchedKey = Object.keys(item).find(key => key.toLowerCase() === fieldName.toLowerCase());
        return matchedKey ? item[matchedKey] : "";
    }

    function renderOrders(id, items) {
        const el = document.getElementById(id);
        if (!el) return;
        if (!items || items.length === 0) {
            el.innerHTML = '<tr><td colspan="7" class="subtle">没有数据</td></tr>';
            return;
        }

        el.innerHTML = items.map(item => `
            <tr>
                <td>${getObjectValue(item, "orderNo")}</td>
                <td>${getObjectValue(item, "status")}</td>
                <td>${getObjectValue(item, "customerName")}</td>
                <td>${getObjectValue(item, "productName")}</td>
                <td>${getObjectValue(item, "totalAmount")}</td>
                <td>${getObjectValue(item, "createdByUserName")}</td>
                <td>${getObjectValue(item, "departmentName")}</td>
            </tr>
        `).join("");
    }

    function renderSessionBox(id) {
        const el = document.getElementById(id);
        if (!el) return;
        const token = getToken();
        const user = getUser();
        el.textContent = token
            ? `令牌：${token}\n用户：${user?.displayName ?? user?.userName ?? "未命名"}\n角色：${user?.role ?? "未知"}`
            : "尚未登录";
    }

    function requireLogin(id) {
        if (!getToken()) {
            setStatus(id, "请先在登录页完成登录。", "error");
            return false;
        }
        return true;
    }

    window.demoApp = {
        apiFetch,
        clearSession,
        getToken,
        getObjectValue,
        getUser,
        renderJson,
        renderOrders,
        renderSessionBox,
        requireLogin,
        saveSession,
        setStatus
    };
})();
