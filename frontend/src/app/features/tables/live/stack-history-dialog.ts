import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

import { StackHistoryEntry } from '../../../core/tables/table.models';
import { ChipColour, chipColour } from '../../../shared/chip-colours';

export interface StackHistoryData {
  playerName: string;
  entries: readonly StackHistoryEntry[];
}

/**
 * Every buy-in and rebuy someone was actually dealt tonight, chips and all —
 * for checking against what is physically in front of them, without waiting
 * for a fresh notice to show up.
 */
@Component({
  selector: 'app-stack-history-dialog',
  imports: [DatePipe, DecimalPipe, MatDialogModule, MatButtonModule],
  templateUrl: './stack-history-dialog.html',
  styleUrl: './stack-history-dialog.scss',
})
export class StackHistoryDialog {
  protected readonly data = inject<StackHistoryData>(MAT_DIALOG_DATA);

  protected colourOf(token: string | null): ChipColour | null {
    return chipColour(token);
  }
}
