mergeInto(LibraryManager.library, {
    WebGLRequestClipboardText: function () {
        if (navigator.clipboard) {
            navigator.clipboard.readText()
                .then(text => {
                    unityInstance.SendMessage('ClipboardManager', 'ReceiveClipboardText', text);
                })
                .catch(err => {
                    console.error('Failed to read clipboard: ', err);
                });
        } else {
            console.warn('Clipboard API not available');
        }
    },
});