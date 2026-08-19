import { assignableRoles, atLeast, rankOf } from './championship.models';

describe('championship roles', () => {
  it('should order the roles the way the API does', () => {
    expect(rankOf('Player')).toBeLessThan(rankOf('TableManager'));
    expect(rankOf('TableManager')).toBeLessThan(rankOf('Admin'));
    expect(rankOf('Admin')).toBeLessThan(rankOf('Owner'));
  });

  it('should treat a role as satisfying itself', () => {
    expect(atLeast('Admin', 'Admin')).toBe(true);
    expect(atLeast('Owner', 'Admin')).toBe(true);
    expect(atLeast('TableManager', 'Admin')).toBe(false);
  });

  it('should only offer roles strictly below the caller', () => {
    // Mirrors the API rule: an Admin who could hand out Admin would create a
    // peer they can no longer demote.
    expect(assignableRoles('Owner')).toEqual(['Player', 'TableManager', 'Admin']);
    expect(assignableRoles('Admin')).toEqual(['Player', 'TableManager']);
    expect(assignableRoles('TableManager')).toEqual(['Player']);
    expect(assignableRoles('Player')).toEqual([]);
  });

  it('should never offer Owner, which is transferred rather than assigned', () => {
    expect(assignableRoles('Owner')).not.toContain('Owner');
  });
});
