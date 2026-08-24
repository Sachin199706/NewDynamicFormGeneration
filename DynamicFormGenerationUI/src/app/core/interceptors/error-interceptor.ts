import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, of, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      // Business validation failures (400) still carry a well-formed ApiResult body
      // ({ success:false, errors:[...] }) — route them back to the caller's normal
      // .subscribe(res => ...) instead of erroring out, since that's what every
      // component in this app expects to read res.success/res.errors from.
      if (err.status === 400 && err.error && typeof err.error.success === 'boolean') {
        return of(err.error);
      }

      // Genuine failures (network down, 404, 500, etc.) — log and rethrow so
      // nothing gets silently swallowed.
      console.error(`HTTP ${err.status} on ${req.method} ${req.url}`, err);
      return throwError(() => err);
    })
  );
};