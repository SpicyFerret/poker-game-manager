import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: { description?: string }[] | Record<string, string[]>;
}

/**
 * The API answers failures with ProblemDetails (see Web.Api's
 * CustomResults.Problem). This pulls out the most specific message available so
 * a form can show the real reason instead of "something went wrong".
 */
export function describeError(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  if (error.status === 0) {
    return $localize`:@@error.offline:Sem conexão com o servidor.`;
  }

  const problem = error.error as ProblemDetails | string | null;

  if (typeof problem === 'string' || !problem) {
    return fallback;
  }

  const validation = firstValidationMessage(problem.errors);

  return validation ?? problem.detail ?? problem.title ?? fallback;
}

function firstValidationMessage(errors: ProblemDetails['errors']): string | null {
  if (!errors) {
    return null;
  }

  if (Array.isArray(errors)) {
    return errors.find((e) => e.description)?.description ?? null;
  }

  return Object.values(errors).flat().find(Boolean) ?? null;
}
