/**
 * Chip-stack counting from a photo, entirely client-side.
 *
 * No ML model and no external CV library: a stacked chip's rim shows up as a
 * horizontal brightness edge, so counting a stack reduces to finding local
 * maxima in the stack's vertical brightness-gradient profile — a 1-D
 * peak-finding problem, plain enough to write and test directly against
 * Canvas `ImageData`. The result is always shown to the person for
 * confirmation before it reaches any form, so an approximate count here is a
 * convenience, never a source of truth.
 */

/** One local maximum in a 1-D signal: its position and how far it stands above its neighbourhood. */
export interface Peak {
  index: number;
  prominence: number;
}

/**
 * Finds local maxima in `signal`, each at least `minSpacing` apart and
 * standing at least `minProminence` above the higher of its two flanking
 * troughs (topographic prominence, computed by scanning outward from the
 * peak until a taller point — or the signal's edge — is reached).
 *
 * Pure and signal-agnostic on purpose: testable with synthetic arrays,
 * without a canvas or a real photo anywhere near it.
 */
export function findPeaks(
  signal: readonly number[],
  minSpacing: number,
  minProminence: number,
): Peak[] {
  const candidates: Peak[] = [];

  for (let i = 1; i < signal.length - 1; i++) {
    if (signal[i] < signal[i - 1] || signal[i] < signal[i + 1]) {
      continue;
    }

    let leftTrough = signal[i];
    for (let j = i - 1; j >= 0 && signal[j] <= signal[i]; j--) {
      leftTrough = Math.min(leftTrough, signal[j]);
    }

    let rightTrough = signal[i];
    for (let j = i + 1; j < signal.length && signal[j] <= signal[i]; j++) {
      rightTrough = Math.min(rightTrough, signal[j]);
    }

    candidates.push({ index: i, prominence: signal[i] - Math.max(leftTrough, rightTrough) });
  }

  // Tallest first, so a strong peak claims its exclusion zone before a
  // smaller neighbour gets a chance to.
  candidates.sort((a, b) => b.prominence - a.prominence);

  const accepted: Peak[] = [];
  for (const candidate of candidates) {
    if (candidate.prominence < minProminence) {
      continue;
    }
    if (accepted.some((p) => Math.abs(p.index - candidate.index) < minSpacing)) {
      continue;
    }
    accepted.push(candidate);
  }

  return accepted.sort((a, b) => a.index - b.index);
}

function movingAverage(signal: readonly number[], window: number): number[] {
  const half = Math.floor(window / 2);

  return signal.map((_, i) => {
    let sum = 0;
    let count = 0;

    for (let j = i - half; j <= i + half; j++) {
      if (j >= 0 && j < signal.length) {
        sum += signal[j];
        count++;
      }
    }

    return sum / count;
  });
}

export interface StackScan {
  count: number;
  /** 0..1, rough — how clean the peak pattern was. Never trusted blindly. */
  confidence: number;
}

/**
 * Counts chips in a side-on photo of a single stack. Converts to greyscale,
 * takes a vertical gradient (each row's brightness step from the row two
 * above it), averages across the width to get one profile per row, smooths
 * it, then counts peaks — one per visible chip rim.
 */
export function countChipsInStack(imageData: ImageData): StackScan {
  const { width, height, data } = imageData;

  if (width === 0 || height < 3) {
    return { count: 0, confidence: 0 };
  }

  const luminance: number[] = new Array(height);

  for (let y = 0; y < height; y++) {
    let sum = 0;

    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      sum += 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    }

    luminance[y] = sum / width;
  }

  const rowProfile: number[] = new Array(height).fill(0);
  for (let y = 1; y < height - 1; y++) {
    rowProfile[y] = Math.abs(luminance[y + 1] - luminance[y - 1]);
  }

  const smoothed = movingAverage(rowProfile, 3);
  const maxSignal = Math.max(...smoothed, 1);

  // A stack of N chips has roughly one rim every height/N pixels; without
  // knowing N up front, a fraction of the frame height is the closest thing
  // to a reasonable minimum spacing between two distinct chips.
  const minSpacing = Math.max(2, Math.floor(height / 60));
  const minProminence = maxSignal * 0.15;

  const peaks = findPeaks(smoothed, minSpacing, minProminence);

  return {
    count: peaks.length,
    confidence: peaks.length === 0 ? 0 : Math.min(1, peaks.length / 20),
  };
}

export interface ColourCandidate {
  token: string;
  swatch: string;
}

function hexToRgb(hex: string): [number, number, number] {
  const clean = hex.replace('#', '');
  const value = parseInt(clean, 16);

  return [(value >> 16) & 255, (value >> 8) & 255, value & 255];
}

/**
 * The stack's average colour, matched to the nearest candidate by distance
 * in RGB space. Samples the middle third of the frame vertically — away from
 * the top and bottom, where the background behind the stack is most likely
 * to bleed into the average.
 */
export function matchDenominationColour(
  imageData: ImageData,
  candidates: readonly ColourCandidate[],
): string | null {
  if (candidates.length === 0) {
    return null;
  }

  const { width, height, data } = imageData;
  const yStart = Math.floor(height / 3);
  const yEnd = Math.floor((height * 2) / 3);

  let r = 0;
  let g = 0;
  let b = 0;
  let count = 0;

  for (let y = yStart; y < yEnd; y++) {
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      r += data[i];
      g += data[i + 1];
      b += data[i + 2];
      count++;
    }
  }

  if (count === 0) {
    return null;
  }

  r /= count;
  g /= count;
  b /= count;

  let best: { token: string; distance: number } | null = null;

  for (const candidate of candidates) {
    const [cr, cg, cb] = hexToRgb(candidate.swatch);
    const distance = (r - cr) ** 2 + (g - cg) ** 2 + (b - cb) ** 2;

    if (!best || distance < best.distance) {
      best = { token: candidate.token, distance };
    }
  }

  return best?.token ?? null;
}

export interface ChipScanEstimate {
  quantity: number;
  colourToken: string | null;
  confidence: number;
}

/** Runs both estimators over one captured frame. */
export function scanChipStack(
  imageData: ImageData,
  colourCandidates: readonly ColourCandidate[],
): ChipScanEstimate {
  const { count, confidence } = countChipsInStack(imageData);
  const colourToken = matchDenominationColour(imageData, colourCandidates);

  return { quantity: count, colourToken, confidence };
}
