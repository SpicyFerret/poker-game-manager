import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { Member } from '../../../core/championships/championship.models';

export interface AddTablePlayerData {
  /** Championship members not already seated here — nobody else is worth offering. */
  candidates: Member[];
}

/**
 * Seating someone who is not doing it themselves — the only door onto an
 * InviteOnly table, and useful on any table for someone not looking at their
 * phone right now. A picker rather than an email field, unlike adding a
 * championship member: everyone offered here is already a member, so there is
 * a list to choose from instead of a name to type.
 */
@Component({
  selector: 'app-add-table-player-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './add-table-player-dialog.html',
})
export class AddTablePlayerDialog {
  private readonly formBuilder = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<AddTablePlayerDialog>);

  protected readonly data = inject<AddTablePlayerData>(MAT_DIALOG_DATA);

  protected readonly form = this.formBuilder.nonNullable.group({
    userId: ['', [Validators.required]],
  });

  protected confirm(): void {
    if (this.form.invalid) {
      return;
    }

    this.dialogRef.close(this.form.getRawValue().userId);
  }
}
