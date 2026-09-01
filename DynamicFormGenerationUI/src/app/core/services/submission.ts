import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ApiResult, PagedResult } from '../models/api-result.model';
import { Observable } from 'rxjs';
import { SubmissionDetail, SubmissionFilter, SubmissionOverviewItem, SubmissionStats, SubmitFormRequest } from '../models/rule.model';

@Injectable({
  providedIn: 'root',
})
export class SubmissionService {

  private istrBase = `${environment.apiUrl}`;

  constructor(private iobjHttp: HttpClient) { }

  submit(aNumFormId: number, aObjFormData: FormData): Observable<ApiResult<number>> {
    return this.iobjHttp.post<ApiResult<number>>(`${this.istrBase}/forms/${aNumFormId}/submissions`, aObjFormData);
  }

  getDetail(aNumSubmissionId: number): Observable<ApiResult<SubmissionDetail>> {
    return this.iobjHttp.get<ApiResult<SubmissionDetail>>(`${this.istrBase}/submissions/${aNumSubmissionId}`);
  }

  markAsRead(aNumSubmissionId: number): Observable<ApiResult<boolean>> {
    return this.iobjHttp.put<ApiResult<boolean>>(`${this.istrBase}/submissions/${aNumSubmissionId}/mark-read`, {});
  }

  getAllSubmissions(aObjFilter: SubmissionFilter): Observable<PagedResult<SubmissionOverviewItem>> {
    let lobjParams = new HttpParams()
      .set('page', aObjFilter.page)
      .set('pageSize', aObjFilter.pageSize);

    if (aObjFilter.search) lobjParams = lobjParams.set('search', aObjFilter.search);
    if (aObjFilter.formId != null) lobjParams = lobjParams.set('formId', aObjFilter.formId);
    if (aObjFilter.isRead != null) lobjParams = lobjParams.set('isRead', aObjFilter.isRead);
    if (aObjFilter.fromDate) lobjParams = lobjParams.set('fromDate', aObjFilter.fromDate);
    if (aObjFilter.toDate) lobjParams = lobjParams.set('toDate', aObjFilter.toDate);

    return this.iobjHttp.get<PagedResult<SubmissionOverviewItem>>(`${this.istrBase}/submissions`, { params: lobjParams });
  }

  getStats(): Observable<SubmissionStats> {
    return this.iobjHttp.get<SubmissionStats>(`${this.istrBase}/submissions/stats`);
  }

  getstatsById(ID:number):Observable<SubmissionStats>{
    return this.iobjHttp.get<SubmissionStats>(`${this.istrBase}/submissions/stats/${ID}`);
  }

}