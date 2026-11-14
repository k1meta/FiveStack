import { HttpErrorResponse } from '@angular/common/http';

/** Only same-site paths, so a crafted link can't bounce users off-site. */
export function safeReturnUrl(url: string | undefined) {
  return url?.startsWith('/') && !url.startsWith('//') ? url : '/';
}

/**
 * Readable text for a failed API call. ASP.NET returns ValidationProblemDetails: { errors: { field: ["..."] } },
 * or ProblemDetails with a `detail` sentence for 403/409.
 */
export function errorMessage(e: unknown): string {
  if (!(e instanceof HttpErrorResponse)) return 'Something went wrong.';
  if (e.status === 429) return 'Too many attempts. Wait a minute and try again.';
  const errors = e.error?.errors as Record<string, string[]> | undefined;
  if (errors) return Object.values(errors).flat().join(' ');
  return typeof e.error?.detail === 'string' ? e.error.detail : 'Something went wrong.';
}
