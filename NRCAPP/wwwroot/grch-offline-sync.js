window.grchOfflineSync = (() => {
    const dbName = "grch-offline";
    const storeName = "sync-packets";

    function openDb() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(dbName, 1);

            request.onupgradeneeded = () => {
                const db = request.result;
                if (!db.objectStoreNames.contains(storeName)) {
                    db.createObjectStore(storeName, { keyPath: "localDeviceActionId" });
                }
            };

            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    async function enqueue(payload) {
        const db = await openDb();
        const packet = {
            localDeviceActionId: crypto.randomUUID(),
            payloadJson: JSON.stringify(payload),
            timestamp: new Date().toISOString()
        };

        await new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, "readwrite");
            transaction.objectStore(storeName).put(packet);
            transaction.oncomplete = resolve;
            transaction.onerror = () => reject(transaction.error);
        });

        return packet;
    }

    async function readAll() {
        const db = await openDb();
        return await new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, "readonly");
            const request = transaction.objectStore(storeName).getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    async function remove(localDeviceActionId) {
        const db = await openDb();
        await new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, "readwrite");
            transaction.objectStore(storeName).delete(localDeviceActionId);
            transaction.oncomplete = resolve;
            transaction.onerror = () => reject(transaction.error);
        });
    }

    async function flush() {
        if (!navigator.onLine) {
            return { synced: 0, pending: (await readAll()).length };
        }

        const packets = await readAll();
        let synced = 0;

        for (const packet of packets) {
            const response = await fetch("/api/sync/queue", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(packet)
            });

            if (!response.ok) {
                break;
            }

            await remove(packet.localDeviceActionId);
            synced++;
        }

        return { synced, pending: packets.length - synced };
    }

    window.addEventListener("online", () => {
        flush();
    });

    return {
        enqueue,
        flush,
        isOnline: () => navigator.onLine,
        pending: async () => (await readAll()).length
    };
})();
