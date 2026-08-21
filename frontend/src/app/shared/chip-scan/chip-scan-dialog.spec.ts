import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { ChipScanData, ChipScanDialog, ChipScanResult } from './chip-scan-dialog';

describe('ChipScanDialog', () => {
  let fixture: ComponentFixture<ChipScanDialog>;
  let closed: ChipScanResult | undefined;

  async function open(data: ChipScanData): Promise<void> {
    closed = undefined;
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [ChipScanDialog],
      providers: [
        {
          provide: MatDialogRef,
          useValue: { close: (result: ChipScanResult | undefined) => (closed = result) },
        },
        { provide: MAT_DIALOG_DATA, useValue: data },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ChipScanDialog);
    await fixture.whenStable();
  }

  function instance(): ChipScanDialog {
    return fixture.componentInstance;
  }

  beforeEach(async () => {
    await open({ label: 'Ficha 25' });
  });

  it('shows the denomination label in the heading', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Ficha 25');
  });

  it('starts idle, offering to open the camera rather than firing a permission prompt on open', () => {
    expect((instance() as unknown as { phase: () => string }).phase()).toBe('idle');
  });

  /**
   * jsdom exposes no `navigator.mediaDevices.getUserMedia`, so this exercises
   * the same fallback path a real phone would hit if the user denies the
   * camera permission or the browser lacks support entirely.
   */
  it('falls back gracefully when the camera is unavailable', async () => {
    await (instance() as unknown as { startCamera: () => Promise<void> }).startCamera();
    await fixture.whenStable();

    expect((instance() as unknown as { phase: () => string }).phase()).toBe('unsupported');
  });

  it('offers a way to cancel without ever touching the camera', () => {
    const cancelButton = (fixture.nativeElement as HTMLElement).querySelector(
      '[mat-dialog-close]',
    );

    expect(cancelButton).not.toBeNull();
    expect(closed).toBeUndefined();
  });

  it('closes with the edited quantity and colour once confirmed', () => {
    const dialog = instance() as unknown as {
      quantity: { set: (v: number | null) => void };
      colourToken: { set: (v: string | null) => void };
      confirm: () => void;
    };

    dialog.quantity.set(7);
    dialog.colourToken.set('red');
    dialog.confirm();

    expect(closed).toEqual({ quantity: 7, colourToken: 'red' });
  });

  it('closes with zero when nothing was ever estimated or typed', () => {
    (instance() as unknown as { confirm: () => void }).confirm();

    expect(closed).toEqual({ quantity: 0, colourToken: null });
  });

  it('clamps a negative typed quantity to null rather than sending it', () => {
    const dialog = instance() as unknown as {
      setQuantity: (v: number | null) => void;
      quantity: () => number | null;
    };

    dialog.setQuantity(-3);

    expect(dialog.quantity()).toBeNull();
  });
});
