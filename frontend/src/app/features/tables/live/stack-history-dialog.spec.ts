import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';

import { StackHistoryData, StackHistoryDialog } from './stack-history-dialog';

describe('StackHistoryDialog', () => {
  let fixture: ComponentFixture<StackHistoryDialog>;

  function load(data: StackHistoryData): void {
    TestBed.configureTestingModule({
      imports: [StackHistoryDialog],
      providers: [{ provide: MAT_DIALOG_DATA, useValue: data }],
    });

    fixture = TestBed.createComponent(StackHistoryDialog);
    fixture.detectChanges();
  }

  it('should list every buy-in and rebuy handed to the player', () => {
    load({
      playerName: 'Pedro',
      entries: [
        {
          ledgerEntryId: 'l1',
          isRebuy: false,
          money: 50,
          createdAtUtc: '2026-01-01T00:00:00Z',
          chips: [
            {
              denominationId: 'd1',
              faceValue: 100,
              effectiveValue: 100,
              colour: 'black',
              quantity: 10,
            },
          ],
        },
        {
          ledgerEntryId: 'l2',
          isRebuy: true,
          money: 50,
          createdAtUtc: '2026-01-01T01:00:00Z',
          chips: [],
        },
      ],
    });

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Buy-in');
    expect(text).toContain('Rebuy');
    expect(text).toContain('10x');
  });

  it('should say when nothing has been dealt yet', () => {
    load({ playerName: 'Pedro', entries: [] });

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Ainda não pegou fichas');
  });
});
