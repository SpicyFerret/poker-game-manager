/**
 * The colours poker chips actually come in.
 *
 * A fixed palette rather than a free colour picker, for a reason that shows up on
 * a phone at a table: an arbitrary hex can land invisible against the card in
 * light mode, dark mode, or both, and there is no way to pick one that is safe in
 * every theme. These carry an explicit swatch and a text colour that contrasts
 * with it, so a chip reads the same on every device.
 *
 * The value stored is the token, never the hex. Changing a shade later then
 * updates every chip already saved, rather than leaving old rows on an old
 * colour.
 */
export interface ChipColour {
  /** Stored in the database. Stable — do not rename. */
  token: string;
  swatch: string;
  /** Readable on top of the swatch. */
  ink: string;
  label: string;
}

export const CHIP_COLOURS: readonly ChipColour[] = [
  {
    token: 'white',
    swatch: '#f5f5f5',
    ink: '#1a1a1a',
    label: $localize`:@@chipColour.white:Branca`,
  },
  { token: 'red', swatch: '#c62828', ink: '#ffffff', label: $localize`:@@chipColour.red:Vermelha` },
  {
    token: 'green',
    swatch: '#2e7d32',
    ink: '#ffffff',
    label: $localize`:@@chipColour.green:Verde`,
  },
  { token: 'blue', swatch: '#1565c0', ink: '#ffffff', label: $localize`:@@chipColour.blue:Azul` },
  {
    token: 'black',
    swatch: '#212121',
    ink: '#ffffff',
    label: $localize`:@@chipColour.black:Preta`,
  },
  {
    token: 'purple',
    swatch: '#6a1b9a',
    ink: '#ffffff',
    label: $localize`:@@chipColour.purple:Roxa`,
  },
  {
    token: 'yellow',
    swatch: '#f9a825',
    ink: '#1a1a1a',
    label: $localize`:@@chipColour.yellow:Amarela`,
  },
  {
    token: 'orange',
    swatch: '#ef6c00',
    ink: '#ffffff',
    label: $localize`:@@chipColour.orange:Laranja`,
  },
  { token: 'pink', swatch: '#ad1457', ink: '#ffffff', label: $localize`:@@chipColour.pink:Rosa` },
  { token: 'grey', swatch: '#757575', ink: '#ffffff', label: $localize`:@@chipColour.grey:Cinza` },
];

const BY_TOKEN = new Map(CHIP_COLOURS.map((colour) => [colour.token, colour]));

/**
 * Resolves a stored value to a colour. Returns null for anything unrecognised —
 * including the free text that could be typed before the palette existed — so
 * callers fall back to a plain card rather than rendering a broken swatch.
 */
export function chipColour(token: string | null | undefined): ChipColour | null {
  return token ? (BY_TOKEN.get(token) ?? null) : null;
}
