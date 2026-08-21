import {
  ColourCandidate,
  countChipsInStack,
  findPeaks,
  matchDenominationColour,
} from './chip-counter';

/** A flat ImageData-like object without needing a real canvas in the test environment. */
function fakeImageData(width: number, height: number, pixel: (x: number, y: number) => [number, number, number]): ImageData {
  const data = new Uint8ClampedArray(width * height * 4);

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const [r, g, b] = pixel(x, y);
      const i = (y * width + x) * 4;
      data[i] = r;
      data[i + 1] = g;
      data[i + 2] = b;
      data[i + 3] = 255;
    }
  }

  return { width, height, data, colorSpace: 'srgb' } as ImageData;
}

describe('findPeaks', () => {
  it('finds nothing in a flat signal', () => {
    expect(findPeaks([1, 1, 1, 1, 1], 1, 0.1)).toEqual([]);
  });

  it('finds a single clean peak', () => {
    const signal = [0, 0, 1, 5, 1, 0, 0];

    const peaks = findPeaks(signal, 2, 1);

    expect(peaks).toEqual([{ index: 3, prominence: 5 }]);
  });

  it('finds evenly spaced peaks, one per bump', () => {
    // Five bumps of height 10, troughs at 0 — a stylised stack of five rims.
    const signal = [0, 10, 0, 10, 0, 10, 0, 10, 0, 10, 0];

    const peaks = findPeaks(signal, 1, 5);

    expect(peaks.map((p) => p.index)).toEqual([1, 3, 5, 7, 9]);
  });

  it('collapses two peaks closer than minSpacing into the taller one', () => {
    const signal = [0, 4, 3, 5, 0];

    const peaks = findPeaks(signal, 3, 1);

    expect(peaks).toEqual([{ index: 3, prominence: 5 }]);
  });

  it('drops peaks below minProminence as noise', () => {
    const signal = [0, 0.5, 0, 10, 0];

    const peaks = findPeaks(signal, 1, 2);

    expect(peaks.map((p) => p.index)).toEqual([3]);
  });
});

describe('countChipsInStack', () => {
  it('reports zero for an empty or degenerate image', () => {
    expect(countChipsInStack(fakeImageData(0, 0, () => [0, 0, 0])).count).toBe(0);
    expect(countChipsInStack(fakeImageData(10, 2, () => [0, 0, 0])).count).toBe(0);
  });

  it('reports zero for a uniform image (no chips, just background)', () => {
    const image = fakeImageData(20, 60, () => [120, 120, 120]);

    expect(countChipsInStack(image).count).toBe(0);
  });

  it('counts one peak per shade change in a striped stack', () => {
    // Six contiguous stripes, alternating shade — a caricature of six
    // stacked chips, each boundary between two chips showing up as one
    // step in brightness. Five internal boundaries, so five peaks.
    const stripeHeight = 20;
    const stripeCount = 6;
    const height = stripeHeight * stripeCount;

    const image = fakeImageData(20, height, (_x, y) => {
      const stripe = Math.floor(y / stripeHeight);
      const shade = stripe % 2 === 0 ? 220 : 60;
      return [shade, shade, shade];
    });

    const result = countChipsInStack(image);

    expect(result.count).toBe(stripeCount - 1);
    expect(result.confidence).toBeGreaterThan(0);
  });
});

describe('matchDenominationColour', () => {
  const candidates: ColourCandidate[] = [
    { token: 'red', swatch: '#c62828' },
    { token: 'blue', swatch: '#1565c0' },
    { token: 'black', swatch: '#212121' },
  ];

  it('returns null with no candidates to match against', () => {
    const image = fakeImageData(10, 10, () => [255, 0, 0]);

    expect(matchDenominationColour(image, [])).toBeNull();
  });

  it('picks the nearest candidate to a solid-colour stack', () => {
    const redImage = fakeImageData(10, 30, () => [198, 40, 40]);

    expect(matchDenominationColour(redImage, candidates)).toBe('red');
  });

  it('is not fooled by background outside the sampled middle band', () => {
    // Bright red top/bottom thirds (background bleed), solid blue middle
    // third (the actual stack) — only the middle band is sampled.
    const image = fakeImageData(10, 30, (_x, y) => (y < 10 || y >= 20 ? [255, 0, 0] : [21, 101, 192]));

    expect(matchDenominationColour(image, candidates)).toBe('blue');
  });
});
