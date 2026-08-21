import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { ChipCountEntry, ChipStock } from '../../../core/tables/table.models';
import { ChipColour, chipColour } from '../../../shared/chip-colours';
import {
  ChipScanCandidate,
  ChipScanData,
  ChipScanDialog,
  ChipScanResult,
} from '../../../shared/chip-scan/chip-scan-dialog';

export interface CountDialogData {
  playerName: string;
  /** The denominations of this table's case, in the order they are shown everywhere else. */
  chips: readonly ChipStock[];
  moneyPerUnit: number;
  /** What this player reported last time, so a correction starts from it. */
  existing?: Readonly<Record<string, number>>;
}

/**
 * A player counting their own stack at the end of the night.
 *
 * One box per denomination rather than a single total, because the
 * reconciliation is per denomination: a total that happens to be right while two
 * chips are swapped would sail through and take the error into the settlement.
 *
 * The running total is shown in money as well as units — it is what everyone
 * actually wants to know while they are counting, and it catches a slipped digit
 * long before the panel does.
 */
@Component({
  selector: 'app-count-dialog',
  imports: [
    DecimalPipe,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  templateUrl: './count-dialog.html',
  styleUrl: './count-dialog.scss',
})
export class CountDialog {
  private readonly dialogRef = inject(MatDialogRef<CountDialog>);
  private readonly matDialog = inject(MatDialog);

  protected readonly data = inject<CountDialogData>(MAT_DIALOG_DATA);

  /** null rather than 0 for an empty box, matching what <input type="number"> puts there. */
  protected readonly quantities = signal<Record<string, number | null>>(
    Object.fromEntries(
      this.data.chips.map((chip) => [
        chip.denominationId,
        this.data.existing?.[chip.denominationId] ?? null,
      ]),
    ),
  );

  protected readonly units = computed(() => {
    const quantities = this.quantities();

    return this.data.chips.reduce(
      (total, chip) => total + (quantities[chip.denominationId] ?? 0) * chip.effectiveValue,
      0,
    );
  });

  protected readonly money = computed(() => this.units() * this.data.moneyPerUnit);

  protected colourOf(token: string | null): ChipColour | null {
    return chipColour(token);
  }

  /** Fills every box at once from a single photo of all the stacks, told apart by colour. */
  protected scanAll(): void {
    const quantities = this.quantities();

    const chips: ChipScanCandidate[] = this.data.chips.map((chip) => ({
      key: chip.denominationId,
      faceValue: chip.faceValue,
      colourToken: chip.colour,
      existingQuantity: quantities[chip.denominationId] ?? null,
    }));

    this.matDialog
      .open(ChipScanDialog, {
        data: { title: $localize`:@@count.scanTitle:a contagem`, chips } satisfies ChipScanData,
        maxWidth: '100vw',
        width: '100vw',
        height: '100dvh',
      })
      .afterClosed()
      .subscribe((results: ChipScanResult[] | undefined) => {
        if (results) {
          for (const result of results) {
            this.set(result.key, result.quantity);
          }
        }
      });
  }

  protected set(denominationId: string, value: number | null): void {
    // Negative chips are not a thing, and the API refuses them anyway.
    const quantity = value === null || value < 0 ? null : Math.floor(value);

    this.quantities.update((current) => ({ ...current, [denominationId]: quantity }));
  }

  /**
   * Blank counts as zero. Someone holding nothing of a denomination should not
   * have to type a nought in every box to be allowed to submit.
   */
  protected confirm(): void {
    const quantities = this.quantities();

    this.dialogRef.close(
      this.data.chips.map((chip) => ({
        denominationId: chip.denominationId,
        quantity: quantities[chip.denominationId] ?? 0,
      })) satisfies ChipCountEntry[],
    );
  }
}
