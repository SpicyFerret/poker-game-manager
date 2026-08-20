import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { ChipCountEntry, ChipStock } from '../../../core/tables/table.models';
import { CountDialog } from './count-dialog';

describe('CountDialog', () => {
  const chips: ChipStock[] = [
    {
      denominationId: 'white',
      faceValue: 5,
      effectiveValue: 5,
      colour: 'white',
      remaining: 60,
      issued: 40,
    },
    {
      // A chip playing above its face value, which is the whole point of the
      // override — the total has to follow the effective value.
      denominationId: 'blue',
      faceValue: 5,
      effectiveValue: 100,
      colour: 'blue',
      remaining: 92,
      issued: 8,
    },
  ];

  let fixture: ComponentFixture<CountDialog>;
  let closed: ChipCountEntry[] | undefined;

  beforeEach(async () => {
    closed = undefined;

    await TestBed.configureTestingModule({
      imports: [CountDialog],
      providers: [
        {
          provide: MatDialogRef,
          useValue: { close: (result: ChipCountEntry[]) => (closed = result) },
        },
        {
          provide: MAT_DIALOG_DATA,
          useValue: { playerName: 'Pedro', chips, moneyPerUnit: 0.05 },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CountDialog);
    await fixture.whenStable();
  });

  function confirm(): void {
    (fixture.componentInstance as unknown as { confirm: () => void }).confirm();
  }

  function units(): number {
    return (fixture.componentInstance as unknown as { units: () => number }).units();
  }

  /**
   * Types into the real input, so Angular's number value accessor converts it
   * the way it does for a person holding a phone.
   */
  async function type(index: number, value: string): Promise<void> {
    const inputs = (fixture.nativeElement as HTMLElement).querySelectorAll('input[type="number"]');
    const input = inputs[index] as HTMLInputElement;

    input.value = value;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  it('should total by effective value, not face value', async () => {
    await type(0, '10');
    await type(1, '3');

    expect(units()).toBe(10 * 5 + 3 * 100);
  });

  it('should report a blank box as zero rather than leaving it out', async () => {
    await type(0, '4');

    confirm();

    expect(closed).toEqual([
      { denominationId: 'white', quantity: 4 },
      { denominationId: 'blue', quantity: 0 },
    ]);
  });

  it('should let someone report holding nothing at all', () => {
    confirm();

    expect(closed?.every((count) => count.quantity === 0)).toBe(true);
  });

  it('should refuse a negative count', async () => {
    await type(0, '-3');

    expect(units()).toBe(0);
  });

  it('should start from what the player reported before', async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [CountDialog],
      providers: [
        { provide: MatDialogRef, useValue: { close: () => undefined } },
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            playerName: 'Pedro',
            chips,
            moneyPerUnit: 0.05,
            existing: { white: 7 },
          },
        },
      ],
    }).compileComponents();

    const corrected = TestBed.createComponent(CountDialog);
    await corrected.whenStable();

    expect((corrected.componentInstance as unknown as { units: () => number }).units()).toBe(35);
  });
});
