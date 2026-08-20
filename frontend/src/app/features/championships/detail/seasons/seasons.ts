import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';

import { describeError } from '../../../../core/api/problem-details';
import {
  ChampionshipRole,
  Season,
  atLeast,
} from '../../../../core/championships/championship.models';
import { ChampionshipsService } from '../../../../core/championships/championships.service';
import { Confirm } from '../../../../shared/confirm/confirm.service';

@Component({
  selector: 'app-seasons-tab',
  imports: [
    ReactiveFormsModule,
    MatListModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './seasons.html',
  styleUrl: './seasons.scss',
})
export class SeasonsTab implements OnInit {
  private readonly championships = inject(ChampionshipsService);
  private readonly confirm = inject(Confirm);

  readonly championshipId = input.required<string>();
  readonly callerRole = input.required<ChampionshipRole>();

  protected readonly seasons = signal<Season[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(60)]],
    startsOn: ['', [Validators.required]],
    // Blank leaves the season open-ended, which is what an ongoing year is.
    endsOn: [''],
  });

  ngOnInit(): void {
    this.load();
  }

  protected canAdminister(): boolean {
    return atLeast(this.callerRole(), 'Admin');
  }

  protected load(): void {
    this.championships.seasons(this.championshipId()).subscribe({
      next: (seasons) => this.seasons.set(seasons),
      error: (err: unknown) =>
        this.error.set(
          describeError(
            err,
            $localize`:@@seasons.loadFailed:Não foi possível carregar as temporadas.`,
          ),
        ),
    });
  }

  protected create(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    const value = this.form.getRawValue();

    this.confirm
      .ask({
        title: $localize`:@@confirm.seasonTitle:Criar esta temporada?`,
        message: $localize`:@@confirm.seasonMessage:Os rankings passam a somar por esta janela de datas.`,
        details: [
          { label: $localize`:@@field.seasonName:Nome da temporada`, value: value.name.trim() },
          { label: $localize`:@@field.startsOn:Começa em`, value: value.startsOn },
          {
            label: $localize`:@@field.endsOn:Termina em`,
            value: value.endsOn === '' ? $localize`:@@seasons.open:— em aberto` : value.endsOn,
          },
        ],
        confirmLabel: $localize`:@@seasons.create:Criar temporada`,
      })
      .subscribe(() => this.persist(value.name.trim(), value.startsOn, value.endsOn));
  }

  private persist(name: string, startsOn: string, endsOn: string): void {
    this.busy.set(true);
    this.error.set(null);

    this.championships
      .createSeason(this.championshipId(), name, startsOn, endsOn === '' ? null : endsOn)
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.form.reset({ name: '', startsOn: '', endsOn: '' });
          this.load();
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.error.set(
            describeError(
              err,
              $localize`:@@seasons.createFailed:Não foi possível criar a temporada. Confira se as datas não se sobrepõem a outra.`,
            ),
          );
        },
      });
  }
}
