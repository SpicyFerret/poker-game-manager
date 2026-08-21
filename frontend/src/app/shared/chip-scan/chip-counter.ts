/**
 * Chip-stack counting from a camera frame, entirely client-side.
 *
 * Two independent estimates, deliberately, because either one alone is easy
 * to fool:
 *
 *  1. **Rim peaks.** A stacked chip's rim is a horizontal brightness edge, so
 *     counting rims is finding local maxima in the stack's vertical
 *     brightness-gradient profile.
 *  2. **Proportion.** Seen side-on, a stack is exactly one chip wide and N
 *     chips tall, so `N ≈ (height / width) × (diameter / thickness)`. This
 *     needs no edge to be visible at all — only the stack's outline.
 *
 * The second is what makes the first trustworthy: it says roughly how far
 * apart two rims must be, which turns peak-finding from an open-ended count
 * (where a shadow or a logo becomes a chip) into a constrained one. When the
 * two estimates disagree, that disagreement is itself the useful output — the
 * caller says "not sure yet" instead of showing a confident wrong number.
 *
 * Everything here is typed against a minimal `RgbaImage` rather than the DOM
 * `ImageData`, so every function works identically on a real captured frame
 * and on a plain object built in a test, with no canvas involved either way.
 */

export interface RgbaImage {
  readonly width: number;
  readonly height: number;
  readonly data: Uint8ClampedArray;
}

/**
 * Diameter ÷ thickness for a standard 39mm × 3.3mm poker chip. Only ever a
 * starting point: a calibration step measures the real ratio of the chips in
 * front of the person, because ceramic, clay and cheap plastic chips are all
 * noticeably different thicknesses at the same diameter.
 */
export const DEFAULT_CHIP_RATIO = 39 / 3.3;

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

function luminanceAt(image: RgbaImage, x: number, y: number): number {
  const i = (y * image.width + x) * 4;

  return 0.299 * image.data[i] + 0.587 * image.data[i + 1] + 0.114 * image.data[i + 2];
}

/** Mean brightness over the whole frame, for the too-dark / too-bright warnings. */
export function meanLuminance(image: RgbaImage): number {
  const { width, height } = image;

  if (width === 0 || height === 0) {
    return 0;
  }

  let sum = 0;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      sum += luminanceAt(image, x, y);
    }
  }

  return sum / (width * height);
}

/**
 * Counts chip rims down a cropped stack. `minSpacing` comes from the
 * proportion estimate rather than being guessed here — that constraint is
 * the whole reason this stopped counting shadows as chips.
 */
export function countRims(image: RgbaImage, minSpacing: number): number {
  const { width, height } = image;

  if (width === 0 || height < 3) {
    return 0;
  }

  const luminance: number[] = new Array(height);
  for (let y = 0; y < height; y++) {
    let sum = 0;
    for (let x = 0; x < width; x++) {
      sum += luminanceAt(image, x, y);
    }
    luminance[y] = sum / width;
  }

  const rowProfile: number[] = new Array(height).fill(0);
  for (let y = 1; y < height - 1; y++) {
    rowProfile[y] = Math.abs(luminance[y + 1] - luminance[y - 1]);
  }

  const smoothed = movingAverage(rowProfile, 3);
  const maxSignal = Math.max(...smoothed, 1);

  return findPeaks(smoothed, Math.max(2, minSpacing), maxSignal * 0.15).length;
}

/**
 * How many chips a stack of this shape must contain, from its outline alone.
 * Independent of whether a single rim is actually visible, which is what
 * makes it a useful check on the rim count rather than a duplicate of it.
 */
export function estimateFromProportion(
  stackWidth: number,
  stackHeight: number,
  ratio: number,
): number {
  if (stackWidth <= 0 || stackHeight <= 0) {
    return 0;
  }

  return Math.round((stackHeight / stackWidth) * ratio);
}

export interface ColourCandidate {
  token: string;
  swatch: string;
}

export function hexToRgb(hex: string): [number, number, number] {
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
  image: RgbaImage,
  candidates: readonly ColourCandidate[],
): string | null {
  if (candidates.length === 0) {
    return null;
  }

  const { width, height, data } = image;
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

/** A span of rows or columns. */
export interface Span {
  start: number;
  end: number;
}

/**
 * The longest contiguous run of rows whose average colour is within
 * `tolerance` of `target` — the stack's vertical extent, as opposed to the
 * whole cropped frame. Without this the proportion estimate would measure the
 * guide box rather than the chips inside it.
 */
export function verticalExtent(
  image: RgbaImage,
  target: readonly [number, number, number],
  tolerance: number,
): Span {
  const { width, height, data } = image;
  let best: Span = { start: 0, end: 0 };
  let runStart: number | null = null;

  const closeRun = (end: number): void => {
    if (runStart !== null && end - runStart > best.end - best.start) {
      best = { start: runStart, end };
    }
    runStart = null;
  };

  for (let y = 0; y < height; y++) {
    let r = 0;
    let g = 0;
    let b = 0;

    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      r += data[i];
      g += data[i + 1];
      b += data[i + 2];
    }

    r /= width;
    g /= width;
    b /= width;

    const distance = Math.sqrt((r - target[0]) ** 2 + (g - target[1]) ** 2 + (b - target[2]) ** 2);

    if (distance <= tolerance) {
      if (runStart === null) {
        runStart = y;
      }
    } else {
      closeRun(y);
    }
  }

  closeRun(height);

  return best;
}

/**
 * Per-column variance of vertical luminance: high where chips are — their
 * rims create brightness banding running down the column — low over a
 * comparatively flat background (table felt, floor, a hand).
 */
export function columnActivity(image: RgbaImage): number[] {
  const { width, height } = image;
  const activity: number[] = new Array(width).fill(0);

  for (let x = 0; x < width; x++) {
    let sum = 0;
    let sumSquares = 0;

    for (let y = 0; y < height; y++) {
      const luminance = luminanceAt(image, x, y);
      sum += luminance;
      sumSquares += luminance * luminance;
    }

    const mean = sum / height;
    activity[x] = sumSquares / height - mean * mean;
  }

  return activity;
}

/**
 * Groups a column-activity signal into stack regions: runs of
 * above-`threshold` columns at least `minWidth` wide, tolerating gaps
 * narrower than `minGap` (a chip's own lighter rim can dip briefly) without
 * splitting the region, and closing it once a gap reaches `minGap`.
 */
export function segmentStacks(
  activity: readonly number[],
  threshold: number,
  minWidth: number,
  minGap: number,
): Span[] {
  const regions: Span[] = [];
  let regionStart: number | null = null;
  let gapRun = 0;

  const closeRegion = (end: number): void => {
    if (regionStart !== null && end - regionStart >= minWidth) {
      regions.push({ start: regionStart, end });
    }
    regionStart = null;
    gapRun = 0;
  };

  for (let x = 0; x < activity.length; x++) {
    if (activity[x] > threshold) {
      if (regionStart === null) {
        regionStart = x;
      }
      gapRun = 0;
    } else if (regionStart !== null) {
      gapRun++;
      if (gapRun >= minGap) {
        closeRegion(x - gapRun + 1);
      }
    }
  }

  closeRegion(activity.length - gapRun);

  return regions;
}

function cropColumns(image: RgbaImage, start: number, end: number): RgbaImage {
  const width = end - start;
  const height = image.height;
  const data = new Uint8ClampedArray(width * height * 4);

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const srcIndex = (y * image.width + (start + x)) * 4;
      const dstIndex = (y * width + x) * 4;

      data[dstIndex] = image.data[srcIndex];
      data[dstIndex + 1] = image.data[srcIndex + 1];
      data[dstIndex + 2] = image.data[srcIndex + 2];
      data[dstIndex + 3] = image.data[srcIndex + 3];
    }
  }

  return { width, height, data };
}

export interface AnalysedStack {
  columns: Span;
  rows: Span;
  colourToken: string | null;
  /** From the outline's proportions. */
  byProportion: number;
  /** From counting visible rims. */
  byRims: number;
  /** What to show. Null when the two estimates disagree too much to be worth showing. */
  quantity: number | null;
  /** True when the stack runs into the top or bottom of the frame, so its real height is unknown. */
  clipped: boolean;
}

/** Something specific and fixable about the current frame. */
export type FrameIssue =
  | 'too-dark'
  | 'too-bright'
  | 'no-stacks'
  | 'clipped'
  | 'too-small'
  | 'disagreement';

export interface FrameAnalysis {
  stacks: AnalysedStack[];
  issues: FrameIssue[];
}

/**
 * The two estimates are allowed to differ by whichever is larger: one chip,
 * or a tenth of the count. A tall stack legitimately loses a rim or two to
 * glare; a short one has no such excuse.
 */
function reconcile(byProportion: number, byRims: number): number | null {
  const tolerance = Math.max(1, Math.round(Math.max(byProportion, byRims) * 0.1));

  if (Math.abs(byProportion - byRims) > tolerance) {
    return null;
  }

  // The proportion estimate is the more robust of the two — it does not need
  // any individual rim to survive the lighting — so it wins ties.
  return byProportion > 0 ? byProportion : byRims;
}

/**
 * Analyses one camera frame: finds each stack, estimates it both ways, and
 * reports what is wrong with the frame in terms the person holding the phone
 * can act on. `colourCandidates` should be just the colours actually in play
 * (the chip set's own denominations), not the whole palette — with only a few
 * real candidates, a colour match is far less likely to land on the wrong one.
 */
export function analyseFrame(
  image: RgbaImage,
  colourCandidates: readonly ColourCandidate[],
  ratio: number = DEFAULT_CHIP_RATIO,
): FrameAnalysis {
  const issues: FrameIssue[] = [];
  const brightness = meanLuminance(image);

  if (brightness < 45) {
    issues.push('too-dark');
  } else if (brightness > 215) {
    issues.push('too-bright');
  }

  const activity = columnActivity(image);
  const maxActivity = Math.max(...activity, 0);

  // An absolute floor, not just a relative one. Over a flat surface the
  // variance is only floating-point dust, and a purely proportional
  // threshold would happily segment that dust into a full-width "stack".
  // Real stacked chips band strongly; this is a standard deviation of ~5
  // brightness levels, well under any genuine stack.
  if (maxActivity < 25) {
    return { stacks: [], issues: [...issues, 'no-stacks'] };
  }

  const regions = segmentStacks(
    activity,
    maxActivity * 0.12,
    Math.max(4, Math.floor(image.width / 40)),
    Math.max(2, Math.floor(image.width / 80)),
  );

  if (regions.length === 0) {
    return { stacks: [], issues: [...issues, 'no-stacks'] };
  }

  const stacks = regions.map((columns) => analyseStack(image, columns, colourCandidates, ratio));

  if (stacks.some((s) => s.clipped)) {
    issues.push('clipped');
  }
  // A stack narrower than this leaves too few pixels per chip for either
  // estimate to mean anything — the answer is to move the phone closer.
  if (stacks.some((s) => s.columns.end - s.columns.start < image.width / 12)) {
    issues.push('too-small');
  }
  if (stacks.some((s) => s.quantity === null)) {
    issues.push('disagreement');
  }

  return { stacks, issues };
}

function analyseStack(
  image: RgbaImage,
  columns: Span,
  colourCandidates: readonly ColourCandidate[],
  ratio: number,
): AnalysedStack {
  const cropped = cropColumns(image, columns.start, columns.end);
  const colourToken = matchDenominationColour(cropped, colourCandidates);

  const swatch = colourCandidates.find((c) => c.token === colourToken)?.swatch;
  const target = swatch ? hexToRgb(swatch) : ([128, 128, 128] as [number, number, number]);

  // Generous tolerance: a chip photographed under room light is nowhere near
  // its nominal swatch, and this only needs to separate chip from table.
  const rows = verticalExtent(cropped, target, 120);

  const stackWidth = columns.end - columns.start;
  const stackHeight = rows.end - rows.start;

  const byProportion = estimateFromProportion(stackWidth, stackHeight, ratio);
  // One chip's thickness in pixels, less a margin, so two adjacent rims stay
  // separable while a shadow within one chip does not become its own peak.
  const minSpacing = Math.max(2, Math.floor((stackWidth / ratio) * 0.6));
  const byRims = countRims(cropped, minSpacing);

  return {
    columns,
    rows,
    colourToken,
    byProportion,
    byRims,
    quantity: reconcile(byProportion, byRims),
    clipped: rows.start === 0 || rows.end === cropped.height,
  };
}

/**
 * The chip ratio implied by a stack the person has told us the true count of.
 * This is the calibration: everything else about a chip cancels out, leaving
 * only how tall N of them stand next to how wide one of them is.
 */
export function ratioFromKnownCount(
  stackWidth: number,
  stackHeight: number,
  knownCount: number,
): number | null {
  if (stackWidth <= 0 || stackHeight <= 0 || knownCount <= 0) {
    return null;
  }

  return (stackWidth / stackHeight) * knownCount;
}
