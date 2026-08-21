import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { Member } from '../../../core/championships/championship.models';
import { AddTablePlayerDialog } from './add-table-player-dialog';

describe('AddTablePlayerDialog', () => {
  const candidates: Member[] = [
    { userId: 'u1', displayName: 'Pedro', role: 'Player', joinedAtUtc: '', hasPaymentHandle: true },
    {
      userId: 'u2',
      displayName: 'Amigo',
      role: 'Player',
      joinedAtUtc: '',
      hasPaymentHandle: false,
    },
  ];

  let fixture: ComponentFixture<AddTablePlayerDialog>;
  let closed: string | undefined;

  async function open(data: Member[]): Promise<void> {
    closed = undefined;
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [AddTablePlayerDialog],
      providers: [
        { provide: MatDialogRef, useValue: { close: (result: string) => (closed = result) } },
        { provide: MAT_DIALOG_DATA, useValue: { candidates: data } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AddTablePlayerDialog);
    await fixture.whenStable();
  }

  function confirm(): void {
    (fixture.componentInstance as unknown as { confirm: () => void }).confirm();
  }

  beforeEach(async () => {
    await open(candidates);
  });

  it('should not close with nobody picked', () => {
    confirm();

    expect(closed).toBeUndefined();
  });

  it('should close with the picked userId once one is selected', () => {
    (
      fixture.componentInstance as unknown as {
        form: { setValue: (v: { userId: string }) => void };
      }
    ).form.setValue({ userId: 'u2' });

    confirm();

    expect(closed).toBe('u2');
  });

  /** Everyone in the championship is already at the table — a real answer, not an empty form. */
  it('should show nobody left to add rather than an empty picker', async () => {
    await open([]);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('já está nesta mesa');
    expect((fixture.nativeElement as HTMLElement).querySelector('mat-select')).toBeNull();
  });
});
