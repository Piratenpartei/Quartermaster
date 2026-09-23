window.qmDownload = {
    blob: function (bytes, fileName, contentType) {
        const blob = new Blob([bytes], { type: contentType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }
};

window.qmPdfPreview = {
    _urls: {},
    show: function (iframe, bytes) {
        if (!iframe) {
            return;
        }
        const blob = new Blob([bytes], { type: "application/pdf" });
        const url = URL.createObjectURL(blob);
        const prev = this._urls[iframe.id];
        if (prev) {
            URL.revokeObjectURL(prev);
        }
        this._urls[iframe.id] = url;
        iframe.src = url;
    }
};
