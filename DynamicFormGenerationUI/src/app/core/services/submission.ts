import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ApiResult, PagedResult } from '../models/api-result.model';
import { Observable } from 'rxjs';
import { SubmissionDetail, SubmissionListItem, SubmitFormRequest } from '../models/rule.model';

@Injectable({
  providedIn: 'root',
})
export class SubmissionService {

  private istrBase = `${environment.apiUrl}`;

  constructor(private iobjHttp: HttpClient) { }

  submit(aNumFormId: number, aObjFormData: FormData): Observable<ApiResult<number>> {
    return this.iobjHttp.post<ApiResult<number>>(`${this.istrBase}/forms/${aNumFormId}/submissions`, aObjFormData);
  }

  getSubmissions(aNumFormId: number, aNumPage = 1, aNumPageSize = 20): Observable<PagedResult<SubmissionListItem>> {
    const lobjParams = new HttpParams().set('page', aNumPage).set('pageSize', aNumPageSize);
    return this.iobjHttp.get<PagedResult<SubmissionListItem>>(`${this.istrBase}/forms/${aNumFormId}/submissions`, { params: lobjParams });
  }

  getDetail(aNumSubmissionId: number): Observable<ApiResult<SubmissionDetail>> {
    return this.iobjHttp.get<ApiResult<SubmissionDetail>>(`${this.istrBase}/submissions/${aNumSubmissionId}`);
  }

}