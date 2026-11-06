mergeInto(LibraryManager.library, {
  // 브라우저에서 PNG를 내려받게 한다
  DownloadFile: function (namePtr, dataPtr, size) {
    var name = UTF8ToString(namePtr);
    var bytes = new Uint8Array(HEAPU8.buffer, dataPtr, size);
    var a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([bytes], { type: 'image/png' }));
    a.download = name;
    document.body.appendChild(a); a.click(); document.body.removeChild(a);
  }
});
