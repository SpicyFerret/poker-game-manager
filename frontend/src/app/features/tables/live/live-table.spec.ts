import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WritableSignal } from '@angular/core';

import { TableDetail, TablePlayer } from '../../../core/tables/table.models';
import { LiveTable } from './live-table';

describe('LiveTable', () => {
  const championshipId = '9a1f1d4c-1c58-4d8e-9a0b-0f2b3c4d5e6f';
  const tableId = 't1';

  const me: TablePlayer = {
    tablePlayerId: 'p1',
    userId: 'u1',
    displayName: 'Eu',
    status: 'Playing',
    seatOrder: 0,
    paidIn: 50,
    rebuyCount: 0,
    hasPaymentHandle: true,
  };

  const other: TablePlayer = {
    tablePlayerId: 'p2',
    userId: 'u2',
    displayName: 'Amigo',
    status: 'Playing',
    seatOrder: 1,
    paidIn: 50,
    rebuyCount: 0,
    hasPaymentHandle: true,
  };

  function table(overrides: Partial<TableDetail> = {}): TableDetail {
    return {
      id: tableId,
      championshipId,
      name: 'Mesa',
      status: 'Running',
      buyIn: 50,
      rebuy: 50,
      moneyPerUnit: 0.05,
      buyInUnits: 1000,
      joinPolicy: 'AnyMember',
      allowLateEntry: true,
      joinCode: null,
      smallChipReserve: 0,
      startedAtUtc: '2026-08-20T00:00:00Z',
      players: [me, other],
      stock: [],
      totalPaidIn: 100,
      canManage: false,
      myPlayerId: 'p1',
      pendingStacks: [],
      ...overrides,
    };
  }

  let fixture: ComponentFixture<LiveTable>;

  interface Exposed {
    table: WritableSignal<TableDetail | null>;
    ownPlayer: () => TablePlayer | undefined;
    canUseOwnFooter: () => boolean;
    manageOthers: () => boolean;
    toggleManageOthers: () => void;
  }

  function instance(): Exposed {
    return fixture.componentInstance as unknown as Exposed;
  }

  /**
   * Sets the table directly rather than going through ngOnInit's poll — these
   * tests are about the computed logic built on top of the table signal, not
   * about the polling itself, and the signal is the seam between the two.
   */
  function load(detail: TableDetail): void {
    fixture = TestBed.createComponent(LiveTable);
    fixture.componentRef.setInput('championshipId', championshipId);
    fixture.componentRef.setInput('tableId', tableId);
    instance().table.set(detail);
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LiveTable],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  /** The footer is the whole point: it exists so a seated player never has to hunt their own card. */
  it('should offer the footer to a seated, playing caller', () => {
    load(table());

    expect(instance().ownPlayer()?.tablePlayerId).toBe('p1');
    expect(instance().canUseOwnFooter()).toBe(true);
  });

  it('should not offer the footer to someone not seated at this table', () => {
    load(table({ myPlayerId: null }));

    expect(instance().ownPlayer()).toBeUndefined();
    expect(instance().canUseOwnFooter()).toBe(false);
  });

  it('should not offer the footer while the caller is only in standby', () => {
    load(table({ players: [{ ...me, status: 'Standby' }, other] }));

    expect(instance().canUseOwnFooter()).toBe(false);
  });

  it('should not offer the footer once the table has stopped running', () => {
    load(table({ status: 'Counting' }));

    expect(instance().canUseOwnFooter()).toBe(false);
  });

  it('should start with the per-player controls for others switched off', () => {
    load(table({ canManage: true }));

    expect(instance().manageOthers()).toBe(false);
  });

  it('should flip on and off', () => {
    load(table({ canManage: true }));

    instance().toggleManageOthers();
    expect(instance().manageOthers()).toBe(true);

    instance().toggleManageOthers();
    expect(instance().manageOthers()).toBe(false);
  });
});
