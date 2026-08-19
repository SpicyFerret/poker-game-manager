import {
  TableStatus,
  TableSummary,
  isActive,
  issuedUnits,
  remainingUnits,
  sortForDisplay,
  stacksLeft,
} from './table.models';

function summary(name: string, status: TableStatus, createdAtUtc: string): TableSummary {
  return {
    id: name,
    name,
    status,
    buyIn: 50,
    playerCount: 0,
    createdAtUtc,
    startedAtUtc: null,
  };
}

describe('isActive', () => {
  it.each<TableStatus>(['Open', 'Running', 'Counting', 'Reconciled', 'Settled'])(
    'should count %s as still going',
    (status) => {
      // Counting and settling are as much part of the night as the play.
      expect(isActive(status)).toBe(true);
    },
  );

  it.each<TableStatus>(['Closed', 'Cancelled'])('should count %s as finished', (status) => {
    expect(isActive(status)).toBe(false);
  });
});

describe('sortForDisplay', () => {
  it('should put a live table above a newer finished one', () => {
    // The whole point: on a game night the table in progress must not be buried
    // under the ones that already ended.
    const sorted = sortForDisplay([
      summary('closed-yesterday', 'Closed', '2026-08-19T00:00:00Z'),
      summary('running-last-week', 'Running', '2026-08-12T00:00:00Z'),
    ]);

    expect(sorted.map((t) => t.name)).toEqual(['running-last-week', 'closed-yesterday']);
  });

  it('should order newest first within each group', () => {
    const sorted = sortForDisplay([
      summary('open-old', 'Open', '2026-08-01T00:00:00Z'),
      summary('closed-old', 'Closed', '2026-07-01T00:00:00Z'),
      summary('open-new', 'Open', '2026-08-10T00:00:00Z'),
      summary('closed-new', 'Closed', '2026-07-20T00:00:00Z'),
    ]);

    expect(sorted.map((t) => t.name)).toEqual(['open-new', 'open-old', 'closed-new', 'closed-old']);
  });

  it('should not mutate the array it was given', () => {
    const original = [
      summary('closed', 'Closed', '2026-08-19T00:00:00Z'),
      summary('running', 'Running', '2026-08-12T00:00:00Z'),
    ];

    sortForDisplay(original);

    expect(original.map((t) => t.name)).toEqual(['closed', 'running']);
  });
});

describe('chip unit helpers', () => {
  const stock = [
    { denominationId: 'a', faceValue: 5, effectiveValue: 5, colour: 'white', remaining: 60, issued: 40 },
    { denominationId: 'b', faceValue: 100, effectiveValue: 100, colour: null, remaining: 92, issued: 8 },
  ];

  it('should count units by effective value, not face value', () => {
    expect(issuedUnits(stock)).toBe(40 * 5 + 8 * 100);
    expect(remainingUnits(stock)).toBe(60 * 5 + 92 * 100);
  });

  it('should report how many more stacks the case could cover', () => {
    // 9500 units left against a 1000-unit stack.
    expect(stacksLeft(stock, 1000)).toBe(9);
  });

  it('should not divide by zero when the buy-in is unset', () => {
    expect(stacksLeft(stock, 0)).toBe(0);
  });
});
