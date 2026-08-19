/**
 * The points table is edited as free text ("10, 7, 5, 3") rather than a row of
 * spinners: the list has no fixed length, and typing it is far quicker on a
 * phone than adding fields one at a time.
 *
 * Returns null when the text isn't a clean list, so the caller can say so
 * instead of silently dropping entries.
 */
export function parsePointsTable(text: string): number[] | null {
  const trimmed = text.trim();

  if (trimmed === '') {
    return [];
  }

  const parts = trimmed.split(',').map((part) => part.trim());
  const points: number[] = [];

  for (const part of parts) {
    // Integers only, and no negatives — a placing cannot cost you points.
    if (!/^\d+$/.test(part)) {
      return null;
    }

    points.push(Number(part));
  }

  return points;
}

export function formatPointsTable(points: readonly number[]): string {
  return points.join(', ');
}
