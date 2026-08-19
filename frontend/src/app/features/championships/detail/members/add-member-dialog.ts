import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { ChampionshipRole } from '../../../../core/championships/championship.models';
import { RoleLabelPipe } from '../../../../core/championships/role-label.pipe';

export interface AddMemberData {
  assignableRoles: ChampionshipRole[];
}

export interface AddMemberResult {
  email: string;
  role: ChampionshipRole;
}

/**
 * A dialog rather than a form sitting above the list. Adding someone is
 * occasional, and a permanent form pushed the list — the thing people actually
 * came to look at — below the fold on a phone.
 */
@Component({
  selector: 'app-add-member-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    RoleLabelPipe,
  ],
  templateUrl: './add-member-dialog.html',
})
export class AddMemberDialog {
  private readonly formBuilder = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<AddMemberDialog>);

  protected readonly data = inject<AddMemberData>(MAT_DIALOG_DATA);

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    role: [this.data.assignableRoles[0] ?? ('Player' as ChampionshipRole), [Validators.required]],
  });

  protected confirm(): void {
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();

    this.dialogRef.close({
      email: value.email.trim(),
      role: value.role,
    } satisfies AddMemberResult);
  }
}
