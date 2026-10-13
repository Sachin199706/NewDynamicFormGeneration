import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_GLOBAL_LOADER } from '../interceptors/loading-interceptor';
import { ApiResult } from '../models/api-result.model';
import { ClassifyFieldLabelsResult, FormImageAiStatus } from '../models/form-image.model';

/**
 * Calls the backend AI endpoints of the "Upload Form Image" feature.
 */
@Injectable({
  providedIn: 'root',
})
export class FormImageService {
  private istrBase = `${environment.apiUrl}/form-image`;

  constructor(private iobjHttp: HttpClient) { }

  /** Tells whether the local AI service is running and the model is installed. */
  getStatus(): Observable<ApiResult<FormImageAiStatus>> {
    return this.iobjHttp.get<ApiResult<FormImageAiStatus>>(`${this.istrBase}/status`);
  }

  /**
   * Asks the AI for the control type of each label. Can take one to two minutes on a computer
   * without a graphics card, so the full-screen spinner is skipped and the page shows its own progress.
   */
  classifyLabels(aArrLabels: string[]): Observable<ApiResult<ClassifyFieldLabelsResult>> {
    return this.iobjHttp.post<ApiResult<ClassifyFieldLabelsResult>>(
      `${this.istrBase}/classify-labels`,
      { labels: aArrLabels },
      { context: new HttpContext().set(SKIP_GLOBAL_LOADER, true) }
    );
  }
}
