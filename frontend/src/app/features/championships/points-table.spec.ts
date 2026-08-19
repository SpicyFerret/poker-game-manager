import { formatPointsTable, parsePointsTable } from './points-table';

describe('parsePointsTable', () => {
  it('should read a comma separated list, spacing and all', () => {
    expect(parsePointsTable('10, 7,5 , 3')).toEqual([10, 7, 5, 3]);
  });

  it('should treat blank as no scoring positions', () => {
    expect(parsePointsTable('   ')).toEqual([]);
  });

  it('should allow zero, which is a real choice for lower places', () => {
    expect(parsePointsTable('10,0,0')).toEqual([10, 0, 0]);
  });

  it.each(['10, x, 5', '10,,5', '10, -3', '10, 2.5', '10 7 5'])(
    'should reject %s rather than silently dropping entries',
    (text) => {
      expect(parsePointsTable(text)).toBeNull();
    },
  );

  it('should round-trip through the formatter', () => {
    expect(parsePointsTable(formatPointsTable([10, 7, 5]))).toEqual([10, 7, 5]);
  });
});
