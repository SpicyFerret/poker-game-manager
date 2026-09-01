import { TestBed } from '@angular/core/testing';
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { vi } from 'vitest';

import { RealtimeService } from './realtime.service';

/** Drains the microtask queue enough to settle the service's promise chain. */
async function flushMicrotasks(): Promise<void> {
  for (let i = 0; i < 6; i++) {
    await Promise.resolve();
  }
}

describe('RealtimeService', () => {
  let handlers: Map<string, (...args: unknown[]) => void>;
  let fakeConnection: {
    state: HubConnectionState;
    start: ReturnType<typeof vi.fn>;
    on: ReturnType<typeof vi.fn>;
    off: ReturnType<typeof vi.fn>;
    invoke: ReturnType<typeof vi.fn>;
    onreconnected: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    handlers = new Map();

    fakeConnection = {
      state: HubConnectionState.Disconnected,
      start: vi.fn().mockImplementation(() => {
        fakeConnection.state = HubConnectionState.Connected;
        return Promise.resolve();
      }),
      on: vi.fn((event: string, handler: (...args: unknown[]) => void) => {
        handlers.set(event, handler);
      }),
      off: vi.fn(),
      invoke: vi.fn().mockResolvedValue(undefined),
      onreconnected: vi.fn(),
    };

    vi.spyOn(HubConnectionBuilder.prototype, 'build').mockReturnValue(
      fakeConnection as unknown as ReturnType<HubConnectionBuilder['build']>,
    );

    TestBed.configureTestingModule({});
  });

  afterEach(() => vi.restoreAllMocks());

  it('should join the championship group once started', async () => {
    const service = TestBed.inject(RealtimeService);

    service.watch('champ-1').subscribe();
    await flushMicrotasks();

    expect(fakeConnection.start).toHaveBeenCalled();
    expect(fakeConnection.invoke).toHaveBeenCalledWith('JoinChampionship', 'champ-1');
  });

  it('should emit only for the championship being watched', async () => {
    const service = TestBed.inject(RealtimeService);
    const events: string[] = [];

    service.watch('champ-1').subscribe(() => events.push('champ-1'));
    service.watch('champ-2').subscribe(() => events.push('champ-2'));
    await flushMicrotasks();

    const changed = handlers.get('changed');
    changed?.('champ-2');

    expect(events).toEqual(['champ-2']);
  });

  it('should stop reacting once unsubscribed', async () => {
    const service = TestBed.inject(RealtimeService);
    const events: string[] = [];

    const subscription = service.watch('champ-1').subscribe(() => events.push('champ-1'));
    await flushMicrotasks();

    subscription.unsubscribe();

    const changed = handlers.get('changed');
    changed?.('champ-1');

    expect(events).toEqual([]);
    expect(fakeConnection.off).toHaveBeenCalledWith('changed', expect.any(Function));
  });

  it('should rejoin every watched championship after a reconnect', async () => {
    const service = TestBed.inject(RealtimeService);

    service.watch('champ-1').subscribe();
    service.watch('champ-2').subscribe();
    await flushMicrotasks();

    fakeConnection.invoke.mockClear();

    const rejoin = fakeConnection.onreconnected.mock.calls[0][0] as () => void;
    rejoin();

    expect(fakeConnection.invoke).toHaveBeenCalledWith('JoinChampionship', 'champ-1');
    expect(fakeConnection.invoke).toHaveBeenCalledWith('JoinChampionship', 'champ-2');
  });
});
