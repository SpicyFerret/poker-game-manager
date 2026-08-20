import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { PendingStack } from '../../../core/tables/table.models';
import { StackNoticeDialog } from './stack-notice-dialog';

describe('StackNoticeDialog', () => {
  const stack: PendingStack = {
    ledgerEntryId: 'e1',
    isRebuy: false,
    money: 50,
    chips: [
      {
        denominationId: 'black',
        faceValue: 100,
        effectiveValue: 100,
        colour: 'black',
        quantity: 4,
      },
      { denominationId: 'green', faceValue: 50, effectiveValue: 50, colour: 'green', quantity: 7 },
      // Playing above its face value: the notice has to show both numbers, or
      // someone counts five chips and thinks they are short.
      { denominationId: 'red', faceValue: 5, effectiveValue: 25, colour: 'red', quantity: 10 },
    ],
  };

  let fixture: ComponentFixture<StackNoticeDialog>;
  let closed: boolean | undefined;

  async function open(data: PendingStack): Promise<void> {
    closed = undefined;
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [StackNoticeDialog],
      providers: [
        { provide: MatDialogRef, useValue: { close: (result: boolean) => (closed = result) } },
        { provide: MAT_DIALOG_DATA, useValue: data },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(StackNoticeDialog);
    await fixture.whenStable();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  beforeEach(async () => {
    await open(stack);
  });

  it('should show how many of each chip', () => {
    expect(text()).toContain('4x');
    expect(text()).toContain('7x');
    expect(text()).toContain('10x');
  });

  it('should show what a chip is worth when it differs from its face', () => {
    expect(text()).toContain('vale 25');
  });

  it('should close as confirmed', () => {
    (fixture.componentInstance as unknown as { confirm: () => void }).confirm();

    expect(closed).toBe(true);
  });

  it('should say it is a rebuy when it is one', async () => {
    await open({ ...stack, isRebuy: true });

    expect(text()).toContain('rebuy');
  });
});
