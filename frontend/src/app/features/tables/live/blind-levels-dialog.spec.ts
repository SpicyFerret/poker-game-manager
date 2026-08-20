import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { BlindLevel, BlindLevelInput } from '../../../core/tables/table.models';
import { BlindLevelsDialog } from './blind-levels-dialog';

describe('BlindLevelsDialog', () => {
  const existing: BlindLevel[] = [
    { order: 1, smallBlind: 5, bigBlind: 10, ante: 0, durationSeconds: 900 },
    { order: 2, smallBlind: 10, bigBlind: 20, ante: 0, durationSeconds: 900 },
  ];

  let fixture: ComponentFixture<BlindLevelsDialog>;
  let closed: BlindLevelInput[] | undefined;

  async function open(levels: BlindLevel[]): Promise<void> {
    closed = undefined;
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [BlindLevelsDialog],
      providers: [
        {
          provide: MatDialogRef,
          useValue: { close: (result: BlindLevelInput[]) => (closed = result) },
        },
        { provide: MAT_DIALOG_DATA, useValue: { levels, smallestChip: 5 } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(BlindLevelsDialog);
    await fixture.whenStable();
  }

  function call<T>(name: string, ...args: unknown[]): T {
    const instance = fixture.componentInstance as unknown as Record<
      string,
      (...args: unknown[]) => T
    >;

    return instance[name](...args);
  }

  beforeEach(async () => {
    await open(existing);
  });

  it('should convert the stored seconds into minutes to edit', async () => {
    call('save');

    expect(closed?.[0].durationSeconds).toBe(900);
  });

  it('should add a level at double the one before it', () => {
    call('add');
    call('save');

    expect(closed?.[2]).toEqual({
      smallBlind: 20,
      bigBlind: 40,
      ante: 0,
      durationSeconds: 900,
    });
  });

  it('should start a first level at the smallest chip in the case', async () => {
    await open([]);

    call('add');
    call('save');

    expect(closed?.[0].smallBlind).toBe(5);
  });

  it('should remove the level asked for, not the last one', () => {
    call('remove', 0);
    call('save');

    expect(closed?.map((level) => level.smallBlind)).toEqual([10]);
  });

  /** Turning the clock off is a real answer, not an unfinished form. */
  it('should allow saving no levels at all', async () => {
    await open([]);

    call('save');

    expect(closed).toEqual([]);
  });

  it('should refuse to save a level with no blind', () => {
    call('set', 0, 'smallBlind', null);

    expect(call<boolean>('isValid')).toBe(false);

    call('save');

    expect(closed).toBeUndefined();
  });

  it('should replace the ladder wholesale when suggesting one', () => {
    call('suggest');
    call('save');

    expect(closed?.length).toBe(8);
    expect(closed?.[0].smallBlind).toBe(5);
  });
});
