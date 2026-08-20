import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { BlindLevel, BlindLevelInput, suggestLadder } from '../../../core/tables/table.models';

export interface BlindLevelsData {
  levels: readonly BlindLevel[];
  /** The smallest chip in the case: nobody can post a blind below it. */
  smallestChip: number;
}

/** A row while it is being edited, where a cleared box is empty rather than zero. */
interface LevelRow {
  smallBlind: number | null;
  bigBlind: number | null;
  ante: number | null;
  minutes: number | null;
}

/**
 * The blind ladder for one table.
 *
 * Duration is entered in minutes because that is how anyone talks about a level;
 * the API takes seconds, and the conversion belongs here rather than in
 * everyone's head.
 *
 * Saving an empty ladder is a real answer, not an incomplete one — it turns the
 * clock off, which is how most casual nights run.
 */
@Component({
  selector: 'app-blind-levels-dialog',
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './blind-levels-dialog.html',
  styleUrl: './blind-levels-dialog.scss',
})
export class BlindLevelsDialog {
  private readonly dialogRef = inject(MatDialogRef<BlindLevelsDialog>);

  protected readonly data = inject<BlindLevelsData>(MAT_DIALOG_DATA);

  protected readonly rows = signal<LevelRow[]>(
    this.data.levels.map((level) => ({
      smallBlind: level.smallBlind,
      bigBlind: level.bigBlind,
      ante: level.ante,
      minutes: Math.round(level.durationSeconds / 60),
    })),
  );

  protected add(): void {
    const rows = this.rows();
    const last = rows[rows.length - 1];

    // A new level starts at double the one before, which is the step people
    // reach for anyway, and saves typing the obvious.
    const smallBlind = last?.smallBlind ? last.smallBlind * 2 : this.data.smallestChip;

    this.rows.update((current) => [
      ...current,
      {
        smallBlind,
        bigBlind: smallBlind * 2,
        ante: 0,
        minutes: last?.minutes ?? 20,
      },
    ]);
  }

  protected remove(index: number): void {
    this.rows.update((current) => current.filter((_, position) => position !== index));
  }

  protected suggest(): void {
    this.rows.set(
      suggestLadder(this.data.smallestChip).map((level) => ({
        smallBlind: level.smallBlind,
        bigBlind: level.bigBlind,
        ante: level.ante,
        minutes: Math.round(level.durationSeconds / 60),
      })),
    );
  }

  protected set(index: number, field: keyof LevelRow, value: number | null): void {
    this.rows.update((current) =>
      current.map((row, position) => (position === index ? { ...row, [field]: value } : row)),
    );
  }

  /** Every blind has to be a real number; the ante and the duration may be nothing. */
  protected isValid(): boolean {
    return this.rows().every((row) => (row.smallBlind ?? 0) > 0 && (row.bigBlind ?? 0) > 0);
  }

  protected save(): void {
    if (!this.isValid()) {
      return;
    }

    this.dialogRef.close(
      this.rows().map((row) => ({
        smallBlind: row.smallBlind ?? 0,
        bigBlind: row.bigBlind ?? 0,
        ante: row.ante ?? 0,
        durationSeconds: (row.minutes ?? 0) * 60,
      })) satisfies BlindLevelInput[],
    );
  }
}
