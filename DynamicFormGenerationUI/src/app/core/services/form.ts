import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateFormTemplateRequest, DashboardItems, FormListItem, FormPublishHistoryItem, FormRenderPayload, FormVersion, FormVersionListItem, SaveFormVersionRequest } from '../models/form.model';
import { environment } from '../../../environments/environment';
import { ApiResult, PagedResult } from '../models/api-result.model';

@Injectable({
  providedIn: 'root',
})
export class FormService {
  private istrBase = `${environment.apiUrl}/forms`;

  constructor(private iobjHttp: HttpClient) { }

  getForms(aNumPage = 1, aNumPageSize = 10, aStrSearch?: string, aFromDate?: string | null, aToDate?: string | null): Observable<PagedResult<FormListItem>> {
    let lobjParams = new HttpParams().set('page', aNumPage);
    lobjParams = lobjParams.set('pageSize', aNumPageSize);
    if (aStrSearch) 
    {
      lobjParams = lobjParams.set('search', aStrSearch);
    }
    if (aFromDate) 
    {
      lobjParams = lobjParams.set('fromDate', aFromDate);
    }
    if (aToDate) 
    {
      lobjParams = lobjParams.set('toDate', aToDate);
    } 
    return this.iobjHttp.get<PagedResult<FormListItem>>(this.istrBase, { params: lobjParams });
  }

  getLatestVersion(aNumFormId: number): Observable<ApiResult<FormVersion>> {
    return this.iobjHttp.get<ApiResult<FormVersion>>(`${this.istrBase}/${aNumFormId}/versions/latest`);
  }

  saveVersion(aObjDto: SaveFormVersionRequest): Observable<ApiResult<FormVersion>> {
    return this.iobjHttp.post<ApiResult<FormVersion>>(`${this.istrBase}/versions`, aObjDto);
  }

  publish(aNumFormId: number, aNumVersionId: number): Observable<ApiResult<boolean>> {
    return this.iobjHttp.put<ApiResult<boolean>>(`${this.istrBase}/${aNumFormId}/versions/${aNumVersionId}/publish`, {});
  }

  getRenderPayload(aNumFormId: number, aNumVersionId: number): Observable<ApiResult<FormRenderPayload>> {
    return this.iobjHttp.get<ApiResult<FormRenderPayload>>(`${this.istrBase}/${aNumFormId}/versions/${aNumVersionId}/render`);
  }

  getAllVersions(): Observable<FormVersionListItem[]> {
    return this.iobjHttp.get<FormVersionListItem[]>(`${this.istrBase}/versions/all`);
  }
  getPublishHistory(): Observable<FormPublishHistoryItem[]> {
    return this.iobjHttp.get<FormPublishHistoryItem[]>(`${this.istrBase}/publish-history`);
  }

  getVersionById(aNumVersionId: number): Observable<ApiResult<FormVersion>> {
    return this.iobjHttp.get<ApiResult<FormVersion>>(`${this.istrBase}/versions/${aNumVersionId}`);
  }

  getDashboardCount(): Observable<DashboardItems> {
      return this.iobjHttp.get<DashboardItems>(`${this.istrBase}/versions/dashboardcount`);
  }
  createTemplate(aObjTemplate: CreateFormTemplateRequest): Observable<ApiResult<FormListItem>> {
    return this.iobjHttp.post<ApiResult<FormListItem>>(this.istrBase, aObjTemplate);
  }
}