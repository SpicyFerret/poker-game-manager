import { Injectable, signal } from '@angular/core';

import { DEFAULT_CHIP_RATIO } from './chip-counter';

const STORAGE_KEY = 'pgm.chipRatio';

/**
 * How tall a chip stands relative to how wide it is — the one number the
 * counter needs that depends on the physical chips rather than the photo.
 *
 * Kept on the device rather than on the championship: it describes the chips
 * whoever is holding this phone is looking at, and the same person uses the
 * same set every week. Sending it to the API would make it a shared setting
 * that one player's bad calibration could break for everyone.
 */
@Injectable({ providedIn: 'root' })
export class ChipCalibration {
  private readonly stored = signal<number | null>(read());

  /** True once someone has actually measured their own chips. */
  readonly isCalibrated = (): boolean => this.stored() !== null;

  readonly ratio = (): number => this.stored() ?? DEFAULT_CHIP_RATIO;

  save(ratio: number): void {
    // A ratio outside this range is not a chip — it is a mis-framed photo, and
    // storing it would quietly poison every later count.
    if (!Number.isFinite(ratio) || ratio < 3 || ratio > 40) {
      return;
    }

    this.stored.set(ratio);
    try {
      localStorage.setItem(STORAGE_KEY, String(ratio));
    } catch {
      // Private mode, or storage full. Calibration still applies this session.
    }
  }

  clear(): void {
    this.stored.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing to do — the in-memory value is already cleared.
    }
  }
}

function read(): number | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const value = raw === null ? NaN : Number(raw);

    return Number.isFinite(value) && value >= 3 && value <= 40 ? value : null;
  } catch {
    return null;
  }
}
