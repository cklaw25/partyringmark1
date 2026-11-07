// The explainable-AI panel ("THE BROKER: LIVE AGENT STATUS") + the post-mortem screen.
const $ = (id) => document.getElementById(id);
const esc = (s) => String(s ?? '').replace(/[&<>]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]));

export class Panel {
  link(status) {
    $('p-link').innerHTML = status === 'online' ? '<span class="ok">online (Claude strategist)</span>' : '<span class="bad">offline (fallback rules)</span>';
  }

  refresh({ state, strategy, events, profile, fastMs, link }) {
    $('p-state').textContent = state;
    $('p-strategy').textContent = strategy ? `${strategy.action}${strategy.target ? ' → ' + strategy.target : ''}` : '-';
    $('p-events').innerHTML = events.slice().reverse().map(e => `<li><span class="dim">${e.t}s</span> ${esc(describe(e))}</li>`).join('');
    const bars = [
      ['Running tendency', profile.running_frequency],
      ['Trust in noises', profile.noise_trust],
      [`Prefers zone: ${profile.preferred_zone || '?'}`, profile.preferred_zone_confidence],
    ];
    $('p-profile').innerHTML = bars.map(([l, v]) => `<div class="bar"><span class="l">${esc(l)}</span><div class="track"><div class="fill" style="width:${Math.round((v || 0) * 100)}%"></div></div><span>${Math.round((v || 0) * 100)}%</span></div>`).join('')
      + `<div class="dim">playstyle: <b>${profile.playstyle}</b> · hides ${profile.hiding_count} (fav ${profile.favourite_locker || '-'}) · throws ${profile.distractions_thrown}, ${profile.distractions_detected} seen through · fav door ${profile.preferred_door || '-'} (${profile.preferred_door_uses}) · captures ${profile.capture_count}</div>`;
    const last = link.last(), p95 = link.p95();
    $('p-latency').innerHTML = `fast path (game loop): <b>${fastMs.toFixed(2)} ms</b>/frame<br>strategist round trip: <b>${last ?? '-'} ms</b> (p95 ${p95 ?? '-'} ms)`;
  }

  decision(r) {
    $('p-prediction').innerHTML = `${esc(r.prediction || '-')} <span class="dim">(confidence ${Math.round((r.confidence || 0) * 100)}%)</span>`;
    $('p-decision').innerHTML = `<b class="${r.valid ? 'ok' : 'bad'}">${esc(r.action)}${r.target ? ' → ' + esc(r.target) : ''}</b> <span class="dim">[${esc(r.source)}${r.latency_ms ? ', ' + r.latency_ms + ' ms' : ''}]</span><br>${esc(r.reason || '')}${r.note ? '<br><span class="dim">engine: ' + esc(r.note) + '</span>' : ''}`;
    $('p-outcome').textContent = 'pending...';
  }
  outcome(r) { $('p-outcome').textContent = `${r.action}: ${r.outcome}`; }
}

export function describe(e) {
  switch (e.event) {
    case 'door_used': return `used ${e.door} (${e.zone}) x${e.times}`;
    case 'player_hiding': return `hid in ${e.hiding_place}${e.seen_entering ? ' (SEEN)' : ''}`;
    case 'object_thrown': return `threw a bottle into ${e.landing_zone}`;
    case 'item_collected': return `collected ${e.item}`;
    case 'player_spotted': return `spotted player in ${e.zone}`;
    case 'player_captured': return `CAPTURED player (${e.how})`;
    case 'distraction_detected': return `saw through a distraction (trust ${Math.round(e.noise_trust * 100)}%)`;
    case 'power_restored': return 'power restored (loud)';
    case 'player_running': return `running in ${e.zone}`;
    case 'noise': return `heard a real-world noise (${e.source})`;
    default: return e.event;
  }
}

export function showPostMortem(won, mem, elapsed, onRestart) {
  const p = mem.profile();
  const decisions = mem.decisions;
  const good = decisions.filter(d => /SUCCESS|partial/.test(d.outcome || '')).length;
  const deception = mem.throws ? Math.round(100 * (mem.throws - mem.distractionsCaught) / mem.throws) : 0;
  const key = mem.log.filter(e => ['player_spotted', 'player_captured', 'item_collected', 'power_restored', 'player_hiding', 'distraction_detected', 'broker_decision', 'player_escaped', 'game_over'].includes(e.event));
  const tl = key.map(e => e.event === 'broker_decision'
    ? `<div><span class="t">${e.t}s</span> <b style="color:#e04848">BROKER → ${esc(e.action)}${e.target ? ' ' + esc(e.target) : ''}</b> <span class="dim">because ${esc(e.reason || '')}; outcome: ${esc(e.outcome)}</span></div>`
    : `<div><span class="t">${e.t}s</span> ${esc(describe(e))}</div>`).join('');
  const html = `<div class="card">
    <h1>${won ? 'ESCAPED' : 'CAPTURED'}: INCIDENT POST-MORTEM</h1>
    <p class="tag">${won ? 'You got out. THE BROKER has filed a report.' : 'THE BROKER has filed a report on how it beat you.'}</p>
    <p><b>Survival time:</b> ${Math.round(elapsed)} s · <b>Captures:</b> ${p.capture_count} · <b>Your playstyle:</b> ${p.playstyle} · <b>Deception score:</b> ${deception}% · <b>BROKER decisions that worked:</b> ${good}/${decisions.length}</p>
    <p><b>What it learned about you:</b> runs ${Math.round(p.running_frequency * 100)}% of the time · favourite zone ${p.preferred_zone || '-'} · favourite door ${p.preferred_door || '-'} · favourite locker ${p.favourite_locker || '-'} · trust in your noises ended at ${Math.round(p.noise_trust * 100)}%</p>
    <h3>Incident timeline</h3>
    <div class="tl">${tl}</div>
    <button id="again">PLAY AGAIN (same maze)</button>
  </div>`;
  const ov = $('overlay');
  ov.innerHTML = html; ov.classList.add('show');
  $('again').onclick = onRestart;
}
