import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { vi } from 'vitest';

import { environment } from '../../../../environments/environment';
import { ChampionshipSummary } from '../../../core/championships/championship.models';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { ChampionshipList } from './list';

describe('ChampionshipList', () => {
  let fixture: ComponentFixture<ChampionshipList>;
  let http: HttpTestingController;
  let activity: Map<string, Subject<void>>;

  function summary(id: string): ChampionshipSummary {
    return {
      id,
      name: id,
      description: null,
      role: 'Player',
      memberCount: 1,
      leaderDisplayName: null,
      leaderPoints: 0,
    };
  }

  interface Exposed {
    items: () => ChampionshipSummary[];
    error: () => string | null;
    drop: (event: Pick<CdkDragDrop<ChampionshipSummary[]>, 'previousIndex' | 'currentIndex'>) => void;
  }

  function instance(): Exposed {
    return fixture.componentInstance as unknown as Exposed;
  }

  async function load(items: ChampionshipSummary[]): Promise<void> {
    activity = new Map();

    await TestBed.configureTestingModule({
      imports: [ChampionshipList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: RealtimeService,
          useValue: {
            watch: (championshipId: string) => {
              const subject = new Subject<void>();
              activity.set(championshipId, subject);
              return subject;
            },
          },
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(ChampionshipList);
    await fixture.whenStable();

    http.expectOne(`${environment.apiUrl}/championships`).flush(items);
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  it('should reorder locally and persist the new order', async () => {
    await load([summary('a'), summary('b'), summary('c')]);

    instance().drop({ previousIndex: 0, currentIndex: 2 });

    // Instant feedback: the local list already reflects the move.
    expect(instance().items().map((c) => c.id)).toEqual(['b', 'c', 'a']);

    const request = http.expectOne(`${environment.apiUrl}/championships/order`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ championshipIds: ['b', 'c', 'a'] });
    request.flush(null);
  });

  it('should do nothing when a card is dropped back where it started', async () => {
    await load([summary('a'), summary('b')]);

    instance().drop({ previousIndex: 0, currentIndex: 0 });

    http.expectNone(`${environment.apiUrl}/championships/order`);
  });

  /**
   * A failed save must not leave the screen claiming an order the server
   * never actually kept — the whole point of the optimistic update is that it
   * matches reality once the request settles.
   */
  it('should roll back the local order when persisting fails', async () => {
    await load([summary('a'), summary('b')]);

    instance().drop({ previousIndex: 0, currentIndex: 1 });
    expect(instance().items().map((c) => c.id)).toEqual(['b', 'a']);

    http
      .expectOne(`${environment.apiUrl}/championships/order`)
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(instance().items().map((c) => c.id)).toEqual(['a', 'b']);
    expect(instance().error()).toBeTruthy();
  });

  /**
   * A card's leader (or membership) can change from something that happened
   * in a different tab entirely — the whole reason this screen watches every
   * championship it lists, not just the one currently open.
   */
  it('should reload the whole list when any watched championship reports a change', async () => {
    await load([summary('a'), summary('b')]);

    vi.useFakeTimers();

    activity.get('a')?.next();
    vi.advanceTimersByTime(300);

    http.expectOne(`${environment.apiUrl}/championships`).flush([summary('a')]);

    vi.useRealTimers();
  });
});
