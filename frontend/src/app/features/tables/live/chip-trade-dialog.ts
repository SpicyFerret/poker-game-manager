import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { TablePlayer } from '../../../core/tables/table.models';

export interface ChipTradeData {
  buyer: TablePlayer;
  sellers: TablePlayer[];
  defaultAmount: number;
}

export interface ChipTradeResult {
  sellerPlayerId: string;
  amount: number;
}

/**
 * Buying chips off another player, for when the case is empty. Offers the
 * fullest stacks first, since those are the people who can actually spare them.
 */
@Component({
  selector: 'app-chip-trade-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './chip-trade-dialog.html',
})
export class ChipTradeDialog {
  private readonly formBuilder = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<ChipTradeDialog>);

  protected readonly data = inject<ChipTradeData>(MAT_DIALOG_DATA);

  protected readonly form = this.formBuilder.group({
    sellerPlayerId: this.formBuilder.nonNullable.control<string>('', [Validators.required]),
    // number | null to match what <input type="number"> actually puts here.
    amount: this.formBuilder.control<number | null>(this.data.defaultAmount, [
      Validators.required,
      Validators.min(0.01),
    ]),
  });

  /**
   * Most paid in first: someone several rebuys deep is holding the chips, and is
   * the least awkward person to ask.
   */
  protected sellers(): TablePlayer[] {
    return [...this.data.sellers].sort((a, b) => b.paidIn - a.paidIn);
  }

  protected confirm(): void {
    const value = this.form.getRawValue();

    if (this.form.invalid || value.amount === null) {
      return;
    }

    this.dialogRef.close({
      sellerPlayerId: value.sellerPlayerId,
      amount: value.amount,
    } satisfies ChipTradeResult);
  }
}
