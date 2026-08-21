import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  ChipSet,
  atLeast,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { CHIP_COLOURS, ChipColour, chipColour } from '../../../../shared/chip-colours';
import { ChipScanDialog, ChipScanResult } from '../../../../shared/chip-scan/chip-scan-dialog';
import { Confirm } from '../../../../shared/confirm/confirm.service';

@Component({
  selector: 'app-chip-sets-tab',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './chip-sets.html',
  styleUrl: './chip-sets.scss',
})
export class ChipSetsTab implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly championships = inject(ChampionshipsService);
  private readonly confirm = inject(Confirm);
  private readonly matDialog = inject(MatDialog);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  protected readonly chipSets = signal<ChipSet[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  /** Which chip set is open in the editor, or 'new'. Null means the list. */
  protected readonly editing = signal<string | 'new' | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    denominations: this.formBuilder.array([this.denominationGroup()]),
  });

  ngOnInit(): void {
    this.load();
  }

  protected get denominations(): FormArray {
    return this.form.controls.denominations;
  }

  /** The palette offered in the editor. */
  protected readonly palette = CHIP_COLOURS;

  /** Resolves a stored token to a colour, or null for anything unrecognised. */
  protected colourOf(token: string | null): ChipColour | null {
    return chipColour(token);
  }

  protected canAdminister(): boolean {
    return atLeast(this.callerRole(), 'Admin');
  }

  private denominationGroup(faceValue = 0, effectiveValue = 0, quantity = 0, colour = '') {
    return this.formBuilder.nonNullable.group({
      faceValue: [faceValue, [Validators.required, Validators.min(1)]],
      effectiveValue: [effectiveValue, [Validators.required, Validators.min(1)]],
      quantity: [quantity, [Validators.required, Validators.min(0)]],
      colour: [colour],
    });
  }

  protected load(): void {
    this.championships.chipSets(this.championshipId()).subscribe({
      next: (chipSets) => this.chipSets.set(chipSets),
      error: (err: unknown) =>
        this.error.set(
          describeError(
            err,
            $localize`:@@chipSets.loadFailed:Não foi possível carregar as maletas.`,
          ),
        ),
    });
  }

  protected startNew(): void {
    this.form.reset({ name: '' });
    this.denominations.clear();
    this.denominations.push(this.denominationGroup());
    this.editing.set('new');
  }

  protected startEdit(chipSet: ChipSet): void {
    this.form.patchValue({ name: chipSet.name });
    this.denominations.clear();

    for (const denomination of chipSet.denominations) {
      this.denominations.push(
        this.denominationGroup(
          denomination.faceValue,
          denomination.effectiveValue,
          denomination.quantity,
          denomination.colour ?? '',
        ),
      );
    }

    this.editing.set(chipSet.id);
  }

  protected cancel(): void {
    this.editing.set(null);
    this.error.set(null);
  }

  /** Fills the count from a photo of the stack instead of typing it. */
  protected scanDenomination(index: number): void {
    const group = this.denominations.at(index);
    const faceValue = Number(group.value.faceValue) || 0;

    this.matDialog
      .open(ChipScanDialog, {
        data: {
          label:
            faceValue > 0
              ? $localize`:@@chipScan.chipLabel:Ficha ${faceValue}:VALUE:`
              : $localize`:@@chipScan.newChip:Ficha nova`,
        },
        maxWidth: '100vw',
        width: '100vw',
        height: '100dvh',
      })
      .afterClosed()
      .subscribe((result: ChipScanResult | undefined) => {
        if (!result) {
          return;
        }

        group.patchValue({ quantity: result.quantity });

        if (!group.value.colour && result.colourToken) {
          group.patchValue({ colour: result.colourToken });
        }
      });
  }

  protected addDenomination(): void {
    this.denominations.push(this.denominationGroup());
  }

  protected removeDenomination(index: number): void {
    this.denominations.removeAt(index);
  }

  protected save(): void {
    const target = this.editing();

    if (this.form.invalid || this.busy() || target === null) {
      return;
    }

    const value = this.form.getRawValue();
    const denominations = value.denominations.map((d) => ({
      faceValue: Number(d.faceValue),
      effectiveValue: Number(d.effectiveValue),
      quantity: Number(d.quantity),
      colour: d.colour.trim() === '' ? null : d.colour.trim(),
    }));

    // Caught here as well as by the API: face value is how a player asks for a
    // chip at the table, so two chips in one case cannot share one.
    const faceValues = new Set(denominations.map((d) => d.faceValue));

    if (faceValues.size !== denominations.length) {
      this.error.set(
        $localize`:@@chipSets.duplicateFaceValue:Cada ficha da maleta precisa ter um valor impresso diferente.`,
      );
      return;
    }

    this.confirm
      .ask({
        title:
          target === 'new'
            ? $localize`:@@confirm.chipSetCreateTitle:Criar esta maleta?`
            : $localize`:@@confirm.chipSetSaveTitle:Salvar a maleta?`,
        details: [
          { label: $localize`:@@field.chipSetName:Nome da maleta`, value: value.name.trim() },
          {
            label: $localize`:@@confirm.chipSetDenominations:tipos de ficha`,
            value: String(denominations.length),
          },
        ],
        confirmLabel: $localize`:@@common.save:Salvar`,
      })
      .subscribe(() => this.persist(target, value.name.trim(), denominations));
  }

  private persist(
    target: string,
    name: string,
    denominations: {
      faceValue: number;
      effectiveValue: number;
      quantity: number;
      colour: string | null;
    }[],
  ): void {
    this.busy.set(true);
    this.error.set(null);

    // Widened to unknown: create answers with the new id and update with
    // nothing, and a union of the two observables has no callable subscribe.
    const request: Observable<unknown> =
      target === 'new'
        ? this.championships.createChipSet(this.championshipId(), name, denominations)
        : this.championships.updateChipSet(this.championshipId(), target, name, denominations);

    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.editing.set(null);
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(err, $localize`:@@chipSets.saveFailed:Não foi possível salvar a maleta.`),
        );
      },
    });
  }

  protected remove(chipSet: ChipSet): void {
    this.confirm
      .ask({
        title: $localize`:@@confirm.chipSetDeleteTitle:Excluir esta maleta?`,
        message: $localize`:@@confirm.chipSetDeleteMessage:Maleta usada por alguma mesa não pode ser excluída — o histórico delas depende dela.`,
        details: [{ label: $localize`:@@field.chipSetName:Nome da maleta`, value: chipSet.name }],
        destructive: true,
        confirmLabel: $localize`:@@common.delete:Excluir`,
      })
      .subscribe(() => this.removeConfirmed(chipSet));
  }

  private removeConfirmed(chipSet: ChipSet): void {
    this.busy.set(true);
    this.error.set(null);

    this.championships.deleteChipSet(this.championshipId(), chipSet.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(
          describeError(
            err,
            $localize`:@@chipSets.deleteFailed:Não foi possível excluir a maleta.`,
          ),
        );
      },
    });
  }
}
