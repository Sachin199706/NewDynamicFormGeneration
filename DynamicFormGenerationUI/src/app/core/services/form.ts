import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { FormListItem, FormRenderPayload, FormVersion, SaveFormVersionRequest } from '../models/form.model';
import { environment } from '../../../environments/environment';
import { ApiResult, PagedResult } from '../models/api-result.model';

@Injectable({
  providedIn: 'root',
})
export class FormService {
  private base = `${environment.apiUrl}/forms`;

  constructor(private http: HttpClient) { }

  getForms(page = 1, pageSize = 10, search?: string): Observable<PagedResult<FormListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedResult<FormListItem>>(this.base, { params });
  }

  getLatestVersion(formId: number): Observable<ApiResult<FormVersion>> {
    return this.http.get<ApiResult<FormVersion>>(`${this.base}/${formId}/versions/latest`);
  }

  saveVersion(dto: SaveFormVersionRequest): Observable<ApiResult<FormVersion>> {
    return this.http.post<ApiResult<FormVersion>>(`${this.base}/versions`, dto);
  }

  publish(formId: number, versionId: number): Observable<ApiResult<boolean>> {
    return this.http.put<ApiResult<boolean>>(`${this.base}/${formId}/versions/${versionId}/publish`, {});
  }

  getRenderPayload(formId: number, versionId: number): Observable<ApiResult<FormRenderPayload>> {
    return this.http.get<ApiResult<FormRenderPayload>>(`${this.base}/${formId}/versions/${versionId}/render`);
  }

}
