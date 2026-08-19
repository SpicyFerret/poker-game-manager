import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { CreateInviteDialog, CreateInviteResult } from './create-invite-dialog';

describe('CreateInviteDialog', () => {
  let fixture: ComponentFixture<CreateInviteDialog>;
  let closed: CreateInviteResult | undefined;

  beforeEach(async () => {
    closed = undefined;

    await TestBed.configureTestingModule({
      imports: [CreateInviteDialog],
      providers: [
        {
          provide: MatDialogRef,
          useValue: { close: (result: CreateInviteResult) => (closed = result) },
        },
        { provide: MAT_DIALOG_DATA, useValue: { assignableRoles: ['Player', 'TableManager'] } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CreateInviteDialog);
    await fixture.whenStable();
  });

  function confirm(): void {
    (fixture.componentInstance as unknown as { confirm: () => void }).confirm();
  }

  /**
   * Types into the real input so Angular's value accessor converts it the way it
   * does for a person. Setting the control directly would not reproduce the bug
   * this covers: the whole problem was that <input type="number"> hands the
   * control a number whatever it was initialised with.
   */
  async function typeMaxUses(value: string): Promise<void> {
    const input = (fixture.nativeElement as HTMLElement).querySelector(
      'input[type="number"]',
    ) as HTMLInputElement;

    input.value = value;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  it('should default to the first role the caller may hand out', () => {
    confirm();

    expect(closed?.role).toBe('Player');
  });

  it('should close with null when no limit is typed', () => {
    confirm();

    expect(closed?.maxUses).toBeNull();
  });

  it('should close with the typed limit as a number', async () => {
    await typeMaxUses('5');

    confirm();

    expect(closed?.maxUses).toBe(5);
  });

  it('should close with null again after the limit is cleared', async () => {
    await typeMaxUses('5');
    await typeMaxUses('');

    confirm();

    expect(closed?.maxUses).toBeNull();
  });

  it('should not close at all for a limit below one', async () => {
    await typeMaxUses('0');

    confirm();

    expect(closed).toBeUndefined();
  });
});
