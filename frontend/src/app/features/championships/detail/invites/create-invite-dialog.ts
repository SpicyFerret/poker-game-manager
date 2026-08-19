import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { ChampionshipRole } from '../../../../core/championships/championship.models';
import { RoleLabelPipe } from '../../../../core/championships/role-label.pipe';

export interface CreateInviteData {
  assignableRoles: ChampionshipRole[];
}

export interface CreateInviteResult {
  role: ChampionshipRole;
  maxUses: number | null;
}

@Component({
  selector: 'app-create-invite-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    RoleLabelPipe,
  ],
  templateUrl: './create-invite-dialog.html',
})
export class CreateInviteDialog {
  private readonly formBuilder = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<CreateInviteDialog>);

  protected readonly data = inject<CreateInviteData>(MAT_DIALOG_DATA);

  protected readonly form = this.formBuilder.group({
    role: this.formBuilder.nonNullable.control<ChampionshipRole>(
      this.data.assignableRoles[0] ?? 'Player',
      [Validators.required],
    ),
    // number | null, matching what <input type="number"> actually writes. Null is
    // the empty case and means unlimited.
    maxUses: this.formBuilder.control<number | null>(null, [Validators.min(1)]),
  });

  protected confirm(): void {
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();

    this.dialogRef.close({
      role: value.role,
      maxUses: value.maxUses,
    } satisfies CreateInviteResult);
  }
}
