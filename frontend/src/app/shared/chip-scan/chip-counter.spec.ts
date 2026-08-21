import {
  ColourCandidate,
  DEFAULT_CHIP_RATIO,
  RgbaImage,
  analyseFrame,
  columnActivity,
  countRims,
  estimateFromProportion,
  findPeaks,
  matchDenominationColour,
  meanLuminance,
  ratioFromKnownCount,
  segmentStacks,
  verticalExtent,
} from './chip-counter';

/** A flat RgbaImage without needing a real canvas in the test environment. */
function fakeImage(
  width: number,
  height: number,
  pixel: (x: number, y: number) => [number, number, number],
): RgbaImage {
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

  return { width, height, data };
}

describe('findPeaks', () => {
  it('finds nothing in a flat signal', () => {
    expect(findPeaks([1, 1, 1, 1, 1], 1, 0.1)).toEqual([]);
  });

  it('finds a single clean peak', () => {
    expect(findPeaks([0, 0, 1, 5, 1, 0, 0], 2, 1)).toEqual([{ index: 3, prominence: 5 }]);
  });

  it('finds evenly spaced peaks, one per bump', () => {
    const signal = [0, 10, 0, 10, 0, 10, 0, 10, 0, 10, 0];

    expect(findPeaks(signal, 1, 5).map((p) => p.index)).toEqual([1, 3, 5, 7, 9]);
  });

  it('collapses two peaks closer than minSpacing into the taller one', () => {
    expect(findPeaks([0, 4, 3, 5, 0], 3, 1)).toEqual([{ index: 3, prominence: 5 }]);
  });

  it('drops peaks below minProminence as noise', () => {
    expect(findPeaks([0, 0.5, 0, 10, 0], 1, 2).map((p) => p.index)).toEqual([3]);
  });
});

describe('meanLuminance', () => {
  it('is zero for an empty image', () => {
    expect(meanLuminance(fakeImage(0, 0, () => [0, 0, 0]))).toBe(0);
  });

  it('reads a flat grey back as its own brightness', () => {
    expect(Math.round(meanLuminance(fakeImage(4, 4, () => [100, 100, 100])))).toBe(100);
  });
});

describe('estimateFromProportion', () => {
  /**
   * The core geometric claim: a stack is one chip wide and N chips tall, so
   * its aspect ratio alone gives N once the chip's own ratio is known.
   */
  it('derives the count from the stack outline', () => {
    // 10 chips of standard proportion: height = 10 thicknesses, width = 1 diameter.
    const width = 100;
    const height = (width / DEFAULT_CHIP_RATIO) * 10;

    expect(estimateFromProportion(width, height, DEFAULT_CHIP_RATIO)).toBe(10);
  });

  it('scales with the stack height, not the absolute pixel size', () => {
    const small = estimateFromProportion(50, 50, DEFAULT_CHIP_RATIO);
    const large = estimateFromProportion(200, 200, DEFAULT_CHIP_RATIO);

    expect(small).toBe(large);
  });

  it('is zero for a degenerate outline', () => {
    expect(estimateFromProportion(0, 100, DEFAULT_CHIP_RATIO)).toBe(0);
    expect(estimateFromProportion(100, 0, DEFAULT_CHIP_RATIO)).toBe(0);
  });
});

describe('ratioFromKnownCount', () => {
  it('recovers the ratio the proportion estimate would have needed', () => {
    const width = 100;
    const height = 250;
    const known = 10;

    const ratio = ratioFromKnownCount(width, height, known)!;

    // Feeding that ratio straight back must reproduce the count it came from.
    expect(estimateFromProportion(width, height, ratio)).toBe(known);
  });

  it('refuses a degenerate measurement rather than storing a nonsense ratio', () => {
    expect(ratioFromKnownCount(0, 100, 5)).toBeNull();
    expect(ratioFromKnownCount(100, 0, 5)).toBeNull();
    expect(ratioFromKnownCount(100, 100, 0)).toBeNull();
  });
});

describe('countRims', () => {
  it('reports zero for a degenerate image', () => {
    expect(countRims(fakeImage(0, 0, () => [0, 0, 0]), 2)).toBe(0);
    expect(countRims(fakeImage(10, 2, () => [0, 0, 0]), 2)).toBe(0);
  });

  it('reports zero for a uniform image (no chips, just background)', () => {
    expect(countRims(fakeImage(20, 60, () => [120, 120, 120]), 2)).toBe(0);
  });

  it('counts one rim per boundary between stacked chips', () => {
    const stripeHeight = 20;
    const stripeCount = 6;

    const image = fakeImage(20, stripeHeight * stripeCount, (_x, y) => {
      const shade = Math.floor(y / stripeHeight) % 2 === 0 ? 220 : 60;
      return [shade, shade, shade];
    });

    expect(countRims(image, 3)).toBe(stripeCount - 1);
  });

  /** The whole point of taking spacing from geometry: it suppresses sub-chip detail. */
  it('ignores detail finer than the given minimum spacing', () => {
    const image = fakeImage(20, 120, (_x, y) => {
      const shade = y % 4 === 0 ? 220 : 60;
      return [shade, shade, shade];
    });

    expect(countRims(image, 40)).toBeLessThan(countRims(image, 2));
  });
});

describe('verticalExtent', () => {
  it('finds the run of rows matching the target colour', () => {
    // Rows 10..29 are red; the rest is grey background.
    const image = fakeImage(10, 40, (_x, y) =>
      y >= 10 && y < 30 ? [198, 40, 40] : [128, 128, 128],
    );

    const extent = verticalExtent(image, [198, 40, 40], 60);

    expect(extent.start).toBe(10);
    expect(extent.end).toBe(30);
  });

  it('returns an empty span when nothing matches', () => {
    const image = fakeImage(10, 20, () => [10, 200, 10]);

    const extent = verticalExtent(image, [200, 10, 10], 20);

    expect(extent.end - extent.start).toBe(0);
  });
});

describe('columnActivity', () => {
  it('is flat over a uniform background', () => {
    expect(columnActivity(fakeImage(10, 20, () => [100, 100, 100])).every((v) => v === 0)).toBe(
      true,
    );
  });

  it('is high in a column with vertical banding and low in a plain one', () => {
    const image = fakeImage(2, 20, (x, y) => {
      if (x === 1) return [100, 100, 100];
      const shade = y % 2 === 0 ? 220 : 40;
      return [shade, shade, shade];
    });

    const activity = columnActivity(image);

    expect(activity[0]).toBeGreaterThan(activity[1]);
    expect(activity[1]).toBe(0);
  });
});

describe('segmentStacks', () => {
  it('finds nothing in flat activity', () => {
    expect(segmentStacks([0, 0, 0, 0, 0], 5, 2, 1)).toEqual([]);
  });

  it('finds one region above threshold', () => {
    expect(segmentStacks([0, 0, 10, 10, 10, 0, 0], 5, 2, 1)).toEqual([{ start: 2, end: 5 }]);
  });

  it('splits two regions separated by a gap at least minGap wide', () => {
    expect(segmentStacks([10, 10, 0, 0, 0, 10, 10], 5, 2, 2)).toEqual([
      { start: 0, end: 2 },
      { start: 5, end: 7 },
    ]);
  });

  it('bridges a gap narrower than minGap rather than splitting on it', () => {
    expect(segmentStacks([10, 10, 0, 10, 10], 5, 2, 2)).toEqual([{ start: 0, end: 5 }]);
  });

  it('drops a region narrower than minWidth as noise', () => {
    expect(segmentStacks([0, 10, 0, 0, 0], 5, 3, 1)).toEqual([]);
  });
});

describe('matchDenominationColour', () => {
  const candidates: ColourCandidate[] = [
    { token: 'red', swatch: '#c62828' },
    { token: 'blue', swatch: '#1565c0' },
    { token: 'black', swatch: '#212121' },
  ];

  it('returns null with no candidates to match against', () => {
    expect(matchDenominationColour(fakeImage(10, 10, () => [255, 0, 0]), [])).toBeNull();
  });

  it('picks the nearest candidate to a solid-colour stack', () => {
    expect(matchDenominationColour(fakeImage(10, 30, () => [198, 40, 40]), candidates)).toBe('red');
  });

  it('is not fooled by background outside the sampled middle band', () => {
    const image = fakeImage(10, 30, (_x, y) =>
      y < 10 || y >= 20 ? [255, 0, 0] : [21, 101, 192],
    );

    expect(matchDenominationColour(image, candidates)).toBe('blue');
  });
});

describe('analyseFrame', () => {
  const candidates: ColourCandidate[] = [
    { token: 'red', swatch: '#c62828' },
    { token: 'blue', swatch: '#1565c0' },
  ];

  /**
   * Builds a frame with stacks of a given chip count, drawn at the standard
   * chip proportion so the geometric estimate has something honest to read.
   */
  function frameWithStacks(
    stacks: readonly { colour: [number, number, number]; chips: number }[],
    ratio = DEFAULT_CHIP_RATIO,
  ): RgbaImage {
    const stackWidth = 60;
    const gap = 30;
    const margin = 20;
    const chipHeight = stackWidth / ratio;
    const tallest = Math.max(...stacks.map((s) => s.chips));
    const height = Math.ceil(chipHeight * tallest) + 40;
    const width = margin * 2 + stacks.length * stackWidth + (stacks.length - 1) * gap;

    return fakeImage(width, height, (x, y) => {
      for (const [index, stack] of stacks.entries()) {
        const start = margin + index * (stackWidth + gap);
        if (x < start || x >= start + stackWidth) {
          continue;
        }

        const stackHeight = chipHeight * stack.chips;
        const top = (height - stackHeight) / 2;
        if (y < top || y >= top + stackHeight) {
          continue;
        }

        // Alternate a light and dark shade of the chip's own colour so each
        // boundary reads as a rim, the way real stacked chips do.
        const chipIndex = Math.floor((y - top) / chipHeight);
        const scale = chipIndex % 2 === 0 ? 1 : 0.72;
        return [stack.colour[0] * scale, stack.colour[1] * scale, stack.colour[2] * scale];
      }

      return [128, 128, 128];
    });
  }

  it('reports no stacks over a flat frame', () => {
    const result = analyseFrame(fakeImage(200, 200, () => [128, 128, 128]), candidates);

    expect(result.stacks).toEqual([]);
    expect(result.issues).toContain('no-stacks');
  });

  it('flags a frame that is too dark to read', () => {
    const result = analyseFrame(fakeImage(200, 200, () => [10, 10, 10]), candidates);

    expect(result.issues).toContain('too-dark');
  });

  it('flags a frame that is blown out', () => {
    const result = analyseFrame(fakeImage(200, 200, () => [245, 245, 245]), candidates);

    expect(result.issues).toContain('too-bright');
  });

  it('separates two stacks by colour and counts each from its proportions', () => {
    const frame = frameWithStacks([
      { colour: [198, 40, 40], chips: 10 },
      { colour: [21, 101, 192], chips: 6 },
    ]);

    const result = analyseFrame(frame, candidates);

    expect(result.stacks.length).toBe(2);
    expect(result.stacks[0].colourToken).toBe('red');
    expect(result.stacks[1].colourToken).toBe('blue');
    expect(result.stacks[0].byProportion).toBe(10);
    expect(result.stacks[1].byProportion).toBe(6);
  });

  /**
   * The safety property, and the false-positive guard that matters most in
   * practice: a solid red object with no rims at all (a phone case, a book
   * spine) has a stack-shaped outline but nothing stacked about it. The
   * proportion estimate sees "many chips", the rim count sees none, and the
   * disagreement is what stops a confident wrong number reaching the form.
   */
  it('withholds a quantity when the outline says chips but no rims are visible', () => {
    const frame = fakeImage(200, 200, (x, y) => {
      const inBlock = x >= 70 && x < 130 && y >= 40 && y < 160;
      // A faint gradient down the block: enough column variance to be
      // segmented at all, but no banding for a rim to be found in.
      return inBlock ? [180 + (y % 3), 40, 40] : [128, 128, 128];
    });

    const result = analyseFrame(frame, candidates);

    expect(result.stacks.length).toBeGreaterThan(0);
    // A couple of spurious rims from the gradient is fine — what matters is
    // that they come nowhere near what the outline claims.
    expect(result.stacks[0].byRims).toBeLessThan(result.stacks[0].byProportion / 2);
    expect(result.stacks[0].quantity).toBeNull();
    expect(result.issues).toContain('disagreement');
  });

  it('flags a stack running off the top and bottom of the frame', () => {
    // Full-height bands: the stack has no visible end, so its real height is unknown.
    const frame = fakeImage(200, 200, (x, y) => {
      if (x < 70 || x >= 130) {
        return [128, 128, 128];
      }
      const shade = Math.floor(y / 5) % 2 === 0 ? 1 : 0.72;
      return [198 * shade, 40 * shade, 40 * shade];
    });

    const result = analyseFrame(frame, candidates);

    expect(result.issues).toContain('clipped');
  });
});
