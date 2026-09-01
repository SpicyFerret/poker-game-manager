import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { TokenStorage } from '../auth/token-storage';

/**
 * "Something changed in this championship" — one signal, shared by every
 * screen that shows championship data. Nobody pushes the new state itself;
 * a screen that hears the signal just refetches through the same service
 * call it already used to load in the first place. That keeps this the only
 * piece of real-time plumbing in the app, instead of a typed push message
 * per entity that changed.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly tokenStorage = inject(TokenStorage);

  private connection: HubConnection | null = null;
  private starting: Promise<void> | null = null;

  // How many active `watch()` subscribers care about each championship, so a
  // reconnect knows which groups to rejoin — SignalR groups do not survive a
  // dropped connection.
  private readonly watched = new Map<string, number>();

  private ensureConnection(): HubConnection {
    if (this.connection) {
      return this.connection;
    }

    const hubUrl = `${environment.apiUrl.replace(/\/api\/v1$/, '')}/hubs/championship-activity`;

    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.tokenStorage.read()?.accessToken ?? '',
        // Auth here travels as a bearer token in the query string, not a
        // cookie, so the connection needs no credentials — and the API's CORS
        // policy does not allow them, the same as every other request it
        // serves.
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();

    this.connection.onreconnected(() => {
      for (const championshipId of this.watched.keys()) {
        void this.connection?.invoke('JoinChampionship', championshipId);
      }
    });

    return this.connection;
  }

  private async start(): Promise<void> {
    const connection = this.ensureConnection();

    if (connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.starting ??= connection.start().catch((err: unknown) => {
      this.starting = null;
      throw err;
    });

    await this.starting;
  }

  /** Emits every time the given championship changes, until unsubscribed. */
  watch(championshipId: string): Observable<void> {
    return new Observable<void>((subscriber) => {
      const connection = this.ensureConnection();

      const handler = (changedId: string): void => {
        if (changedId === championshipId) {
          subscriber.next();
        }
      };

      connection.on('changed', handler);
      this.watched.set(championshipId, (this.watched.get(championshipId) ?? 0) + 1);

      this.start()
        .then(() => connection.invoke('JoinChampionship', championshipId))
        .catch((err: unknown) => subscriber.error(err));

      return () => {
        connection.off('changed', handler);

        const remaining = (this.watched.get(championshipId) ?? 1) - 1;

        if (remaining <= 0) {
          this.watched.delete(championshipId);
        } else {
          this.watched.set(championshipId, remaining);
        }
      };
    });
  }
}
