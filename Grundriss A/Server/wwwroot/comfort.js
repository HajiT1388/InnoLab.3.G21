const clamp = (x, a, b) => Math.min(b, Math.max(a, x));

function badCO2(ppm){
  const p = clamp(ppm, 0, 3000);
  if (p <= 600) return 0;
  const z = clamp((p - 600) / 1400, 0, 1.6);
  return clamp(1 - Math.exp(-3.5 * Math.pow(z, 3.0)), 0, 1);
}

function badRH(rh){
  const h = clamp(rh, 0, 100);
  if (h >= 30 && h <= 50) return 0;

  if (h < 30){
    const d = 30 - h;
    const z = clamp(d / 15, 0, 2.0);
    return clamp(1 - Math.exp(-3.0 * Math.pow(z, 3.0)), 0, 1);
  } else {
    const d = h - 50;
    const z = clamp(d / 20, 0, 2.0);
    return clamp(1 - Math.exp(-2.3 * Math.pow(z, 3.0)), 0, 1);
  }
}

function badTempC(t){
  const x = clamp(t, 6, 40);
  const d = Math.abs(x - 22.5);

  const z1 = clamp(Math.max(0, d - 2.5) / 10, 0, 2.0);
  const z2 = clamp(Math.max(0, d - 5.5) / 10, 0, 2.0);

  const base = 1 - Math.exp(-1.8 * Math.pow(z1, 2.0));
  const extra = 1 - Math.exp(-2.2 * Math.pow(z2, 3.0));

  return clamp(1 - (1 - base) * (1 - extra), 0, 1);
}

function badPressure(p){
  const x = clamp(p, 950, 1070);
  const d = Math.abs(x - 1013);
  if (d <= 15) return 0;
  const z = clamp((d - 15) / 60, 0, 2.0);
  return clamp(1 - Math.exp(-1.6 * Math.pow(z, 2.3)), 0, 0.85);
}

export function computeWellbeing(inputs){

  const enabled = inputs.enabled ?? { co2:true, temp:false, rh:true, pres:true };

  const weights = { co2: 0.50, rh: 0.25, temp: 0.20, pres: 0.05 };
  const P = 2.2;
  const K = 2.9;

  const parts = {};
  const reasons = [];

  let sumW = 0;
  let acc = 0;

  const pushPart = (key, value, badFn, describeFn) => {
    const b = clamp(badFn(value), 0, 1);
    const w = weights[key] ?? 0;
    sumW += w;
    acc += w * Math.pow(b, P);
    parts[key] = { bad: b, score: Math.round(100 * (1 - b)) };
    const msg = describeFn(value, b);
    if (msg) reasons.push(msg);
  };

  if (enabled.co2){
    const v = clamp(Number(inputs.co2 ?? 0), 0, 3000);
    pushPart("co2", v, badCO2, (ppm, b) => {
      if (ppm < 800) return "CO2 ist normal.";
      if (ppm < 1200) return "CO2 ist erhöht.";
      if (ppm < 1800) return "CO2 ist deutlich erhöht.";
      return "CO2 ist sehr hoch.";
    });
  }

  if (enabled.rh){
    const v = clamp(Number(inputs.rh ?? 0), 0, 100);
    pushPart("rh", v, badRH, (rh, b) => {
      if (rh >= 30 && rh <= 50) return "Luftfeuchtigkeit ist normal.";
      if ((rh >= 25 && rh < 30) || (rh > 50 && rh <= 60)) return "Luftfeuchtigkeit ist leicht ungünstig.";
      if ((rh >= 20 && rh < 25) || (rh > 60 && rh <= 70)) return "Luftfeuchtigkeit ist ungünstig.";
      return "Luftfeuchtigkeit ist sehr ungünstig.";
    });
  }

  if (enabled.temp){
    const v = clamp(Number(inputs.temp ?? 0), 6, 40);
    pushPart("temp", v, badTempC, (t, b) => {
      if (t >= 20 && t <= 26) return "Temperatur ist im angenehmen Bereich.";
      if ((t >= 18 && t < 20) || (t > 26 && t <= 28)) return "Temperatur ist außerhalb des angenehmen Bereichs.";
      if ((t >= 15 && t < 18) || (t > 28 && t <= 30)) return "Temperatur ist deutlich außerhalb des Komfortbereichs.";
      return "Temperatur ist extrem.";
    });
  }

  if (enabled.pres){
    const v = clamp(Number(inputs.pres ?? 1013), 950, 1070);
    pushPart("pres", v, badPressure, (p, b) => {
      if (Math.abs(p - 1013) <= 15) return "Luftdruck ist normal.";
      if (Math.abs(p - 1013) <= 35) return "Luftdruck abweichend.";
      return "Luftdruck stark abweichend.";
    });
  }

  if (sumW <= 0){
    return {
      score: 0,
      risk: 1,
      label: "-",
      hint: "Keine Werte aktiv.",
      parts: {},
      reasons: ["Keine Werte aktiv."]
    };
  }

  const normalized = acc / sumW;
  const risk = clamp(1 - Math.exp(-K * normalized), 0, 1);
  const score = Math.round(clamp(100 * (1 - risk), 0, 100));

  let label = "Gute Luftqualität";
  let hint = "Werte im Optimalbereich.";
  if (score < 40){ label = "Gefahr"; hint = "Werte im schädlichen Bereich."; }
  else if (score < 60){ label = "Warnung"; hint = "Werte spürbar suboptimal."; }
  else if (score < 75){ label = "OK"; hint = "In Ordnung, aber nicht ideal."; }

  return { score, risk, label, hint, parts, reasons };
}

export function computeWellbeingScore(co2, temp, rh, pres, enabled){
  return computeWellbeing({ co2, temp, rh, pres, enabled }).score;
}
