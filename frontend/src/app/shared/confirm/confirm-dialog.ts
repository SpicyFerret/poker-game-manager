import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

/** One line of detail, such as a chip to count out. */
export interface ConfirmDetail {
  label: string;
  value: string;
  /** Palette swatch, when the line is about a chip. */
  swatch?: string;
  ink?: string;
}

export interface ConfirmData {
  title: string;
  message?: string;
  details?: ConfirmDetail[];
  confirmLabel: string;

  /** Tints the confirm button as a warning and slows the eye down. */
  destructive?: boolean;

  /**
   * When set, the exact text has to be typed before confirming. Reserved for the
   * genuinely irreversible — a yes/no prompt is far too easy to answer wrong at
   * 2am, and these take a night's bookkeeping or a whole season with them.
   */
  requireTyped?: string;
  requireTypedLabel?: string;

  /** Shown in place of the confirm button when the action cannot proceed. */
  blockedReason?: string;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './confirm-dialog.html',
  styleUrl: './confirm-dialog.scss',
})
export class ConfirmDialog {
  private readonly dialogRef = inject(MatDialogRef<ConfirmDialog>);

  protected readonly data = inject<ConfirmData>(MAT_DIALOG_DATA);

  protected readonly typed = inject(FormBuilder).nonNullable.control('');

  protected canConfirm(): boolean {
    if (this.data.blockedReason) {
      return false;
    }

    return !this.data.requireTyped || this.typed.value.trim() === this.data.requireTyped;
  }

  protected confirm(): void {
    if (this.canConfirm()) {
      this.dialogRef.close(true);
    }
  }
}
