import { DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

import { PendingStack } from '../../../core/tables/table.models';
import { ChipColour, chipColour } from '../../../shared/chip-colours';

/**
 * "These are the chips you should have."
 *
 * Shown to the player, not the manager: the manager counted the stack out of the
 * case, and the whole value of the check is a second pair of eyes on it. It has
 * no cancel — a stack you were handed is a fact, and the only two useful answers
 * are "counted, correct" and "this is wrong, sort it out at the table".
 */
@Component({
  selector: 'app-stack-notice-dialog',
  imports: [DecimalPipe, MatDialogModule, MatButtonModule],
  templateUrl: './stack-notice-dialog.html',
  styleUrl: './stack-notice-dialog.scss',
})
export class StackNoticeDialog {
  private readonly dialogRef = inject(MatDialogRef<StackNoticeDialog>);

  protected readonly data = inject<PendingStack>(MAT_DIALOG_DATA);

  protected colourOf(token: string | null): ChipColour | null {
    return chipColour(token);
  }

  protected confirm(): void {
    this.dialogRef.close(true);
  }
}
