---
name: add-frontend-feature
description: Scaffold a complete Angular feature slice — standalone component, HttpClient-backed service, route, and specs. Use when the user asks to add a feature, page, screen, or view to the Angular frontend in this repo.
argument-hint: <feature description, e.g. "list todos" or "login page">
---

# Add a Frontend Feature (Angular)

**Scope: frontend Angular (`frontend/src/`) only.** For API/backend work, use `add-feature`, `add-entity`, or `add-tests` instead.

Scaffold a feature following this frontend's conventions: standalone components (no NgModules), signals for local state, a per-feature service wrapping `HttpClient`, and specs for both.

## Workflow

1. **Classify the feature.** A feature is a folder under `frontend/src/app/features/{feature}/` (kebab-case, e.g. `todos`). One feature can contain several components (list, detail, form) sharing one service.
2. **Create or extend the service** — `frontend/src/app/features/{feature}/{feature}.service.ts`. `providedIn: 'root'`, injects `HttpClient` via `inject()`, calls `environment.apiUrl` (already points at `/api/v1` in both dev and prod — see `frontend/src/environments/`). One method per API operation, returning `Observable<T>`.
3. **Create the component** — `frontend/src/app/features/{feature}/{use-case}/{use-case}.component.ts` (+ `.html`, `.scss`). Standalone, injects the service via `inject()`, holds state in `signal`/`computed`.
4. **Register the route** in `frontend/src/app/app.routes.ts` with lazy `loadComponent`.
5. **Write specs** — component spec (`TestBed`, standalone `imports: [Component]`) and service spec (`HttpClientTestingModule`/`provideHttpClientTesting()` + `HttpTestingController`).
6. **Verify:** `npm test -- --watch=false` and `npm run build` from `frontend/`.

## Non-negotiable conventions

- **Standalone only.** No `NgModule`. Components declare their own `imports` array.
- **`inject()` over constructor injection** for services, matching the rest of the app (`app.config.ts` already uses it).
- **Signals for local component state** (`signal`, `computed`, `effect` where needed). No NgRx or other state library unless explicitly asked for.
- **One service per feature**, not per component — components call the service, never `HttpClient` directly.
- **Routes are lazy** (`loadComponent: () => import('./features/.../x.component').then(m => m.XComponent)`), even for the first route in a feature — keeps the pattern consistent as more features are added.
- **`apiUrl` always comes from `environment`**, never hardcoded — in prod it's a same-origin relative path (`/api/v1`, proxied by the Cloudflare Worker in `frontend/worker/`), in dev it's `http://localhost:5000/api/v1`.
- **No inline styles/templates** for anything beyond a couple of lines — use the `.html`/`.scss` sibling files (matches what `ng generate component` produces).

## Naming reference

| Artifact | Pattern | Example |
|---|---|---|
| Feature folder | `features/{feature}/` (kebab-case) | `features/todos/` |
| Service | `{Feature}Service` in `{feature}.service.ts` | `TodosService` in `todos.service.ts` |
| Component | `{UseCase}Component` in `{use-case}/{use-case}.component.ts` | `TodoListComponent` in `todo-list/todo-list.component.ts` |
| Route path | kebab-case, matches the feature | `/todos`, `/todos/:id` |
| Component spec | `{use-case}.component.spec.ts` | `todo-list.component.spec.ts` |
| Service spec | `{feature}.service.spec.ts` | `todos.service.spec.ts` |

## Example: service

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Todo {
  id: string;
  description: string;
  isCompleted: boolean;
}

@Injectable({ providedIn: 'root' })
export class TodosService {
  private readonly http = inject(HttpClient);

  getById(id: string): Observable<Todo> {
    return this.http.get<Todo>(`${environment.apiUrl}/todos/${id}`);
  }
}
```

## Example: component

```typescript
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Todo, TodosService } from '../todos.service';

@Component({
  selector: 'app-todo-detail',
  templateUrl: './todo-detail.component.html',
  styleUrl: './todo-detail.component.scss',
})
export class TodoDetailComponent {
  private readonly todosService = inject(TodosService);
  private readonly route = inject(ActivatedRoute);

  protected readonly todo = signal<Todo | null>(null);

  constructor() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.todosService.getById(id).subscribe((todo) => this.todo.set(todo));
  }
}
```

## Example: route registration

```typescript
// frontend/src/app/app.routes.ts
import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'todos/:id',
    loadComponent: () =>
      import('./features/todos/todo-detail/todo-detail.component').then((m) => m.TodoDetailComponent),
  },
];
```
