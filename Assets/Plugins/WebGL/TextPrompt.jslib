mergeInto(LibraryManager.library, {
  // 브라우저 입력창(한글 입력 가능)을 띄워 글을 받는다. 취소하면 빈 글을 돌려준다.
  PromptInput: function (msgPtr, defPtr) {
    var r = window.prompt(UTF8ToString(msgPtr), UTF8ToString(defPtr));
    if (r === null) r = '';
    var size = lengthBytesUTF8(r) + 1;
    var buf = _malloc(size);
    stringToUTF8(r, buf, size);
    return buf;
  }
});
