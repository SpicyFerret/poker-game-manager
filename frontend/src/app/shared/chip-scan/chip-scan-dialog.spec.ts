import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { ChipCalibration } from './chip-calibration';
import { ChipScanCandidate, ChipScanData, ChipScanDialog, ChipScanResult } from './chip-scan-dialog';
import { AnalysedStack, DEFAULT_CHIP_RATIO, FrameAnalysis, FrameIssue } from './chip-counter';

describe('ChipScanDialog', () => {
  let fixture: ComponentFixture<ChipScanDialog>;
  let closed: ChipScanResult[] | undefined;

  const chips: ChipScanCandidate[] = [
    { key: 'd1', faceValue: 5, colourToken: 'red' },
    { key: 'd2', faceValue: 25, colourToken: 'blue', existingQuantity: 3 },
    { key: 'd3', faceValue: 100, colourToken: null },
  ];

  function stack(partial: Partial<AnalysedStack>): AnalysedStack {
    return {
      columns: { start: 0, end: 10 },
      rows: { start: 0, end: 40 },
      colourToken: null,
      byProportion: 0,
      byRims: 0,
      quantity: null,
      clipped: false,
      ...partial,
    };
  }

  async function open(data: ChipScanData): Promise<void> {
    closed = undefined;
    TestBed.resetTestingModule();
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [ChipScanDialog],
      providers: [
        {
          provide: MatDialogRef,
          useValue: { close: (result: ChipScanResult[] | undefined) => (closed = result) },
        },
        { provide: MAT_DIALOG_DATA, useValue: data },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ChipScanDialog);
    await fixture.whenStable();
  }

  /** The component's members are protected; specs reach them through a narrow cast. */
  function api() {
    return fixture.componentInstance as unknown as {
      phase: () => string;
      quantities: () => Record<string, number | null>;
      analysis: { set: (v: FrameAnalysis | null) => void };
      tilted: { set: (v: boolean) => void };
      steadyFrames: { set: (v: number) => void };
      calibrationCount: { set: (v: number | null) => void };
      guidance: () => string;
      readingLabel: (s: AnalysedStack) => string;
      applyDetections: (d: readonly AnalysedStack[]) => void;
      startCamera: () => Promise<void>;
      set: (key: string, v: number | null) => void;
      setCalibrationCount: (v: number | null) => void;
      confirm: () => void;
    };
  }

  beforeEach(async () => {
    await open({ title: 'a maleta', chips });
  });

  it('shows the heading context', () => {
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain('a maleta');
  });

  it('starts idle, offering to open the camera rather than firing a permission prompt on open', () => {
    expect(api().phase()).toBe('idle');
  });

  it('seeds quantities from what was already there, not blank', () => {
    expect(api().quantities()['d2']).toBe(3);
    expect(api().quantities()['d1']).toBeNull();
  });

  /**
   * jsdom exposes no `navigator.mediaDevices.getUserMedia`, so this exercises
   * the same fallback path a real phone would hit if the user denies the
   * camera permission or the browser lacks support entirely.
   */
  it('falls back gracefully when the camera is unavailable', async () => {
    await api().startCamera();
    await fixture.whenStable();

    expect(api().phase()).toBe('unsupported');
  });

  it('offers a way to cancel without ever touching the camera', () => {
    expect((fixture.nativeElement as HTMLElement).querySelector('[mat-dialog-close]')).not.toBeNull();
    expect(closed).toBeUndefined();
  });

  describe('guidance', () => {
    function guidanceFor(issues: FrameIssue[]): string {
      api().analysis.set({ stacks: [], issues });
      return api().guidance();
    }

    it('puts tilt above everything else, since it silently breaks the measurement', () => {
      api().tilted.set(true);
      api().analysis.set({ stacks: [], issues: ['too-dark'] });

      expect(api().guidance()).toContain('de frente');
    });

    it('names the specific problem rather than a generic failure', () => {
      expect(guidanceFor(['too-dark'])).toContain('escuro');
      expect(guidanceFor(['too-bright'])).toContain('luz');
      expect(guidanceFor(['no-stacks'])).toContain('moldura');
      expect(guidanceFor(['clipped'])).toContain('afaste');
      expect(guidanceFor(['too-small'])).toContain('perto');
      expect(guidanceFor(['disagreement'])).toContain('certeza');
    });

    it('asks the person to hold still once a reading has started to settle', () => {
      api().analysis.set({ stacks: [], issues: [] });
      api().steadyFrames.set(2);

      expect(api().guidance()).toContain('Segure');
    });
  });

  it('shows a question mark rather than a number for a stack it cannot resolve', () => {
    expect(api().readingLabel(stack({ quantity: null }))).toBe('?');
    expect(api().readingLabel(stack({ quantity: 12 }))).toBe('12');
  });

  it('applies a detected stack only to the row whose colour matches, and leaves the rest as they were', () => {
    api().applyDetections([
      stack({ colourToken: 'red', quantity: 12 }),
      // No row is green — this detection should simply be dropped, not
      // mis-assigned to whichever row happens to be closest.
      stack({ colourToken: 'green', quantity: 99 }),
    ]);

    expect(api().quantities()['d1']).toBe(12);
    expect(api().quantities()['d2']).toBe(3);
    expect(api().quantities()['d3']).toBeNull();
  });

  it('ignores an unresolved stack rather than writing a null into a row', () => {
    api().applyDetections([stack({ colourToken: 'red', quantity: null })]);

    expect(api().quantities()['d1']).toBeNull();
  });

  it('closes with one entry per chip, using whatever is in each row at the time', () => {
    api().set('d1', 10);
    api().set('d3', 4);
    api().confirm();

    expect(closed).toEqual([
      { key: 'd1', quantity: 10 },
      { key: 'd2', quantity: 3 },
      { key: 'd3', quantity: 4 },
    ]);
  });

  it('clamps a negative typed quantity to null rather than sending it', () => {
    api().set('d1', -3);

    expect(api().quantities()['d1']).toBeNull();
  });

  it('refuses a calibration count below one', () => {
    api().setCalibrationCount(0);

    expect(
      (fixture.componentInstance as unknown as { calibrationCount: () => number | null }).calibrationCount(),
    ).toBeNull();
  });
});

describe('ChipCalibration', () => {
  let calibration: ChipCalibration;

  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
    calibration = TestBed.inject(ChipCalibration);
  });

  it('falls back to the standard chip proportion until someone measures their own', () => {
    expect(calibration.isCalibrated()).toBe(false);
    expect(calibration.ratio()).toBe(DEFAULT_CHIP_RATIO);
  });

  it('remembers a measured ratio', () => {
    calibration.save(9.5);

    expect(calibration.isCalibrated()).toBe(true);
    expect(calibration.ratio()).toBe(9.5);
  });

  /** A ratio this far off is a mis-framed photo, and storing it would poison every later count. */
  it('rejects a ratio no real chip could have', () => {
    calibration.save(500);
    expect(calibration.isCalibrated()).toBe(false);

    calibration.save(0.2);
    expect(calibration.isCalibrated()).toBe(false);

    calibration.save(Number.NaN);
    expect(calibration.isCalibrated()).toBe(false);
  });

  it('can be cleared back to the default', () => {
    calibration.save(9.5);
    calibration.clear();

    expect(calibration.isCalibrated()).toBe(false);
    expect(calibration.ratio()).toBe(DEFAULT_CHIP_RATIO);
  });
});
