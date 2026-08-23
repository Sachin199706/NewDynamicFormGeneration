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

  private base = `${environment.apiUrl}`;

  constructor(private http: HttpClient) { }

  submit(formId: number, dto: SubmitFormRequest): Observable<ApiResult<number>> {
    return this.http.post<ApiResult<number>>(`${this.base}/forms/${formId}/submissions`, dto);
  }

  getSubmissions(formId: number, page = 1, pageSize = 20): Observable<PagedResult<SubmissionListItem>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<SubmissionListItem>>(`${this.base}/forms/${formId}/submissions`, { params });
  }

  getDetail(submissionId: number): Observable<ApiResult<SubmissionDetail>> {
    return this.http.get<ApiResult<SubmissionDetail>>(`${this.base}/submissions/${submissionId}`);
  }

}
