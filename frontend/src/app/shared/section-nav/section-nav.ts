import {
  AfterViewInit,
  Component,
  ElementRef,
  effect,
  input,
  model,
  viewChild,
} from '@angular/core';

export interface NavSection {
  id: string;
  label: string;
}

/**
 * A horizontally scrollable row of sections.
 *
 * Written rather than using mat-tab-group because Material's tab header pages
 * with arrow buttons and cannot be dragged. On a phone, dragging the strip is
 * how people expect to reach the section that is off-screen — and tapping one
 * should bring it into the middle rather than leaving it half cut off at an edge.
 *
 * The first and last stay put at their ends: nudging them to the centre would
 * leave dead space beside them and make the strip feel broken.
 */
@Component({
  selector: 'app-section-nav',
  imports: [],
  templateUrl: './section-nav.html',
  styleUrl: './section-nav.scss',
})
export class SectionNav implements AfterViewInit {
  readonly sections = input.required<readonly NavSection[]>();
  readonly selected = model.required<string>();

  private readonly strip = viewChild.required<ElementRef<HTMLElement>>('strip');

  constructor() {
    // Centres on selection whether that came from a tap here or from elsewhere.
    effect(() => {
      const id = this.selected();
      queueMicrotask(() => this.centre(id));
    });
  }

  ngAfterViewInit(): void {
    this.centre(this.selected());
  }

  protected select(id: string): void {
    this.selected.set(id);
  }

  private centre(id: string): void {
    const strip = this.strip().nativeElement;
    const button = strip.querySelector<HTMLElement>(`[data-section="${CSS.escape(id)}"]`);

    if (!button) {
      return;
    }

    // scrollIntoView with inline:'center' would also scroll the page vertically
    // on some browsers, which yanks the content out from under the reader.
    // Scrolling the strip itself keeps the movement where it belongs.
    const target = button.offsetLeft - (strip.clientWidth - button.clientWidth) / 2;
    const max = strip.scrollWidth - strip.clientWidth;
    const left = Math.max(0, Math.min(target, max));

    // Not everywhere has smooth scrolling — jsdom has no scrollTo at all. Jumping
    // to the right place beats throwing out of a lifecycle hook and taking the
    // rest of the view's setup with it.
    if (typeof strip.scrollTo === 'function') {
      strip.scrollTo({ left, behavior: 'smooth' });
    } else {
      strip.scrollLeft = left;
    }
  }
}
