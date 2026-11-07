// THE BROKER's voice. Browser speech for now (instant, free). ElevenLabs streaming can replace speak() later.
export class Voice {
  constructor() {
    this.synth = window.speechSynthesis;
    this.voice = null;
    const pickVoice = () => {
      const vs = this.synth?.getVoices() || [];
      this.voice = vs.find(v => /en-GB/i.test(v.lang) && /male|george|daniel|ryan/i.test(v.name)) || vs.find(v => /en-GB/i.test(v.lang)) || vs[0] || null;
    };
    pickVoice();
    if (this.synth) this.synth.onvoiceschanged = pickVoice;
  }
  speak(text, kind) {
    if (!this.synth) return;
    if (kind === 'strategist') this.synth.cancel();      // strategy lines interrupt reflex lines
    else if (this.synth.speaking) return;                // reflex lines never queue up
    const u = new SpeechSynthesisUtterance(text);
    if (this.voice) u.voice = this.voice;
    u.pitch = 0.5; u.rate = 0.95; u.volume = 1;
    this.synth.speak(u);
  }
}
