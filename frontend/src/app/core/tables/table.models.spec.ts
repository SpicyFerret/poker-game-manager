import {
  TableClock,
  TableStatus,
  TableSummary,
  countUnits,
  formatDuration,
  isActive,
  isFinished,
  issuedUnits,
  offBy,
  remainingUnits,
  secondsLeft,
  sortForDisplay,
  stacksLeft,
  suggestLadder,
  tableMood,
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
    {
      denominationId: 'a',
      faceValue: 5,
      effectiveValue: 5,
      colour: 'white',
      remaining: 60,
      issued: 40,
    },
    {
      denominationId: 'b',
      faceValue: 100,
      effectiveValue: 100,
      colour: null,
      remaining: 92,
      issued: 8,
    },
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

describe('counting helpers', () => {
  const lines = [
    {
      denominationId: 'a',
      faceValue: 5,
      effectiveValue: 5,
      issued: 40,
      counted: 40,
      difference: 0,
    },
    {
      denominationId: 'b',
      faceValue: 25,
      effectiveValue: 100,
      issued: 8,
      counted: 6,
      difference: -2,
    },
  ];

  it('should value a reported stack by effective value', () => {
    expect(countUnits(lines, { a: 10, b: 3 })).toBe(10 * 5 + 3 * 100);
  });

  it('should treat a blank box as none of that chip', () => {
    expect(countUnits(lines, { a: 10, b: null })).toBe(50);
  });

  it('should surface only the chips that do not tally', () => {
    expect(offBy(lines).map((line) => line.denominationId)).toEqual(['b']);
  });
});

describe('blind clock helpers', () => {
  const level = { order: 1, smallBlind: 5, bigBlind: 10, ante: 0, durationSeconds: 600 };

  function clock(overrides: Partial<TableClock> = {}): TableClock {
    return {
      currentLevel: 1,
      isPaused: false,
      elapsedSeconds: 0,
      serverTimeUtc: '2026-08-20T00:00:00Z',
      ...overrides,
    };
  }

  it('should count down from the sample plus however long ago it landed', () => {
    expect(secondsLeft(level, clock({ elapsedSeconds: 120 }), 30)).toBe(450);
  });

  it('should stand still while paused', () => {
    // The 300 seconds since the sample are a break, so the level keeps its time.
    expect(secondsLeft(level, clock({ elapsedSeconds: 120, isPaused: true }), 300)).toBe(480);
  });

  it('should stop at zero rather than going negative', () => {
    expect(secondsLeft(level, clock({ elapsedSeconds: 900 }), 0)).toBe(0);
  });

  it('should report nothing to count for a level with no duration', () => {
    expect(secondsLeft({ ...level, durationSeconds: 0 }, clock(), 40)).toBe(0);
  });

  it('should report nothing when the level is unknown', () => {
    expect(secondsLeft(undefined, clock(), 40)).toBe(0);
  });

  it('should format under an hour as mm:ss', () => {
    expect(formatDuration(605)).toBe('10:05');
    expect(formatDuration(9)).toBe('0:09');
  });

  it('should format an hour or more as h:mm:ss', () => {
    expect(formatDuration(3661)).toBe('1:01:01');
  });

  it('should never format a negative remainder', () => {
    expect(formatDuration(-5)).toBe('0:00');
  });
});

describe('suggestLadder', () => {
  it('should start where the smallest chip in the case allows', () => {
    expect(suggestLadder(25)[0].smallBlind).toBe(25);
  });

  it('should keep the big blind at twice the small one', () => {
    expect(suggestLadder(5).every((level) => level.bigBlind === level.smallBlind * 2)).toBe(true);
  });

  it('should climb, never sit still or fall back', () => {
    const ladder = suggestLadder(5, 8);

    expect(
      ladder.every(
        (level, index) => index === 0 || level.smallBlind > ladder[index - 1].smallBlind,
      ),
    ).toBe(true);
  });

  it('should not suggest a blind of zero when the case has no chips', () => {
    expect(suggestLadder(0)[0].smallBlind).toBe(1);
  });
});

describe('tableMood', () => {
  it('should be idle before anyone has sat down', () => {
    expect(tableMood('Draft')).toBe('idle');
    expect(tableMood('Open')).toBe('idle');
  });

  it('should be live once play has started, through counting and settling', () => {
    expect(tableMood('Running')).toBe('live');
    expect(tableMood('Counting')).toBe('live');
    expect(tableMood('Reconciled')).toBe('live');
    expect(tableMood('Settled')).toBe('live');
  });

  it('should be done once nothing more will happen at the table', () => {
    expect(tableMood('Closed')).toBe('done');
    expect(tableMood('Cancelled')).toBe('done');
  });
});

describe('isFinished', () => {
  it.each<TableStatus>(['Settled', 'Closed'])(
    'should count %s as finished — its result lives in the history tab now',
    (status) => {
      expect(isFinished(status)).toBe(true);
    },
  );

  it.each<TableStatus>(['Draft', 'Open', 'Running', 'Counting', 'Reconciled', 'Cancelled'])(
    'should not count %s as finished',
    (status) => {
      expect(isFinished(status)).toBe(false);
    },
  );
});
