import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LoadingService {
  private inumActiveRequests = 0;
  iboolLoading = signal(false);

  show(): void {
    this.inumActiveRequests++;
    this.iboolLoading.set(true);
  }

  hide(): void {
    this.inumActiveRequests = Math.max(0, this.inumActiveRequests - 1);
    if (this.inumActiveRequests === 0) {
      this.iboolLoading.set(false);
    }
  }
}
