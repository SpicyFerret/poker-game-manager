import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NavSection, SectionNav } from './section-nav';

@Component({
  imports: [SectionNav],
  template: `<app-section-nav [sections]="sections" [(selected)]="selected" />`,
})
class Host {
  readonly sections: NavSection[] = [
    { id: 'members', label: 'Membros' },
    { id: 'invites', label: 'Convites' },
    { id: 'chips', label: 'Maletas' },
  ];

  readonly selected = signal('members');
}

describe('SectionNav', () => {
  let fixture: ComponentFixture<Host>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();

    fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
  });

  function buttons(): HTMLButtonElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
  }

  it('should render one item per section', () => {
    expect(buttons().map((b) => b.textContent?.trim())).toEqual(['Membros', 'Convites', 'Maletas']);
  });

  it('should mark the selected one for assistive tech, not just visually', () => {
    expect(buttons()[0].getAttribute('aria-selected')).toBe('true');
    expect(buttons()[1].getAttribute('aria-selected')).toBe('false');
  });

  it('should write the selection back to the host', async () => {
    buttons()[2].click();
    await fixture.whenStable();

    expect(fixture.componentInstance.selected()).toBe('chips');
    expect(buttons()[2].getAttribute('aria-selected')).toBe('true');
  });

  it('should follow a selection made from outside', async () => {
    fixture.componentInstance.selected.set('invites');
    await fixture.whenStable();

    expect(buttons()[1].getAttribute('aria-selected')).toBe('true');
  });
});
