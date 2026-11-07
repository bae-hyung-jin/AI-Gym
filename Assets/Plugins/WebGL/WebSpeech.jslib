mergeInto(LibraryManager.library, {
  // WebGL 빌드에서 브라우저 내장 음성 합성(Web Speech API)을 사용해 즉시 한국어 음성을 재생한다.
  // 네트워크 다운로드가 없고 CORS 차단이 전혀 없어 0초 지연으로 확실하게 소리가 난다.
  WebSpeechSpeak: function (textPtr) {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;
    var text = UTF8ToString(textPtr);
    if (!text || text.length === 0) return;
    try {
      window.speechSynthesis.cancel();
      var u = new SpeechSynthesisUtterance(text);
      u.lang = 'ko-KR';
      u.rate = 1.0;
      u.pitch = 1.0;
      window.speechSynthesis.speak(u);
    } catch (e) {
      console.warn('[WebSpeech] 발화 실패:', e);
    }
  },

  WebSpeechStop: function () {
    if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
      try {
        window.speechSynthesis.cancel();
      } catch (e) {}
    }
  }
});
