mergeInto(LibraryManager.library, {
    SuperfishialSyncSave: function () {
        FS.syncfs(false, function (error) {
            if (error)
                console.error("Superfishial save persistence failed:", error);
        });
    }
});
